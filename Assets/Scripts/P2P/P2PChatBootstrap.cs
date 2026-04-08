using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkManager))]
[RequireComponent(typeof(UnityTransport))]
public class P2PChatBootstrap : MonoBehaviour
{
    public static P2PChatBootstrap Instance { get; private set; }

    [Header("네트워크 설정")]
    [SerializeField] private ushort port = 7777;
    [SerializeField] private int maxParticipants = 5; // 호스트 포함 5명
    [SerializeField] private float connectTimeoutSeconds = 8f;

    private NetworkManager networkManager;
    private UnityTransport transport;

    private readonly Dictionary<ulong, string> approvedNicknames = new();
    private readonly List<ChatHistoryEntry> chatHistory = new();

    private string localNickname;
    private string hostNickname;

    private bool clientJoinPending;
    private bool clientJoinConfirmed;
    private Coroutine connectTimeoutRoutine;

    [Serializable]
    public struct ChatHistoryEntry
    {
        public string sender;
        public string message;
        public string timestamp;

        public ChatHistoryEntry(string sender, string message, string timestamp)
        {
            this.sender = sender;
            this.message = message;
            this.timestamp = timestamp;
        }
    }

    public IReadOnlyList<ChatHistoryEntry> ChatHistory => chatHistory;
    public string HostNickname => hostNickname;
    public string LocalNickname => localNickname;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        networkManager = GetComponent<NetworkManager>();
        transport = GetComponent<UnityTransport>();

        networkManager.NetworkConfig.ConnectionApproval = true;
        networkManager.ConnectionApprovalCallback = ApprovalCheck;
    }

    private void OnEnable()
    {
        networkManager.OnClientConnectedCallback += HandleClientConnected;
        networkManager.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    private void OnDisable()
    {
        if (networkManager == null)
            return;

        networkManager.OnClientConnectedCallback -= HandleClientConnected;
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    public void PrepareClient(string nickname)
    {
        localNickname = nickname.Trim();
        clientJoinPending = false;
        clientJoinConfirmed = false;
        StopConnectTimeout();
    }

    public void StartHost(string nickname)
    {
        ShutdownIfNeeded();

        localNickname = nickname.Trim();
        hostNickname = localNickname;

        approvedNicknames.Clear();
        chatHistory.Clear();

        // host 자신의 로컬 클라이언트는 loopback으로 붙고,
        // 서버는 모든 IPv4 인터페이스에서 수신.
        transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
        networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(localNickname);

        bool started = networkManager.StartHost();
        if (!started)
        {
            P2PChatUI.Instance.SetStatus("방 생성에 실패했습니다.");
            P2PChatUI.Instance.ResetToInitialState();
            return;
        }

        approvedNicknames[NetworkManager.ServerClientId] = localNickname;
        P2PChatUI.Instance.SetStatus("방이 생성되었습니다.");
    }

    public void StartClient(string ip)
    {
        string trimmedIp = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim();

        if (!IsValidIpv4(trimmedIp))
        {
            P2PChatUI.Instance.SetStatus("유효하지 않은 IPv4 주소입니다.");
            return;
        }

        ShutdownIfNeeded();

        clientJoinPending = true;
        clientJoinConfirmed = false;

        transport.SetConnectionData(trimmedIp, port);
        networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(localNickname);

        bool started = networkManager.StartClient();
        if (!started)
        {
            clientJoinPending = false;
            P2PChatUI.Instance.SetStatus("입장에 실패했습니다.");
            return;
        }

        RestartConnectTimeout();
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request,
                               NetworkManager.ConnectionApprovalResponse response)
    {
        string requestedNickname = DecodeNickname(request.Payload);

        if (string.IsNullOrWhiteSpace(requestedNickname))
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Reason = "닉네임이 비어 있습니다.";
            response.Pending = false;
            return;
        }

        // host 포함 최대 인원 제한
        if (networkManager.ConnectedClientsIds.Count >= maxParticipants)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Reason = $"방 인원이 가득 찼습니다. (최대 {maxParticipants}명)";
            response.Pending = false;
            return;
        }

        approvedNicknames[request.ClientNetworkId] = requestedNickname;

        response.Approved = true;
        response.CreatePlayerObject = false; // 아직 플레이어 오브젝트 없음
        response.Pending = false;
    }

    private void HandleClientConnected(ulong clientId)
    {
        // 서버/호스트 쪽: 새 클라이언트 입장 메시지
        if (networkManager.IsServer && clientId != NetworkManager.ServerClientId)
        {
            if (approvedNicknames.TryGetValue(clientId, out string nickname))
                P2PChatUI.Instance.SetStatus($"{nickname} 님이 입장했습니다.");
            else
                P2PChatUI.Instance.SetStatus("알 수 없는 클라이언트가 입장했습니다.");
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (networkManager.IsServer)
        {
            approvedNicknames.Remove(clientId);
        }

        // 클라이언트 본인 연결 실패 처리
        if (!networkManager.IsServer && clientId == networkManager.LocalClientId)
        {
            if (clientJoinConfirmed)
                return;

            if (!clientJoinPending)
                return;

            clientJoinPending = false;
            StopConnectTimeout();

            string reason = networkManager.DisconnectReason;

            if (string.IsNullOrWhiteSpace(reason))
                P2PChatUI.Instance.SetStatus("해당 IP에 방이 존재하지 않습니다.");
            else
                P2PChatUI.Instance.SetStatus(reason);
        }
    }

    public void NotifyClientJoinedRoom(string connectedHostNickname)
    {
        if (clientJoinConfirmed)
            return;

        clientJoinPending = false;
        clientJoinConfirmed = true;
        StopConnectTimeout();

        P2PChatUI.Instance.SetStatus($"{connectedHostNickname} 님의 방에 입장했습니다.");
    }

    private void RestartConnectTimeout()
    {
        StopConnectTimeout();
        connectTimeoutRoutine = StartCoroutine(CoConnectTimeout());
    }

    private void StopConnectTimeout()
    {
        if (connectTimeoutRoutine != null)
        {
            StopCoroutine(connectTimeoutRoutine);
            connectTimeoutRoutine = null;
        }
    }

    private IEnumerator CoConnectTimeout()
    {
        yield return new WaitForSeconds(connectTimeoutSeconds);

        if (!clientJoinPending || clientJoinConfirmed)
            yield break;

        clientJoinPending = false;

        if (networkManager != null && networkManager.IsListening)
            networkManager.Shutdown();

        P2PChatUI.Instance.SetStatus("해당 IP에 방이 존재하지 않습니다.");
    }

    private void ShutdownIfNeeded()
    {
        StopConnectTimeout();
        clientJoinPending = false;
        clientJoinConfirmed = false;

        if (networkManager != null && networkManager.IsListening)
            networkManager.Shutdown();
    }

    private bool IsValidIpv4(string value)
    {
        return IPAddress.TryParse(value, out IPAddress address) &&
               address.AddressFamily == AddressFamily.InterNetwork;
    }

    private string DecodeNickname(byte[] payload)
    {
        if (payload == null || payload.Length == 0)
            return string.Empty;

        return Encoding.UTF8.GetString(payload).Trim();
    }
}