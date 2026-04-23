using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class P2PChatSession : NetworkBehaviour
{
    public static P2PChatSession Instance { get; private set; }

    [Header("채팅 제한")]
    [SerializeField] private float maxMessagesPerSecond = 2f;
    [SerializeField] private float muteDurationSeconds = 3f;

    [Header("인스펙터 테스트")]
    [SerializeField] private string editorTestNarrationText = "테스트 나레이션";
    [SerializeField] private bool logDebugMessages;

    private readonly Dictionary<ulong, Queue<float>> recentMessageTimes = new();
    private readonly Dictionary<ulong, float> mutedUntilTimes = new();

    private bool clientRoomJoinMessageShown;

    private NetworkSessionState Session => NetworkSessionState.Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (Session != null)
        {
            Session.ChatHistory.OnListChanged += OnHistoryChanged;
            Session.HostNickname.OnValueChanged += OnHostNicknameChanged;
        }

        if (IsServer)
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnectedOnServer;

        RefreshHistoryUI();
        TryNotifyClientJoinedRoom();
    }

    public override void OnNetworkDespawn()
    {
        if (Session != null)
        {
            Session.ChatHistory.OnListChanged -= OnHistoryChanged;
            Session.HostNickname.OnValueChanged -= OnHostNicknameChanged;
        }

        if (NetworkManager != null && IsServer)
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnectedOnServer;

        clientRoomJoinMessageShown = false;
    }

    public bool TrySendLocalChat(string rawText)
    {
        if (!IsSpawned || Session == null)
            return false;

        string trimmed = (rawText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return false;

        string filtered = ApplyForbiddenWordFilterStub(trimmed);

        if (IsServer)
            return TryAcceptChatOnServer(NetworkManager.LocalClientId, filtered);

        SubmitChatRpc(filtered);
        return true;
    }

    public void AddNarrationFromLocal(string narrationText)
    {
        string trimmed = (narrationText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || Session == null)
            return;

        if (NetworkCommandAuthority.CanLocalServerRunCommand(this))
            Session.ServerAddNarration(trimmed);
        else
            AddNarrationRpc(trimmed);
    }

    public void AddDividerFromLocal()
    {
        if (Session == null)
            return;

        if (NetworkCommandAuthority.CanLocalServerRunCommand(this))
            Session.ServerAddDivider();
        else
            AddDividerRpc();
    }

    [Rpc(SendTo.Server)]
    private void SubmitChatRpc(string message, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        TryAcceptChatOnServer(senderId, message);
    }

    [Rpc(SendTo.Server)]
    private void AddNarrationRpc(string narrationText, RpcParams rpcParams = default)
    {
        if (Session == null) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (!TryAuthorizeHostOwnedCommand(senderId, "나레이션"))
            return;

        Session.ServerAddNarration(narrationText);
    }

    [Rpc(SendTo.Server)]
    private void AddDividerRpc(RpcParams rpcParams = default)
    {
        if (Session == null) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (!TryAuthorizeHostOwnedCommand(senderId, "구분선"))
            return;

        Session.ServerAddDivider();
    }

    [ClientRpc]
    private void ShowStatusClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        GameStatusMessageRouter.Show(message);
    }

    private bool TryAcceptChatOnServer(ulong senderId, string message)
    {
        if (Session == null)
            return false;

        float now = Time.unscaledTime;

        if (mutedUntilTimes.TryGetValue(senderId, out float mutedUntil) && now < mutedUntil)
        {
            float remain = mutedUntil - now;
            WarnOneClient(senderId, $"채팅이 제한되었습니다. {remain:0.0}초 후 다시 시도하세요.");
            return false;
        }

        Queue<float> queue = GetOrCreateTimeQueue(senderId);

        while (queue.Count > 0 && now - queue.Peek() > 1f)
            queue.Dequeue();

        int allowedCount = Mathf.Max(1, Mathf.FloorToInt(maxMessagesPerSecond));

        if (queue.Count >= allowedCount)
        {
            mutedUntilTimes[senderId] = now + muteDurationSeconds;
            queue.Clear();

            WarnOneClient(senderId, $"채팅 과다 입력으로 {muteDurationSeconds:0.0}초간 제한됩니다.");
            return false;
        }

        queue.Enqueue(now);

        string senderNickname = "Unknown";
        if (P2PChatBootstrap.Instance != null &&
            P2PChatBootstrap.Instance.TryGetNickname(senderId, out string approvedNickname))
        {
            senderNickname = approvedNickname;
        }

        Session.ServerAddNormalChat(senderId, senderNickname, message);

        if (logDebugMessages)
            Debug.Log($"[Chat] {senderNickname}: {message}");

        return true;
    }

    private void WarnOneClient(ulong clientId, string message)
    {
        if (NetworkManager == null)
            return;

        if (clientId == NetworkManager.ServerClientId)
        {
            GameStatusMessageRouter.Show(message);
            return;
        }

        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        };

        ShowStatusClientRpc(message, rpcParams);
    }

    private bool TryAuthorizeHostOwnedCommand(ulong senderId, string commandName)
    {
        if (NetworkCommandAuthority.CanRunHostOwnedCommand(senderId))
            return true;

        string message = $"{commandName} 명령은 호스트만 사용할 수 있습니다.";
        WarnOneClient(senderId, message);

        if (logDebugMessages)
            Debug.LogWarning($"[ChatCommandDenied] clientId={senderId}, command={commandName}");

        return false;
    }

    private Queue<float> GetOrCreateTimeQueue(ulong clientId)
    {
        if (!recentMessageTimes.TryGetValue(clientId, out Queue<float> queue))
        {
            queue = new Queue<float>();
            recentMessageTimes.Add(clientId, queue);
        }

        return queue;
    }

    private void OnClientDisconnectedOnServer(ulong clientId)
    {
        recentMessageTimes.Remove(clientId);
        mutedUntilTimes.Remove(clientId);
    }

    private void OnHistoryChanged(NetworkListEvent<P2PChatEntry> changeEvent)
    {
        if (P2PChatUI.Instance == null || Session == null)
            return;

        P2PChatUI.Instance.ApplyHistoryChange(Session.ChatHistory, changeEvent);
    }

    private void OnHostNicknameChanged(Unity.Collections.FixedString128Bytes previousValue,
                                       Unity.Collections.FixedString128Bytes newValue)
    {
        TryNotifyClientJoinedRoom();
    }

    private void TryNotifyClientJoinedRoom()
    {
        if (!IsClient || IsServer || clientRoomJoinMessageShown || Session == null)
            return;

        string nick = Session.HostNickname.Value.ToString();
        if (string.IsNullOrWhiteSpace(nick))
            return;

        clientRoomJoinMessageShown = true;
        P2PChatBootstrap.Instance.NotifyClientJoinedRoom(nick);
    }

    private void RefreshHistoryUI()
    {
        if (P2PChatUI.Instance == null || Session == null)
            return;

        P2PChatUI.Instance.RefreshHistory(Session.ChatHistory);
    }

    private string ApplyForbiddenWordFilterStub(string rawText)
    {
        return rawText;
    }

    public void Editor_AddTestNarration()
    {
        AddNarrationFromLocal(editorTestNarrationText);
    }

    public void Editor_AddTestDivider()
    {
        AddDividerFromLocal();
    }
}
