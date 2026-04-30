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

    public NetworkVariable<FixedString64Bytes> CurrentMonsterStageName =
        new(new FixedString64Bytes("Grassland"), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> IsRoundLocked =
        new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<float> RandomEventChance =
        new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkList<P2PChatEntry> ChatHistory;

    [Header("전투 라운드 잠금")]
    [SerializeField] private bool monitorRoundLock = true;
    [SerializeField, Min(0f)] private float roundLockHoldSeconds = 1.5f;

    [Header("채팅 히스토리")]
    [SerializeField, Min(1)] private int maxChatHistoryEntries = 300;

    [Header("비파괴 설정")]
    [SerializeField] private bool makeDontDestroyOnLoad = true;

    public int MaxChatHistoryEntries => Mathf.Max(1, maxChatHistoryEntries);
    public float RoundLockHoldSeconds => Mathf.Max(0f, roundLockHoldSeconds);
    public string CurrentMonsterStageNameText => CurrentMonsterStageName.Value.ToString();

    private readonly System.Collections.Generic.List<NetworkPlayerState> roundLockPlayers =
        new System.Collections.Generic.List<NetworkPlayerState>();

    private float allCardsSubmittedHeldSeconds;

    private void Awake()
    {
        // 세션 상태는 씬을 넘어 유지되는 단일 네트워크 상태 컨테이너다.
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

    private void OnValidate()
    {
        maxChatHistoryEntries = Mathf.Max(1, maxChatHistoryEntries);
        roundLockHoldSeconds = Mathf.Max(0f, roundLockHoldSeconds);
    }

    private void Update()
    {
        if (!IsServer || !monitorRoundLock || IsRoundLocked.Value)
            return;

        UpdateRoundLockMonitor();
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
        // Host가 세션을 처음 열 때 방장 정보를 서버 기준값으로 초기화한다.
        if (IsServer && HostClientId.Value == ulong.MaxValue)
            HostClientId.Value = NetworkManager.ServerClientId;
    }

    public void ServerSetRoundLocked(bool locked)
    {
        if (!IsServer)
            return;

        if (IsRoundLocked.Value == locked)
            return;

        IsRoundLocked.Value = locked;

        if (locked)
            Debug.Log("[NetworkSessionState] 모든 플레이어가 카드를 제출해 라운드가 잠겼습니다.");
    }

    public void ServerResetRoundLock()
    {
        if (!IsServer)
            return;

        allCardsSubmittedHeldSeconds = 0f;
        ServerSetRoundLocked(false);
    }

    public void ServerSetCurrentMonsterStageName(string stageName)
    {
        if (!IsServer)
            return;

        string normalizedStageName = string.IsNullOrWhiteSpace(stageName)
            ? "Grassland"
            : stageName.Trim();

        CurrentMonsterStageName.Value = new FixedString64Bytes(normalizedStageName);
    }

    public int GetInsuranceAmount(BattleCrystalColor color)
    {
        return color switch
        {
            BattleCrystalColor.Red => InsuranceRed.Value,
            BattleCrystalColor.Blue => InsuranceBlue.Value,
            BattleCrystalColor.Green => InsuranceGreen.Value,
            _ => 0
        };
    }

    public int GetTotalInsuranceCrystals()
    {
        return Mathf.Max(0, InsuranceRed.Value)
            + Mathf.Max(0, InsuranceBlue.Value)
            + Mathf.Max(0, InsuranceGreen.Value);
    }

    public void ServerAddInsuranceCrystals(BattleCrystalColor color, int amount)
    {
        if (!IsServer || amount <= 0)
            return;

        switch (color)
        {
            case BattleCrystalColor.Red:
                InsuranceRed.Value = Mathf.Max(0, InsuranceRed.Value + amount);
                break;

            case BattleCrystalColor.Blue:
                InsuranceBlue.Value = Mathf.Max(0, InsuranceBlue.Value + amount);
                break;

            case BattleCrystalColor.Green:
                InsuranceGreen.Value = Mathf.Max(0, InsuranceGreen.Value + amount);
                break;
        }
    }

    public bool ServerTryRemoveOneInsuranceCrystal(BattleCrystalColor color)
    {
        if (!IsServer || GetInsuranceAmount(color) <= 0)
            return false;

        switch (color)
        {
            case BattleCrystalColor.Red:
                InsuranceRed.Value = Mathf.Max(0, InsuranceRed.Value - 1);
                return true;

            case BattleCrystalColor.Blue:
                InsuranceBlue.Value = Mathf.Max(0, InsuranceBlue.Value - 1);
                return true;

            case BattleCrystalColor.Green:
                InsuranceGreen.Value = Mathf.Max(0, InsuranceGreen.Value - 1);
                return true;
        }

        return false;
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

        // 나레이션은 일반 채팅과 구분되는 시스템 메시지로 히스토리에 저장한다.
        ServerAppendChatHistory(new P2PChatEntry(
            P2PChatEntryType.Narration,
            ulong.MaxValue,
            string.Empty,
            message.Trim()
        ));
    }

    public void ServerAddDivider()
    {
        if (!IsServer) return;

        // 구분선도 채팅 히스토리 이벤트로 동기화해 모든 클라이언트 UI가 같은 순서를 본다.
        ServerAppendChatHistory(new P2PChatEntry(
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

        // 실제 히스토리 변경은 서버만 수행해서 모든 클라이언트의 채팅 순서를 맞춘다.
        ServerAppendChatHistory(new P2PChatEntry(
            P2PChatEntryType.Normal,
            senderClientId,
            nickname.Trim(),
            message.Trim()
        ));
    }

    private void ServerAppendChatHistory(P2PChatEntry entry)
    {
        if (!IsServer || ChatHistory == null)
            return;

        // NetworkList가 무한히 커지지 않도록 오래된 메시지부터 밀어낸다.
        while (ChatHistory.Count >= MaxChatHistoryEntries && ChatHistory.Count > 0)
            ChatHistory.RemoveAt(0);

        ChatHistory.Add(entry);
    }

    private void UpdateRoundLockMonitor()
    {
        if (AreAllRoundPlayersSubmitted())
        {
            allCardsSubmittedHeldSeconds += Time.unscaledDeltaTime;

            if (allCardsSubmittedHeldSeconds >= RoundLockHoldSeconds)
                ServerSetRoundLocked(true);

            return;
        }

        allCardsSubmittedHeldSeconds = 0f;
    }

    private bool AreAllRoundPlayersSubmitted()
    {
        NetworkPlayerRegistry.FillSpawnedPlayers(roundLockPlayers);

        int roundPlayerCount = 0;
        for (int i = 0; i < roundLockPlayers.Count; i++)
        {
            NetworkPlayerState player = roundLockPlayers[i];
            if (!IsRoundPlayer(player))
                continue;

            roundPlayerCount++;

            if (player.SubmittedCard.Value < 1 || player.SubmittedCard.Value > 6)
                return false;
        }

        return roundPlayerCount > 0;
    }

    private bool IsRoundPlayer(NetworkPlayerState player)
    {
        if (player == null || !player.IsSpawned)
            return false;

        if (player.PlayerIndex.Value < 0)
            return false;

        return player.Job.Value != JobType.None;
    }
}
