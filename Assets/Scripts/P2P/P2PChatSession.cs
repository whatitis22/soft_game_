using System.Collections.Generic;
using Unity.Collections;
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

    private readonly NetworkVariable<FixedString512Bytes> hostNickname =
        new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkList<P2PChatEntry> history;

    private readonly Dictionary<ulong, Queue<float>> recentMessageTimes = new();
    private readonly Dictionary<ulong, float> mutedUntilTimes = new();

    private bool clientRoomJoinMessageShown;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        history = new NetworkList<P2PChatEntry>();
    }

    public override void OnNetworkSpawn()
    {
        history.OnListChanged += OnHistoryChanged;
        hostNickname.OnValueChanged += OnHostNicknameChanged;

        if (IsServer)
        {
            string nick = P2PChatBootstrap.Instance != null
                ? P2PChatBootstrap.Instance.HostNickname
                : string.Empty;

            hostNickname.Value = new FixedString512Bytes(nick);
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnectedOnServer;
        }

        RefreshHistoryUI();
        TryNotifyClientJoinedRoom();
    }

    public override void OnNetworkDespawn()
    {
        history.OnListChanged -= OnHistoryChanged;
        hostNickname.OnValueChanged -= OnHostNicknameChanged;

        if (NetworkManager != null && IsServer)
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnectedOnServer;

        clientRoomJoinMessageShown = false;
    }

    public bool TrySendLocalChat(string rawText)
    {
        if (!IsSpawned)
            return false;

        string trimmed = (rawText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return false;

        string filtered = ApplyForbiddenWordFilterStub(trimmed);

        if (IsServer)
        {
            return TryAcceptChatOnServer(NetworkManager.LocalClientId, filtered);
        }

        SubmitChatRpc(new FixedString512Bytes(filtered));
        return true;
    }

    public void AddNarrationFromLocal(string narrationText)
    {
        string trimmed = (narrationText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return;

        if (IsServer)
        {
            AddNarrationOnServer(trimmed);
        }
        else
        {
            AddNarrationRpc(new FixedString512Bytes(trimmed));
        }
    }

    public void AddDividerFromLocal()
    {
        if (IsServer)
        {
            AddDividerOnServer();
        }
        else
        {
            AddDividerRpc();
        }
    }

    [Rpc(SendTo.Server)]
    private void SubmitChatRpc(FixedString512Bytes message, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        TryAcceptChatOnServer(senderId, message.ToString());
    }

    [Rpc(SendTo.Server)]
    private void AddNarrationRpc(FixedString512Bytes narrationText)
    {
        AddNarrationOnServer(narrationText.ToString());
    }

    [Rpc(SendTo.Server)]
    private void AddDividerRpc()
    {
        AddDividerOnServer();
    }

    [ClientRpc]
    private void ShowStatusClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        P2PChatUI.Instance.SetStatus(message);
    }

    private bool TryAcceptChatOnServer(ulong senderId, string message)
    {
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

        history.Add(new P2PChatEntry(P2PChatEntryType.Normal, senderNickname, message));

        if (logDebugMessages)
            Debug.Log($"[Chat] {senderNickname}: {message}");

        return true;
    }

    private void AddNarrationOnServer(string narrationText)
    {
        history.Add(new P2PChatEntry(P2PChatEntryType.Narration, string.Empty, narrationText));
    }

    private void AddDividerOnServer()
    {
        history.Add(new P2PChatEntry(P2PChatEntryType.Divider, string.Empty, string.Empty));
    }

    private void WarnOneClient(ulong clientId, string message)
    {
        if (NetworkManager == null)
            return;

        if (clientId == NetworkManager.ServerClientId)
        {
            P2PChatUI.Instance.SetStatus(message);
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
        RefreshHistoryUI();
    }

    private void OnHostNicknameChanged(FixedString512Bytes previousValue, FixedString512Bytes newValue)
    {
        TryNotifyClientJoinedRoom();
    }

    private void TryNotifyClientJoinedRoom()
    {
        if (!IsClient || IsServer || clientRoomJoinMessageShown)
            return;

        string nick = hostNickname.Value.ToString();
        if (string.IsNullOrWhiteSpace(nick))
            return;

        clientRoomJoinMessageShown = true;
        P2PChatBootstrap.Instance.NotifyClientJoinedRoom(nick);
    }

    private void RefreshHistoryUI()
    {
        if (P2PChatUI.Instance == null)
            return;

        P2PChatUI.Instance.RefreshHistory(history);
    }

    private string ApplyForbiddenWordFilterStub(string rawText)
    {
        // 금칙어 기능은 나중 단계에서 구현.
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