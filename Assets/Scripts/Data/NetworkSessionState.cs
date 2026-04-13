using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class NetworkSessionState : NetworkBehaviour
{
    public static NetworkSessionState Instance { get; private set; }

    [Header("공용 상태")]
    public NetworkVariable<int> LeaderPlayerIndex =
        new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<ulong> HostClientId =
        new(ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<FixedString128Bytes> HostNickname =
        new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> InsuranceRed =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> InsuranceBlue =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> InsuranceGreen =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<QuestStageType> CurrentQuestStage =
        new(QuestStageType.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<MealStageType> CurrentMealStage =
        new(MealStageType.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> CurrentRound =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> CurrentStage =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<float> RandomEventChance =
        new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkList<P2PChatEntry> ChatHistory;

    [Header("비파괴 설정")]
    [SerializeField] private bool makeDontDestroyOnLoad = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ChatHistory = new NetworkList<P2PChatEntry>();

        if (makeDontDestroyOnLoad)
        {
            if (transform.parent != null)
            {
                Debug.LogError(
                    "[NetworkSessionState] DontDestroyOnLoad를 사용하려면 루트 오브젝트여야 합니다.",
                    this);
            }
            else
            {
                DontDestroyOnLoad(gameObject);
            }
        }
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (ChatHistory != null)
            ChatHistory.Dispose();

        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && HostClientId.Value == ulong.MaxValue)
            HostClientId.Value = NetworkManager.ServerClientId;
    }

    public void ServerSetHostInfo(ulong hostClientId, string hostNickname)
    {
        if (!IsServer) return;

        HostClientId.Value = hostClientId;
        HostNickname.Value = new FixedString128Bytes(hostNickname ?? string.Empty);
    }

    public void ServerAddNarration(string message)
    {
        if (!IsServer || string.IsNullOrWhiteSpace(message)) return;

        ChatHistory.Add(new P2PChatEntry(
            P2PChatEntryType.Narration,
            ulong.MaxValue,
            string.Empty,
            message.Trim()
        ));
    }

    public void ServerAddDivider()
    {
        if (!IsServer) return;

        ChatHistory.Add(new P2PChatEntry(
            P2PChatEntryType.Divider,
            ulong.MaxValue,
            string.Empty,
            string.Empty
        ));
    }

    public void ServerAddNormalChat(ulong senderClientId, string nickname, string message)
    {
        if (!IsServer) return;
        if (string.IsNullOrWhiteSpace(nickname)) return;
        if (string.IsNullOrWhiteSpace(message)) return;

        ChatHistory.Add(new P2PChatEntry(
            P2PChatEntryType.Normal,
            senderClientId,
            nickname.Trim(),
            message.Trim()
        ));
    }
}