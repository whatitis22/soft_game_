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
        // 세션의 NetworkList 변경을 구독해 채팅 UI를 이벤트 기반으로 갱신한다.
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

        // 클라이언트는 요청만 보내고, 실제 히스토리 추가는 Host/서버에서 확정한다.
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

        // 나레이션은 호스트 소유 명령이라 원격 클라이언트 요청은 서버에서 다시 검사된다.
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
        // SenderClientId는 클라이언트가 속일 수 없는 Netcode 수신 메타데이터다.
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

        // 간단한 초당 메시지 제한으로 채팅 도배를 서버 쪽에서 막는다.
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
            // 승인된 닉네임만 채팅 발신자로 사용해 클라이언트 임의 표시명을 막는다.
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

        // 실패 사유는 전체 브로드캐스트하지 않고 요청한 클라이언트에게만 보낸다.
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
        // 시스템 채팅 명령은 Host만 사용할 수 있도록 공통 권한 헬퍼를 거친다.
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

        // UI가 전체 재생성 대신 Add/Remove 이벤트를 증분 처리하도록 변경 이벤트를 그대로 넘긴다.
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

        // HostNickname이 도착하면 승인된 방에 정상 입장한 것으로 보고 초기 UI를 전환한다.
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
