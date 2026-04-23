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
    [SerializeField] private int maxParticipants = 5; // 호스트 포함
    [SerializeField] private float connectTimeoutSeconds = 8f;

    private NetworkManager networkManager;
    private UnityTransport transport;

    private readonly Dictionary<ulong, string> approvedNicknames = new();

    private string localNickname;
    private string hostNickname;

    private bool clientJoinPending;
    private bool clientJoinConfirmed;
    private Coroutine connectTimeoutRoutine;

    public string HostNickname => hostNickname;
    public string LocalNickname => localNickname;
    public int MaxParticipants => maxParticipants;

    private void Awake()
    {
        // NetworkManager와 Transport 설정은 방 생성/입장 전 한 번만 잡는다.
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
        networkManager.OnTransportFailure += HandleTransportFailure;
    }

    private void OnDisable()
    {
        if (networkManager == null)
            return;

        networkManager.OnClientConnectedCallback -= HandleClientConnected;
        networkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
        networkManager.OnTransportFailure -= HandleTransportFailure;
    }

    public void PrepareClient(string nickname)
    {
        // 클라이언트가 IP 입력 단계로 넘어가기 전에 접속 페이로드로 보낼 닉네임을 보관한다.
        localNickname = nickname.Trim();
        clientJoinPending = false;
        clientJoinConfirmed = false;
        StopConnectTimeout();
    }

    public void CancelClientJoinPreparation()
    {
        // IP 입력 단계에서 닉네임 수정으로 돌아갈 때 대기 중인 접속 시도를 정리한다.
        ShutdownIfNeeded();
        localNickname = string.Empty;
    }

    public bool StartHost(string nickname)
    {
        // Host는 자기 자신이 서버 역할도 하므로 기존 네트워크 세션을 먼저 정리한다.
        ShutdownIfNeeded();

        localNickname = nickname.Trim();
        hostNickname = localNickname;

        approvedNicknames.Clear();

        transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
        networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(localNickname);

        bool started = networkManager.StartHost();
        if (!started)
        {
            GameStatusMessageRouter.Show("방 생성에 실패했습니다.");
            return false;
        }

        approvedNicknames[NetworkManager.ServerClientId] = localNickname;

        if (NetworkSessionState.Instance != null)
        {
            // 세션 상태에 Host 정보를 저장해 다른 권한 검사들이 같은 기준을 쓰게 한다.
            NetworkSessionState.Instance.ServerSetHostInfo(
                NetworkManager.ServerClientId,
                localNickname
            );
        }

        P2PChatUI.Instance.EnterRoomAsHost();
        GameStatusMessageRouter.Show("방이 생성되었습니다.");

        return true;
    }

    public bool StartClient(string ip)
    {
        string trimmedIp = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim();

        // 현재 UI는 IPv4 직접 입력만 지원하므로 접속 전에 형식을 걸러낸다.
        if (!IsValidIpv4(trimmedIp))
        {
            GameStatusMessageRouter.Show("유효하지 않은 IPv4 주소입니다.");
            return false;
        }

        ShutdownIfNeeded();

        clientJoinPending = true;
        clientJoinConfirmed = false;

        transport.SetConnectionData(trimmedIp, port);
        // ConnectionApproval에서 닉네임 중복/공백 여부를 판정할 수 있게 페이로드로 보낸다.
        networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(localNickname);

        bool started = networkManager.StartClient();
        if (!started)
        {
            clientJoinPending = false;
            GameStatusMessageRouter.Show("입장에 실패했습니다.");
            return false;
        }

        RestartConnectTimeout();
        return true;
    }

    public void NotifyClientJoinedRoom(string connectedHostNickname)
    {
        if (clientJoinConfirmed)
            return;

        // HostNickname 동기화가 도착한 시점을 클라이언트 입장 확정 시점으로 본다.
        clientJoinPending = false;
        clientJoinConfirmed = true;
        hostNickname = connectedHostNickname;

        StopConnectTimeout();

        P2PChatUI.Instance.EnterRoomAsClient();
        GameStatusMessageRouter.Show($"{connectedHostNickname} 님의 방에 입장했습니다.");
    }

    public bool TryGetNickname(ulong clientId, out string nickname)
    {
        if (approvedNicknames.TryGetValue(clientId, out nickname))
            return true;

        if (clientId == NetworkManager.ServerClientId && !string.IsNullOrWhiteSpace(hostNickname))
        {
            nickname = hostNickname;
            return true;
        }

        nickname = string.Empty;
        return false;
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request,
                               NetworkManager.ConnectionApprovalResponse response)
    {
        // 승인 콜백은 Host 쪽에서 실행되며, 여기서 참가 가능 여부를 최종 결정한다.
        string requestedNickname = DecodeNickname(request.Payload);

        if (string.IsNullOrWhiteSpace(requestedNickname))
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Reason = "닉네임이 비어 있습니다.";
            response.Pending = false;
            return;
        }

        if (networkManager.ConnectedClientsIds.Count >= maxParticipants)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Reason = $"방 인원이 가득 찼습니다. (최대 {maxParticipants}명)";
            response.Pending = false;
            return;
        }

        if (IsNicknameAlreadyApproved(requestedNickname))
        {
            // 같은 방 안에서는 닉네임을 고유하게 유지해 채팅/플레이어 표시가 헷갈리지 않게 한다.
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Reason = $"이미 사용 중인 닉네임입니다. ({requestedNickname})";
            response.Pending = false;
            return;
        }

        approvedNicknames[request.ClientNetworkId] = requestedNickname;

        response.Approved = true;
        response.CreatePlayerObject = true;
        response.Pending = false;
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (networkManager.IsServer && clientId != NetworkManager.ServerClientId)
        {
            if (approvedNicknames.TryGetValue(clientId, out string nickname))
                GameStatusMessageRouter.Show($"{nickname} 님이 입장했습니다.");
            else
                GameStatusMessageRouter.Show("알 수 없는 클라이언트가 입장했습니다.");
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (networkManager.IsServer)
            approvedNicknames.Remove(clientId);

        // 입장 확정 전 끊긴 경우는 승인 실패/접속 실패 메시지로 처리한다.
        if (!networkManager.IsServer && clientId == networkManager.LocalClientId)
        {
            StopConnectTimeout();

            if (!clientJoinConfirmed)
            {
                clientJoinPending = false;

                string reason = networkManager.DisconnectReason;
                if (string.IsNullOrWhiteSpace(reason))
                    GameStatusMessageRouter.Show("해당 IP에 방이 존재하지 않습니다.");
                else
                    GameStatusMessageRouter.Show(reason);

                return;
            }

            GameStatusMessageRouter.Show("호스트와의 연결이 종료되었습니다.");
        }
    }

    private void HandleTransportFailure()
    {
        GameStatusMessageRouter.Show("네트워크 오류가 발생했습니다.");
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
        // Transport 연결 실패가 명확히 오지 않는 경우를 대비한 사용자용 타임아웃이다.
        yield return new WaitForSeconds(connectTimeoutSeconds);

        if (!clientJoinPending || clientJoinConfirmed)
            yield break;

        clientJoinPending = false;

        if (networkManager != null && networkManager.IsListening)
            networkManager.Shutdown();

        GameStatusMessageRouter.Show("해당 IP에 방이 존재하지 않습니다.");
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
        return IPAddress.TryParse(value, out IPAddress address)
            && address.AddressFamily == AddressFamily.InterNetwork;
    }

    private bool IsNicknameAlreadyApproved(string requestedNickname)
    {
        // 대소문자 차이만 있는 닉네임도 같은 이름으로 본다.
        foreach (string nickname in approvedNicknames.Values)
        {
            if (string.Equals(nickname?.Trim(), requestedNickname, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private string DecodeNickname(byte[] payload)
    {
        if (payload == null || payload.Length == 0)
            return string.Empty;

        return Encoding.UTF8.GetString(payload).Trim();
    }
}
