using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class BattleFlowController : NetworkBehaviour
{
    public static BattleFlowController Instance { get; private set; }

    private const string FallbackStageName = "Grassland";

    [Header("References")]
    [SerializeField] private MonsterSpawner monsterSpawner;
    [SerializeField] private MonsterTable monsterTable;

    [Header("Scene Startup")]
    [SerializeField] private bool autoStartBattleOnSceneEntry = true;
    [SerializeField, Min(0f)] private float autoStartBattleDelay = 0.25f;

    [Header("Encounter Sequence")]
    [SerializeField, Min(1)] private int monstersPerBattle = 4;
    [SerializeField, Min(0f)] private float nextMonsterDelay = 0.5f;
    [SerializeField] private string mapSelectSceneName = "MapSelectScene";

    [Header("Monster Selection")]
    [SerializeField] private string defaultMonsterStageName = FallbackStageName;
    [SerializeField, Range(0f, 1f)] private float currentStageTableChance = 0.95f;

    [Header("Round Timing")]
    [SerializeField, Min(0f)] private float resolveDelayAfterLock = 1f;
    [SerializeField, Min(0f)] private float attackAnimationDuration = 0.6f;
    [SerializeField, Min(0f)] private float missAnimationDuration = 0.6f;
    [SerializeField, Min(0f)] private float delayAfterEachAction = 0f;
    [SerializeField, Min(0f)] private float resultAnimationDelay = 0.4f;

    [Header("Animation Triggers")]
    [SerializeField] private string attackTriggerName = "Hit";
    [SerializeField] private string missTriggerName = "Hit";
    [SerializeField] private string victoryTriggerName = "Victory";
    [SerializeField] private string defeatTriggerName = "Defeat";

    private readonly Dictionary<string, HashSet<string>> appearedMonsterIdsByStage =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

    private readonly List<NetworkPlayerState> playerBuffer = new List<NetworkPlayerState>();
    private readonly List<BattleRoundParticipant> participantBuffer = new List<BattleRoundParticipant>();
    private readonly List<BattleRoundParticipant> attackOrderBuffer = new List<BattleRoundParticipant>();
    private readonly List<BattleRoundParticipant> rewardRankBuffer = new List<BattleRoundParticipant>();
    private readonly List<BattleRoundParticipant> insuranceRankBuffer = new List<BattleRoundParticipant>();
    private readonly List<IBattleAttackPowerModifier> attackPowerModifiers = new List<IBattleAttackPowerModifier>();
    private readonly List<IBattleRewardRankModifier> rewardRankModifiers = new List<IBattleRewardRankModifier>();
    private readonly List<BattleCrystalColor> colorBuffer = new List<BattleCrystalColor>();
    private readonly HashSet<int> clientKnightShiftBlameTargets = new HashSet<int>();
    private readonly HashSet<int> pendingKnightShiftBlameTargets = new HashSet<int>();
    private readonly HashSet<int> clientDefenderTargetChoices = new HashSet<int>();
    private readonly HashSet<int> pendingDefenderTargetChoices = new HashSet<int>();

    private RoundContext currentRoundContext;
    private MageRoundState currentMageRoundState;
    private ArcherRoundState currentArcherRoundState;
    private FighterRoundState currentFighterRoundState;
    private SaintRoundState currentSaintRoundState;
    private KnightRoundState currentKnightRoundState;
    private readonly BerserkerStageState berserkerStageState = new BerserkerStageState();
    private Coroutine resolveCoroutine;
    private Coroutine autoStartCoroutine;
    private bool resolvingRound;
    private bool lastRoundLocked;
    private bool autoStartBattleAttempted;
    private int spawnedMonsterCount;
    private int nextChoiceRequestId = 1;
    private int pendingChoiceRequestId;
    private ulong pendingChoiceOwnerClientId = ulong.MaxValue;
    private bool pendingChoiceRedEnabled;
    private bool pendingChoiceGreenEnabled;
    private bool pendingChoiceBlueEnabled;
    private bool pendingChoiceResolved;
    private BattleCrystalColor pendingChoiceColor;
    private int pendingKnightShiftBlameRequestId;
    private ulong pendingKnightShiftBlameOwnerClientId = ulong.MaxValue;
    private bool pendingKnightShiftBlameTargetResolved;
    private int pendingKnightShiftBlameTargetIndex = -1;
    private bool clientKnightShiftBlameSelectionActive;
    private int clientKnightShiftBlameRequestId;
    private int pendingDefenderTargetRequestId;
    private ulong pendingDefenderTargetOwnerClientId = ulong.MaxValue;
    private bool pendingDefenderTargetResolved;
    private int pendingDefenderTargetIndex = -1;
    private bool pendingDefenderLoanSelection;
    private bool clientDefenderTargetSelectionActive;
    private int clientDefenderTargetRequestId;

    public bool IsResolvingRound => resolvingRound || resolveCoroutine != null;
    public float ResolveDelayAfterLock => Mathf.Max(0f, resolveDelayAfterLock);
    public float AttackAnimationDuration => Mathf.Max(0f, attackAnimationDuration);
    public float MissAnimationDuration => Mathf.Max(0f, missAnimationDuration);
    public float DelayAfterEachAction => Mathf.Max(0f, delayAfterEachAction);
    public float AttackStepDelay => AttackAnimationDuration + DelayAfterEachAction;
    public int MonstersPerBattle => Mathf.Max(1, monstersPerBattle);
    public int CurrentEncounterNumber => Mathf.Clamp(spawnedMonsterCount, 0, MonstersPerBattle);
    public MonsterData CurrentMonsterData => monsterSpawner != null ? monsterSpawner.CurrentMonsterData : null;

    public bool TryRequestKnightShiftBlameTarget(int playerIndex)
    {
        if (!clientKnightShiftBlameSelectionActive)
            return false;

        if (!clientKnightShiftBlameTargets.Contains(playerIndex))
            return false;

        clientKnightShiftBlameSelectionActive = false;
        if (IsSpawned)
        {
            RequestKnightShiftBlameTargetRpc(clientKnightShiftBlameRequestId, playerIndex);
        }
        else if (clientKnightShiftBlameRequestId == pendingKnightShiftBlameRequestId &&
                 pendingKnightShiftBlameTargets.Contains(playerIndex))
        {
            pendingKnightShiftBlameTargetIndex = playerIndex;
            pendingKnightShiftBlameTargetResolved = true;
        }

        return true;
    }

    public bool TryRequestDefenderTarget(int playerIndex)
    {
        if (!clientDefenderTargetSelectionActive)
            return false;

        if (!clientDefenderTargetChoices.Contains(playerIndex))
            return false;

        clientDefenderTargetSelectionActive = false;
        if (IsSpawned)
        {
            RequestDefenderTargetRpc(clientDefenderTargetRequestId, playerIndex);
        }
        else if (clientDefenderTargetRequestId == pendingDefenderTargetRequestId &&
                 pendingDefenderTargetChoices.Contains(playerIndex))
        {
            pendingDefenderTargetIndex = playerIndex;
            pendingDefenderTargetResolved = true;
            if (pendingDefenderLoanSelection &&
                NetworkPlayerRegistry.TryGetByPlayerIndex(playerIndex, out NetworkPlayerState target))
            {
                DefenderAbility.SetCustomer(DefenderAbility.FindDefender(), target);
            }
        }

        return true;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("BattleFlowController 중복 인스턴스가 감지되었습니다.", this);
            return;
        }

        Instance = this;
        CacheReferences();
        CacheModifierComponents();
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        base.OnDestroy();
    }

    private void Start()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening)
            return;

        QueueAutoStartBattle();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            QueueAutoStartBattle();
    }

    private void OnValidate()
    {
        currentStageTableChance = Mathf.Clamp01(currentStageTableChance);
        monstersPerBattle = Mathf.Max(1, monstersPerBattle);
        resolveDelayAfterLock = Mathf.Max(0f, resolveDelayAfterLock);
        autoStartBattleDelay = Mathf.Max(0f, autoStartBattleDelay);
        nextMonsterDelay = Mathf.Max(0f, nextMonsterDelay);
        attackAnimationDuration = Mathf.Max(0f, attackAnimationDuration);
        missAnimationDuration = Mathf.Max(0f, missAnimationDuration);
        delayAfterEachAction = Mathf.Max(0f, delayAfterEachAction);
        resultAnimationDelay = Mathf.Max(0f, resultAnimationDelay);

        if (string.IsNullOrWhiteSpace(defaultMonsterStageName))
            defaultMonsterStageName = FallbackStageName;
    }

    private void Update()
    {
        if (!CanRunServerFlow())
            return;

        NetworkSessionState session = NetworkSessionState.Instance;
        if (session == null)
            return;

        bool roundLocked = session.IsRoundLocked.Value;
        if (roundLocked && !lastRoundLocked)
            QueueResolveAfterDelay();

        lastRoundLocked = roundLocked;
    }

    public void RegisterAttackPowerModifier(IBattleAttackPowerModifier modifier)
    {
        if (modifier == null || attackPowerModifiers.Contains(modifier))
            return;

        attackPowerModifiers.Add(modifier);
    }

    public void UnregisterAttackPowerModifier(IBattleAttackPowerModifier modifier)
    {
        if (modifier == null)
            return;

        attackPowerModifiers.Remove(modifier);
    }

    public void RegisterRewardRankModifier(IBattleRewardRankModifier modifier)
    {
        if (modifier == null || rewardRankModifiers.Contains(modifier))
            return;

        rewardRankModifiers.Add(modifier);
    }

    public void UnregisterRewardRankModifier(IBattleRewardRankModifier modifier)
    {
        if (modifier == null)
            return;

        rewardRankModifiers.Remove(modifier);
    }

    public bool ServerStartBattle()
    {
        if (!CanRunServerFlow())
            return false;

        CacheReferences();
        EnsureSessionStageName();

        if (spawnedMonsterCount >= MonstersPerBattle)
        {
            Debug.Log("BattleFlowController: 모든 몬스터 전투가 끝났습니다.");
            return false;
        }

        if (monsterSpawner == null || monsterTable == null)
        {
            Debug.LogWarning("BattleFlowController: MonsterSpawner 또는 MonsterTable 참조가 없습니다.");
            return false;
        }

        EnsureMonsterTableLoaded();
        MonsterData selectedMonster = SelectNextMonster();
        if (selectedMonster == null)
        {
            Debug.LogWarning("BattleFlowController: 소환 가능한 몬스터 후보가 없습니다.");
            return false;
        }

        if (!monsterSpawner.SpawnMonster(selectedMonster))
            return false;

        NetworkPlayerRegistry.FillSpawnedPlayers(playerBuffer);
        if (spawnedMonsterCount == 0)
            DefenderAbility.ResetStageDebtBlocks(playerBuffer);

        DefenderAbility.ApplyPendingDebtAtRoundStart(playerBuffer);
        spawnedMonsterCount++;
        MarkMonsterAppeared(selectedMonster);
        ClearSubmittedCards();
        currentFighterRoundState = FighterAbility.GenerateForRound(FindPlayerByJob(JobType.Fighter));
        currentSaintRoundState = new SaintRoundState();
        currentKnightRoundState = new KnightRoundState();
        NetworkSessionState.Instance?.ServerSetCurrentStage(spawnedMonsterCount);
        NetworkSessionState.Instance?.ServerResetRoundLock();

        Debug.Log($"BattleFlowController: 전투 시작 / 몬스터={selectedMonster.monster_name}({selectedMonster.id})");
        return true;
    }

    public bool ServerResolveCurrentRoundNow()
    {
        if (!CanRunServerFlow() || resolvingRound)
            return false;

        if (resolveCoroutine != null)
        {
            StopCoroutine(resolveCoroutine);
            resolveCoroutine = null;
        }

        resolveCoroutine = StartCoroutine(ResolveAfterDelay(0f, true));
        return true;
    }

    public bool ServerTryUseSaintRevelation(NetworkPlayerState saint)
    {
        if (!CanRunServerFlow())
            return false;

        if (NetworkSessionState.Instance != null && NetworkSessionState.Instance.IsRoundLocked.Value)
            return false;

        currentSaintRoundState ??= new SaintRoundState();
        bool used = SaintAbility.TryUseRevelation(saint, currentSaintRoundState);
        if (used)
            NetworkSessionState.Instance?.ServerAddNarration("Saint used Revelation.");

        return used;
    }

    public bool ServerTryBeginDefenderLoan(NetworkPlayerState defender)
    {
        if (!CanRunServerFlow())
            return false;

        if (NetworkSessionState.Instance != null && NetworkSessionState.Instance.IsRoundLocked.Value)
            return false;

        if (!DefenderAbility.TryUseLoanCard(defender))
            return false;

        pendingDefenderTargetChoices.Clear();
        foreach (NetworkPlayerState player in NetworkPlayerRegistry.GetSpawnedPlayers())
        {
            if (player == null || player == defender || !IsRoundPlayer(player))
                continue;

            pendingDefenderTargetChoices.Add(player.PlayerIndex.Value);
        }

        if (pendingDefenderTargetChoices.Count == 0)
            return false;

        pendingDefenderTargetRequestId = nextChoiceRequestId++;
        pendingDefenderTargetOwnerClientId = defender.OwnerClientId;
        pendingDefenderTargetResolved = false;
        pendingDefenderTargetIndex = -1;
        pendingDefenderLoanSelection = true;

        int[] targets = new int[pendingDefenderTargetChoices.Count];
        pendingDefenderTargetChoices.CopyTo(targets);

        if (IsSpawned)
            BeginDefenderTargetSelectionRpc(defender.OwnerClientId, pendingDefenderTargetRequestId, targets);
        else
            BeginLocalDefenderTargetSelection(pendingDefenderTargetRequestId, targets);

        NetworkSessionState.Instance?.ServerAddNarration("Defender used Loan.");
        return true;
    }

    public void ServerResetAppearedMonsterHistory()
    {
        if (!CanRunServerFlow())
            return;

        appearedMonsterIdsByStage.Clear();
        Debug.Log("BattleFlowController: 몬스터 등장 기록을 초기화했습니다.");
    }

    public void ServerSetCurrentMonsterStageName(string stageName)
    {
        if (!CanRunServerFlow())
            return;

        NetworkSessionState.Instance?.ServerSetCurrentMonsterStageName(stageName);
    }

    private void QueueResolveAfterDelay()
    {
        if (resolvingRound || resolveCoroutine != null)
            return;

        resolveCoroutine = StartCoroutine(ResolveAfterDelay(ResolveDelayAfterLock, false));
    }

    private void QueueAutoStartBattle()
    {
        if (!autoStartBattleOnSceneEntry || autoStartBattleAttempted || autoStartCoroutine != null)
            return;

        autoStartCoroutine = StartCoroutine(AutoStartBattleAfterDelay());
    }

    private IEnumerator AutoStartBattleAfterDelay()
    {
        if (autoStartBattleDelay > 0f)
            yield return new WaitForSeconds(autoStartBattleDelay);
        else
            yield return null;

        autoStartCoroutine = null;
        autoStartBattleAttempted = true;

        CacheReferences();
        if (monsterSpawner != null && monsterSpawner.HasSpawnedMonster)
            yield break;

        ServerStartBattle();
    }

    private IEnumerator ResolveAfterDelay(float delay, bool force)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        resolveCoroutine = null;

        NetworkSessionState session = NetworkSessionState.Instance;
        if (!force && (session == null || !session.IsRoundLocked.Value))
            yield break;

        yield return ResolveCurrentRound();
    }

    private IEnumerator ResolveCurrentRound()
    {
        resolvingRound = true;
        CacheReferences();
        CacheModifierComponents();

        if (monsterSpawner == null || !monsterSpawner.TryGetCurrentMonsterState(out MonsterBattleState monsterState))
        {
            Debug.LogWarning("BattleFlowController: 판정할 현재 몬스터가 없습니다.");
            FinishRoundResolution(false);
            yield break;
        }

        FillParticipants(participantBuffer);
        if (participantBuffer.Count == 0)
        {
            Debug.LogWarning("BattleFlowController: 제출된 카드가 없어 라운드 판정을 중단했습니다.");
            FinishRoundResolution(false);
            yield break;
        }

        NetworkSessionState session = NetworkSessionState.Instance;
        ApplyMissFlags(participantBuffer);
        currentRoundContext = BattleRoundResolver.CreateRoundContext(
            participantBuffer,
            monsterState,
            session != null ? session.CurrentRound.Value : 0);
        BattleRoundResolver.ApplyDuplicateCancellations(participantBuffer, currentRoundContext);
        SaintAbility.ResolveRevealResult(currentSaintRoundState, currentRoundContext);
        currentArcherRoundState = ArcherAbility.PrepareRound(participantBuffer);
        currentMageRoundState = MageAbility.ResolveDestructionSpell(
            currentRoundContext,
            participantBuffer,
            monsterSpawner,
            monsterState);
        ApplyAttackPowerModifiers(participantBuffer, monsterSpawner.CurrentMonsterData, monsterState);
        BuildAttackOrder(participantBuffer, attackOrderBuffer);

        for (int i = 0; i < attackOrderBuffer.Count; i++)
        {
            BattleRoundParticipant participant = attackOrderBuffer[i];
            bool attackCancelled = participant.IsMiss || participant.AttackCancelled || participant.DirectAttackSkipped;
            if (!attackCancelled)
                ArcherAbility.TryCancelKillingDamage(currentArcherRoundState, participant, monsterState, currentRoundContext);

            attackCancelled = participant.IsMiss || participant.AttackCancelled || participant.DirectAttackSkipped;
            PlayStandingTrigger(participant.PlayerIndex, attackCancelled ? missTriggerName : attackTriggerName);

            if (!attackCancelled && participant.AttackPower > 0 && !monsterState.IsDead)
            {
                int monsterHpBefore = monsterState.CurrentHp;
                participant.AppliedDamage = monsterSpawner.ApplyDamageToCurrentMonster(participant.AttackPower, true);
                BattleRoundResolver.RecordDamage(
                    currentRoundContext,
                    participant,
                    monsterHpBefore,
                    monsterState.CurrentHp,
                    participant.AppliedDamage);

                if (participant.AppliedDamage)
                {
                    BerserkerAbility.ResolveAfterSuccessfulAttack(
                        berserkerStageState,
                        participant,
                        participant.AttackPower);

                    ArcherAbility.TryApplySupportDamage(
                        currentArcherRoundState,
                        participantBuffer,
                        participant,
                        monsterSpawner,
                        monsterState,
                        currentRoundContext);
                }
            }

            float actionWait = GetActionWaitSeconds(participant);
            if (actionWait > 0f)
                yield return new WaitForSeconds(actionWait);
        }

        BattleRoundResolver.FinalizeAttackFlags(currentRoundContext, participantBuffer);
        currentRoundContext.MonsterKilled = monsterState.IsDead;
        currentRoundContext.MonsterHpAfter = monsterState.CurrentHp;
        KnightAbility.ResolvePostAttack(currentKnightRoundState, currentRoundContext, participantBuffer);
        FighterAbility.ApplyPostAttackRewardModifier(currentFighterRoundState, monsterState, participantBuffer);
        BuildRewardRanks(participantBuffer, rewardRankBuffer);
        BuildInsuranceRanks(participantBuffer, insuranceRankBuffer);

        if (monsterState.IsDead)
        {
            GrantMonsterRewards(rewardRankBuffer, monsterSpawner.CurrentMonsterData);
            KnightAbility.GrantCalculatedVictoryReward(
                currentKnightRoundState,
                NetworkSessionState.Instance,
                KnightAbility.FindKnight(participantBuffer)?.Player);
            yield return ResolveSaintAmenRewardFlow(SaintAbility.FindSaint(participantBuffer)?.Player);
            yield return DistributeInsuranceCrystals(insuranceRankBuffer);
            FighterAbility.GrantLuckyReward(currentFighterRoundState, participantBuffer);
            PlayResultForAll(victoryTriggerName);

            if (resultAnimationDelay > 0f)
                yield return new WaitForSeconds(resultAnimationDelay);

            monsterSpawner.PlayCurrentMonsterPendingDeathAnimation();
        }
        else
        {
            MonsterAttackContext monsterAttackContext = BuildMonsterAttackContext(participantBuffer);
            List<BattleRoundParticipant> penaltyTargets = ApplyFailurePenalty(monsterAttackContext.FinalTargets);
            DefenderAbility.ResolveUnprotectedCustomerHits(penaltyTargets);
            PlayResultForParticipants(penaltyTargets, defeatTriggerName);

            if (resultAnimationDelay > 0f)
                yield return new WaitForSeconds(resultAnimationDelay);

            yield return ResolveKnightShiftBlameFlow();
        }

        FinishRoundResolution(true);
        yield return QueueNextMonsterEncounter();
    }

    private IEnumerator ResolveSaintAmenRewardFlow(NetworkPlayerState saint)
    {
        if (currentSaintRoundState == null || !currentSaintRoundState.AmenTriggered || saint == null)
            yield break;

        NetworkSessionState session = NetworkSessionState.Instance;
        if (session == null)
            yield break;

        if (!BattleGimmickDebugSettings.SaintAmenEnabled)
            yield break;

        if (session.GetTotalInsuranceCrystals() > 0)
            SaintAbility.ResolveAmenReward(currentSaintRoundState, session, saint);
    }

    private IEnumerator ResolveKnightShiftBlameFlow()
    {
        if (currentKnightRoundState == null || !currentKnightRoundState.ShiftBlameAvailable || currentKnightRoundState.ShiftBlameUsed)
            yield break;

        if (!BattleGimmickDebugSettings.KnightShiftBlameEnabled)
            yield break;

        NetworkPlayerState knight = FindPlayerByJob(JobType.Hero);
        if (knight == null || currentKnightRoundState.ShiftBlameTargets.Count == 0)
            yield break;

        pendingKnightShiftBlameTargets.Clear();
        for (int i = 0; i < currentKnightRoundState.ShiftBlameTargets.Count; i++)
        {
            int targetIndex = currentKnightRoundState.ShiftBlameTargets[i];
            if (targetIndex == knight.PlayerIndex.Value)
                continue;

            if (NetworkPlayerRegistry.TryGetByPlayerIndex(targetIndex, out NetworkPlayerState target) && target != null)
                pendingKnightShiftBlameTargets.Add(targetIndex);
        }

        if (pendingKnightShiftBlameTargets.Count == 0)
            yield break;

        pendingKnightShiftBlameRequestId = nextChoiceRequestId++;
        pendingKnightShiftBlameOwnerClientId = knight.OwnerClientId;
        pendingKnightShiftBlameTargetResolved = false;
        pendingKnightShiftBlameTargetIndex = -1;

        int[] targets = new int[pendingKnightShiftBlameTargets.Count];
        pendingKnightShiftBlameTargets.CopyTo(targets);

        if (IsSpawned)
            BeginKnightShiftBlameTargetSelectionRpc(knight.OwnerClientId, pendingKnightShiftBlameRequestId, targets);
        else
            BeginLocalKnightShiftBlameTargetSelection(pendingKnightShiftBlameRequestId, targets);

        float timeoutAt = Time.unscaledTime + 30f;
        while (!pendingKnightShiftBlameTargetResolved && Time.unscaledTime < timeoutAt)
            yield return null;

        if (IsSpawned)
            EndKnightShiftBlameTargetSelectionRpc(knight.OwnerClientId);
        else
            EndLocalKnightShiftBlameTargetSelection();

        if (!pendingKnightShiftBlameTargetResolved ||
            !NetworkPlayerRegistry.TryGetByPlayerIndex(pendingKnightShiftBlameTargetIndex, out NetworkPlayerState targetPlayer) ||
            targetPlayer == null)
        {
            yield break;
        }

        bool redEnabled = targetPlayer.CrystalRed.Value > 0;
        bool greenEnabled = targetPlayer.CrystalGreen.Value > 0;
        bool blueEnabled = targetPlayer.CrystalBlue.Value > 0;

        string targetName = targetPlayer.DisplayNickname.Value.ToString();
        if (string.IsNullOrWhiteSpace(targetName))
            targetName = $"P{targetPlayer.PlayerIndex.Value + 1}";

        yield return RequestCrystalChoice(
            knight,
            $"책임 전가: {targetName}에게서 뺏기",
            redEnabled,
            greenEnabled,
            blueEnabled,
            selected =>
            {
                if (TryMoveOneCrystal(targetPlayer, knight, selected))
                    currentKnightRoundState.ShiftBlameUsed = true;
            });
    }

    private IEnumerator ResolveDefenderSettlementFlow()
    {
        if (!BattleGimmickDebugSettings.DefenderSettlementEnabled)
            yield break;

        NetworkPlayerState defender = DefenderAbility.FindDefender();
        if (defender == null)
            yield break;

        List<NetworkPlayerState> candidates = DefenderAbility.GetHighestDebtCustomers();
        if (candidates.Count == 0)
            yield break;

        NetworkPlayerState selected = candidates[0];
        if (candidates.Count > 1)
        {
            pendingDefenderTargetChoices.Clear();
            for (int i = 0; i < candidates.Count; i++)
                pendingDefenderTargetChoices.Add(candidates[i].PlayerIndex.Value);

            pendingDefenderTargetRequestId = nextChoiceRequestId++;
            pendingDefenderTargetOwnerClientId = defender.OwnerClientId;
            pendingDefenderTargetResolved = false;
            pendingDefenderTargetIndex = -1;
            pendingDefenderLoanSelection = false;

            int[] targets = new int[pendingDefenderTargetChoices.Count];
            pendingDefenderTargetChoices.CopyTo(targets);

            if (IsSpawned)
                BeginDefenderTargetSelectionRpc(defender.OwnerClientId, pendingDefenderTargetRequestId, targets);
            else
                BeginLocalDefenderTargetSelection(pendingDefenderTargetRequestId, targets);

            float timeoutAt = Time.unscaledTime + 30f;
            while (!pendingDefenderTargetResolved && Time.unscaledTime < timeoutAt)
                yield return null;

            if (IsSpawned)
                EndDefenderTargetSelectionRpc(defender.OwnerClientId);
            else
                EndLocalDefenderTargetSelection();

            if (pendingDefenderTargetResolved &&
                NetworkPlayerRegistry.TryGetByPlayerIndex(pendingDefenderTargetIndex, out NetworkPlayerState chosen) &&
                chosen != null &&
                candidates.Contains(chosen))
            {
                selected = chosen;
            }
        }

        DefenderAbility.SettleCustomer(defender, selected);
    }

    private IEnumerator RequestCrystalChoice(
        NetworkPlayerState player,
        string title,
        bool redEnabled,
        bool greenEnabled,
        bool blueEnabled,
        Action<BattleCrystalColor> onSelected)
    {
        if (player == null)
            yield break;

        int requestId = nextChoiceRequestId++;
        bool anyEnabled = redEnabled || greenEnabled || blueEnabled;
        pendingChoiceRequestId = requestId;
        pendingChoiceOwnerClientId = player.OwnerClientId;
        pendingChoiceRedEnabled = redEnabled;
        pendingChoiceGreenEnabled = greenEnabled;
        pendingChoiceBlueEnabled = blueEnabled;
        pendingChoiceResolved = false;
        pendingChoiceColor = BattleCrystalColor.Red;

        if (IsSpawned)
            ShowCrystalChoiceRpc(player.OwnerClientId, requestId, new FixedString64Bytes(title ?? string.Empty), redEnabled, greenEnabled, blueEnabled);
        else
            ShowLocalCrystalChoice(requestId, title, redEnabled, greenEnabled, blueEnabled);

        if (!anyEnabled)
        {
            yield return new WaitForSecondsRealtime(1.5f);

            if (IsSpawned)
                HideCrystalChoiceRpc(player.OwnerClientId);
            else
                BattleCrystalChoiceUI.Hide();

            ClearPendingCrystalChoice(requestId);
            yield break;
        }

        float timeoutAt = Time.unscaledTime + 30f;
        while (!pendingChoiceResolved && Time.unscaledTime < timeoutAt)
            yield return null;

        if (pendingChoiceResolved && IsChoiceColorEnabled(pendingChoiceColor))
            onSelected?.Invoke(pendingChoiceColor);

        if (IsSpawned)
            HideCrystalChoiceRpc(player.OwnerClientId);
        else
            BattleCrystalChoiceUI.Hide();

        ClearPendingCrystalChoice(requestId);
    }

    private void FinishRoundResolution(bool clearSubmittedCards)
    {
        currentRoundContext = null;
        currentMageRoundState = null;
        currentArcherRoundState = null;
        currentFighterRoundState = null;
        currentSaintRoundState = null;
        currentKnightRoundState = null;

        if (clearSubmittedCards)
            ClearSubmittedCards();

        NetworkSessionState.Instance?.ServerResetRoundLock();
        resolvingRound = false;
    }

    private IEnumerator QueueNextMonsterEncounter()
    {
        if (nextMonsterDelay > 0f)
            yield return new WaitForSeconds(nextMonsterDelay);

        CacheReferences();
        monsterSpawner?.RemoveCurrentMonster();

        if (spawnedMonsterCount >= MonstersPerBattle)
        {
            Debug.Log("BattleFlowController: 전투 시퀀스 종료");
            yield return ResolveDefenderSettlementFlow();
            BerserkerAbility.ResetStage(berserkerStageState);
            LoadMapSelectScene();
            yield break;
        }

        ServerStartBattle();
    }

    private void LoadMapSelectScene()
    {
        if (string.IsNullOrWhiteSpace(mapSelectSceneName))
            return;

        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening)
        {
            if (!manager.IsServer || !manager.NetworkConfig.EnableSceneManagement)
                return;

            NetworkSessionState.Instance?.ServerSetMapSelectionSeed(UnityEngine.Random.Range(1, int.MaxValue));

            var status = manager.SceneManager.LoadScene(mapSelectSceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
                Debug.LogWarning($"BattleFlowController: Map select scene load failed. ({status})");

            return;
        }

        SceneManager.LoadSceneAsync(mapSelectSceneName);
    }
    private void CacheReferences()
    {
        if (monsterSpawner == null)
            monsterSpawner = FindAnyObjectByType<MonsterSpawner>();

        if (monsterTable == null)
        {
            if (monsterSpawner != null)
                monsterTable = monsterSpawner.MonsterTable;

            if (monsterTable == null)
                monsterTable = FindAnyObjectByType<MonsterTable>();
        }
    }

    private void CacheModifierComponents()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            if (behaviour is IBattleAttackPowerModifier attackModifier)
                RegisterAttackPowerModifier(attackModifier);

            if (behaviour is IBattleRewardRankModifier rankModifier)
                RegisterRewardRankModifier(rankModifier);
        }
    }

    private bool CanRunServerFlow()
    {
        return !IsSpawned || IsServer;
    }

    private void EnsureSessionStageName()
    {
        NetworkSessionState session = NetworkSessionState.Instance;
        if (session == null)
            return;

        if (string.IsNullOrWhiteSpace(session.CurrentMonsterStageNameText))
            session.ServerSetCurrentMonsterStageName(defaultMonsterStageName);
    }

    private void EnsureMonsterTableLoaded()
    {
        if (monsterTable == null)
            return;

        if (monsterTable.Monsters == null || monsterTable.Monsters.Count == 0)
            monsterTable.LoadMonsterTable();
    }

    private MonsterData SelectNextMonster()
    {
        string currentStage = GetCurrentStageName();
        string selectedStage = UnityEngine.Random.value <= currentStageTableChance
            ? currentStage
            : PickRandomOtherStage(currentStage);

        MonsterData selected = PickRandomMonsterFromStage(selectedStage);
        if (selected != null)
            return selected;

        // 다른 스테이지 5%가 프리펩 미구현 등으로 비면 현재 스테이지 후보로 되돌린다.
        if (!string.Equals(selectedStage, currentStage, StringComparison.OrdinalIgnoreCase))
            selected = PickRandomMonsterFromStage(currentStage);

        return selected;
    }

    private string GetCurrentStageName()
    {
        NetworkSessionState session = NetworkSessionState.Instance;
        string stageName = session != null ? session.CurrentMonsterStageNameText : defaultMonsterStageName;

        if (string.IsNullOrWhiteSpace(stageName))
            stageName = defaultMonsterStageName;

        return string.IsNullOrWhiteSpace(stageName) ? FallbackStageName : stageName.Trim();
    }

    private string PickRandomOtherStage(string currentStage)
    {
        List<string> stages = new List<string>();

        IReadOnlyList<MonsterData> monsters = monsterTable != null ? monsterTable.Monsters : null;
        if (monsters != null)
        {
            for (int i = 0; i < monsters.Count; i++)
            {
                MonsterData monster = monsters[i];
                if (monster == null || string.IsNullOrWhiteSpace(monster.spawn_stage))
                    continue;

                string stage = monster.spawn_stage.Trim();
                if (string.Equals(stage, currentStage, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!stages.Exists(existing => string.Equals(existing, stage, StringComparison.OrdinalIgnoreCase)))
                    stages.Add(stage);
            }
        }

        if (stages.Count == 0)
            return currentStage;

        return stages[UnityEngine.Random.Range(0, stages.Count)];
    }

    private MonsterData PickRandomMonsterFromStage(string stageName)
    {
        List<MonsterData> candidates = new List<MonsterData>();
        FillSpawnableMonsterCandidates(stageName, true, candidates);

        if (candidates.Count == 0)
        {
            ClearAppearedMonsterIds(stageName);
            FillSpawnableMonsterCandidates(stageName, false, candidates);
        }

        if (candidates.Count == 0)
            return null;

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private void FillSpawnableMonsterCandidates(string stageName, bool excludeAppeared, List<MonsterData> target)
    {
        target.Clear();

        IReadOnlyList<MonsterData> monsters = monsterTable != null ? monsterTable.Monsters : null;
        if (monsters == null)
            return;

        HashSet<string> appearedIds = GetAppearedMonsterIds(stageName);
        for (int i = 0; i < monsters.Count; i++)
        {
            MonsterData monster = monsters[i];
            if (monster == null)
                continue;

            if (!string.Equals(monster.spawn_stage, stageName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (excludeAppeared && !string.IsNullOrWhiteSpace(monster.id) && appearedIds.Contains(monster.id))
                continue;

            if (monsterSpawner != null && !monsterSpawner.CanSpawnMonster(monster))
                continue;

            target.Add(monster);
        }
    }

    private HashSet<string> GetAppearedMonsterIds(string stageName)
    {
        string key = string.IsNullOrWhiteSpace(stageName) ? FallbackStageName : stageName.Trim();
        if (!appearedMonsterIdsByStage.TryGetValue(key, out HashSet<string> appearedIds))
        {
            appearedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            appearedMonsterIdsByStage.Add(key, appearedIds);
        }

        return appearedIds;
    }

    private void ClearAppearedMonsterIds(string stageName)
    {
        GetAppearedMonsterIds(stageName).Clear();
    }

    private void MarkMonsterAppeared(MonsterData monsterData)
    {
        if (monsterData == null || string.IsNullOrWhiteSpace(monsterData.id))
            return;

        GetAppearedMonsterIds(monsterData.spawn_stage).Add(monsterData.id);
    }

    private void FillParticipants(List<BattleRoundParticipant> target)
    {
        target.Clear();
        NetworkPlayerRegistry.FillSpawnedPlayers(playerBuffer);
        playerBuffer.Sort(ComparePlayerIndex);

        for (int i = 0; i < playerBuffer.Count; i++)
        {
            NetworkPlayerState player = playerBuffer[i];
            if (!IsRoundPlayer(player))
                continue;

            int submittedCard = player.SubmittedCard.Value;
            if (submittedCard < 1 || submittedCard > 6)
                continue;

            target.Add(new BattleRoundParticipant(player, submittedCard));
        }
    }

    private int ComparePlayerIndex(NetworkPlayerState a, NetworkPlayerState b)
    {
        return a.PlayerIndex.Value.CompareTo(b.PlayerIndex.Value);
    }

    private bool IsRoundPlayer(NetworkPlayerState player)
    {
        if (player == null || !player.IsSpawned)
            return false;

        if (player.PlayerIndex.Value < 0)
            return false;

        return player.Job.Value != JobType.None;
    }

    private void ApplyMissFlags(List<BattleRoundParticipant> participants)
    {
        int[] cardCounts = new int[7];

        for (int i = 0; i < participants.Count; i++)
        {
            int cardValue = participants[i].CardValue;
            if (cardValue >= 1 && cardValue <= 6)
                cardCounts[cardValue]++;
        }

        for (int i = 0; i < participants.Count; i++)
        {
            BattleRoundParticipant participant = participants[i];
            participant.IsMiss = cardCounts[participant.CardValue] > 1;
            if (participant.IsMiss)
                participant.AttackPower = 0;
        }
    }

    private void ApplyAttackPowerModifiers(List<BattleRoundParticipant> participants, MonsterData monsterData, MonsterBattleState monsterState)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            BattleRoundParticipant participant = participants[i];
            if (participant.IsMiss || participant.AttackCancelled || participant.DirectAttackSkipped)
                continue;

            int attackPower = Mathf.Max(0, participant.CardValue);
            BattleAttackContext context = new BattleAttackContext(participant, monsterData, monsterState);

            for (int modifierIndex = 0; modifierIndex < attackPowerModifiers.Count; modifierIndex++)
            {
                IBattleAttackPowerModifier modifier = attackPowerModifiers[modifierIndex];
                if (modifier == null)
                    continue;

                attackPower = Mathf.Max(0, modifier.ModifyAttackPower(context, attackPower));
            }

            DamageContext damageContext = new DamageContext
            {
                Participant = participant,
                MonsterData = monsterData,
                MonsterState = monsterState,
                BaseDamage = participant.CardValue,
                FinalDamage = attackPower
            };

            MageAbility.ApplyMediocreSpell(damageContext);
            FighterAbility.ApplyDamageModifier(currentFighterRoundState, damageContext);
            BerserkerAbility.ApplyOneMore(berserkerStageState, damageContext);
            participant.AttackPower = Mathf.Max(0, damageContext.FinalDamage);
        }
    }

    private void BuildAttackOrder(List<BattleRoundParticipant> participants, List<BattleRoundParticipant> target)
    {
        target.Clear();

        for (int cardValue = 6; cardValue >= 1; cardValue--)
        {
            List<BattleRoundParticipant> sameCardGroup = new List<BattleRoundParticipant>();
            for (int i = 0; i < participants.Count; i++)
            {
                if (participants[i].CardValue == cardValue)
                    sameCardGroup.Add(participants[i]);
            }

            Shuffle(sameCardGroup);
            target.AddRange(sameCardGroup);
        }
    }

    private void BuildRewardRanks(List<BattleRoundParticipant> participants, List<BattleRoundParticipant> target)
    {
        target.Clear();

        for (int i = 0; i < participants.Count; i++)
        {
            if (!participants[i].IsMiss)
                target.Add(participants[i]);
        }

        target.Sort((a, b) => a.RewardPriority.CompareTo(b.RewardPriority));

        for (int i = 0; i < rewardRankModifiers.Count; i++)
        {
            IBattleRewardRankModifier modifier = rewardRankModifiers[i];
            if (modifier == null)
                continue;

            modifier.ModifyRewardRanks(target);
        }

        MageAbility.ApplyRewardRankOverride(currentMageRoundState, target);
    }

    private void BuildInsuranceRanks(List<BattleRoundParticipant> participants, List<BattleRoundParticipant> target)
    {
        target.Clear();

        for (int cardValue = 1; cardValue <= 6; cardValue++)
        {
            List<BattleRoundParticipant> sameCardGroup = new List<BattleRoundParticipant>();
            for (int i = 0; i < participants.Count; i++)
            {
                if (participants[i].CardValue == cardValue)
                    sameCardGroup.Add(participants[i]);
            }

            Shuffle(sameCardGroup);
            target.AddRange(sameCardGroup);
        }
    }

    private void GrantMonsterRewards(List<BattleRoundParticipant> rewardRanks, MonsterData monsterData)
    {
        if (monsterData == null)
            return;

        for (int rankIndex = 0; rankIndex < rewardRanks.Count && rankIndex < 3; rankIndex++)
        {
            NetworkPlayerState player = rewardRanks[rankIndex].Player;
            if (player == null)
                continue;

            CrystalBundle reward = ParseReward(rankIndex switch
            {
                0 => monsterData.reward_1st,
                1 => monsterData.reward_2nd,
                2 => monsterData.reward_3rd,
                _ => "0"
            });

            player.ServerAddCrystals(reward.Red, reward.Blue, reward.Green);
            rewardRanks[rankIndex].ReceivedBaseReward = true;
            if (reward.Red + reward.Blue + reward.Green > 0)
                DefenderAbility.RecordReward(player);

            Debug.Log(
                $"BattleFlowController: Reward granted / monster={monsterData.monster_name}({monsterData.id}) " +
                $"/ rank={rankIndex + 1} / playerIndex={player.PlayerIndex.Value} " +
                $"/ nickname={player.DisplayNickname.Value} / red={reward.Red}, green={reward.Green}, blue={reward.Blue}");
        }
    }

    private IEnumerator DistributeInsuranceCrystals(List<BattleRoundParticipant> insuranceRanks)
    {
        NetworkSessionState session = NetworkSessionState.Instance;
        if (session == null || insuranceRanks.Count == 0)
            yield break;

        int safetyGuard = 0;
        while (session.GetTotalInsuranceCrystals() > 0 && safetyGuard < 10000)
        {
            for (int i = 0; i < insuranceRanks.Count && session.GetTotalInsuranceCrystals() > 0; i++)
            {
                NetworkPlayerState player = insuranceRanks[i].Player;
                if (player == null)
                    continue;

                if (ArcherAbility.IsArcher(player) && BattleGimmickDebugSettings.ArcherContributionRewardEnabled)
                {
                    int insuranceBefore = session.GetTotalInsuranceCrystals();
                    yield return ResolveArcherInsuranceRewardChoice(player, session);
                    if (session.GetTotalInsuranceCrystals() < insuranceBefore)
                        safetyGuard++;

                    continue;
                }

                if (!TryPickRandomInsuranceColor(session, out BattleCrystalColor color))
                    yield break;

                if (!session.ServerTryRemoveOneInsuranceCrystal(color))
                    continue;

                AddOneCrystal(player, color);
                DefenderAbility.RecordReward(player);
                safetyGuard++;
            }
        }
    }

    private IEnumerator ResolveArcherInsuranceRewardChoice(NetworkPlayerState archer, NetworkSessionState session)
    {
        if (archer == null || session == null)
            yield break;

        List<BattleCrystalColor> choices = GetRandomInsuranceChoiceColors(session, 2);
        if (choices.Count == 0)
            yield break;

        if (choices.Count == 1)
        {
            BattleCrystalColor onlyChoice = choices[0];
            if (session.ServerTryRemoveOneInsuranceCrystal(onlyChoice))
            {
                BattleCurrencyUtility.AddOne(archer, onlyChoice);
                DefenderAbility.RecordReward(archer);
            }

            yield break;
        }

        bool redEnabled = choices.Contains(BattleCrystalColor.Red);
        bool greenEnabled = choices.Contains(BattleCrystalColor.Green);
        bool blueEnabled = choices.Contains(BattleCrystalColor.Blue);

        yield return RequestCrystalChoice(
            archer,
            "Archer: choose a crystal",
            redEnabled,
            greenEnabled,
            blueEnabled,
            selected =>
            {
                if (!choices.Contains(selected))
                    return;

                if (session.ServerTryRemoveOneInsuranceCrystal(selected))
                {
                    BattleCurrencyUtility.AddOne(archer, selected);
                    DefenderAbility.RecordReward(archer);
                }
            });
    }

    private MonsterAttackContext BuildMonsterAttackContext(List<BattleRoundParticipant> participants)
    {
        MonsterAttackContext context = new MonsterAttackContext();
        if (participants == null || participants.Count == 0)
        {
            context.ResolveFinalTargets();
            return context;
        }

        int minCardValue = int.MaxValue;
        for (int i = 0; i < participants.Count; i++)
            minCardValue = Mathf.Min(minCardValue, participants[i].CardValue);

        for (int i = 0; i < participants.Count; i++)
        {
            BattleRoundParticipant participant = participants[i];
            if (participant.CardValue == minCardValue)
                context.OriginalTargets.Add(participant);
        }

        SaintAbility.ApplyMonsterAttackTargetOverride(currentSaintRoundState, participants, context);
        MageAbility.ApplyMonsterAttackTargetOverride(currentMageRoundState, participants, context);
        KnightAbility.ApplyMonsterAttackAdditionalTarget(currentKnightRoundState, participants, context);
        DefenderAbility.ApplyMonsterAttackTargetOverride(DefenderAbility.FindDefender(), context);
        context.ResolveFinalTargets();

        if (currentRoundContext != null)
        {
            currentRoundContext.MonsterAttackTargetIndexes.Clear();
            for (int i = 0; i < context.FinalTargets.Count; i++)
                currentRoundContext.MonsterAttackTargetIndexes.Add(context.FinalTargets[i].PlayerIndex);
        }

        return context;
    }

    private List<BattleRoundParticipant> ApplyFailurePenalty(List<BattleRoundParticipant> participants)
    {
        List<BattleRoundParticipant> penaltyTargets = new List<BattleRoundParticipant>();
        if (participants.Count == 0)
            return penaltyTargets;

        NetworkSessionState session = NetworkSessionState.Instance;
        for (int i = 0; i < participants.Count; i++)
        {
            BattleRoundParticipant participant = participants[i];
            penaltyTargets.Add(participant);
            if (session == null || participant.Player == null)
                continue;

            if (!TryPickMaxCrystalColor(participant.Player, out BattleCrystalColor color))
                continue;

            int removedAmount = participant.Player.ServerRemoveAllCrystals(color);
            session.ServerAddInsuranceCrystals(color, removedAmount);
            KnightAbility.RecordFailedPlanLoss(currentKnightRoundState, participant, color, removedAmount);
        }

        return penaltyTargets;
    }

    private CrystalBundle ParseReward(string rewardText)
    {
        CrystalBundle reward = new CrystalBundle();
        if (string.IsNullOrWhiteSpace(rewardText) || rewardText.Trim() == "0")
            return reward;

        string[] tokens = rewardText.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i].Trim().ToLowerInvariant();
            if (token.Length < 2)
                continue;

            int amount = 0;
            if (!int.TryParse(token.Substring(1), out amount) || amount <= 0)
                continue;

            switch (token[0])
            {
                case 'r':
                    reward.Red += amount;
                    break;

                case 'g':
                    reward.Green += amount;
                    break;

                case 'b':
                    reward.Blue += amount;
                    break;
            }
        }

        return reward;
    }

    private bool TryPickRandomInsuranceColor(NetworkSessionState session, out BattleCrystalColor color)
    {
        colorBuffer.Clear();
        AddInsuranceColorIfAvailable(session, BattleCrystalColor.Red);
        AddInsuranceColorIfAvailable(session, BattleCrystalColor.Green);
        AddInsuranceColorIfAvailable(session, BattleCrystalColor.Blue);

        if (colorBuffer.Count == 0)
        {
            color = BattleCrystalColor.Red;
            return false;
        }

        color = colorBuffer[UnityEngine.Random.Range(0, colorBuffer.Count)];
        return true;
    }

    private List<BattleCrystalColor> GetRandomInsuranceChoiceColors(NetworkSessionState session, int count)
    {
        List<BattleCrystalColor> choices = new List<BattleCrystalColor>();
        colorBuffer.Clear();

        if (session == null || count <= 0)
            return choices;

        AddInsuranceColorCopies(session, BattleCrystalColor.Red);
        AddInsuranceColorCopies(session, BattleCrystalColor.Green);
        AddInsuranceColorCopies(session, BattleCrystalColor.Blue);
        Shuffle(colorBuffer);

        int revealCount = Mathf.Min(count, colorBuffer.Count);
        for (int i = 0; i < revealCount; i++)
        {
            BattleCrystalColor color = colorBuffer[i];
            if (!choices.Contains(color))
                choices.Add(color);
        }

        return choices;
    }

    private NetworkPlayerState FindPlayerByJob(JobType job)
    {
        NetworkPlayerRegistry.FillSpawnedPlayers(playerBuffer);
        for (int i = 0; i < playerBuffer.Count; i++)
        {
            NetworkPlayerState player = playerBuffer[i];
            if (IsRoundPlayer(player) && player.Job.Value == job)
                return player;
        }

        return null;
    }

    private void AddInsuranceColorIfAvailable(NetworkSessionState session, BattleCrystalColor color)
    {
        if (session.GetInsuranceAmount(color) > 0)
            colorBuffer.Add(color);
    }

    private void AddInsuranceColorCopies(NetworkSessionState session, BattleCrystalColor color)
    {
        int amount = session != null ? session.GetInsuranceAmount(color) : 0;
        for (int i = 0; i < amount; i++)
            colorBuffer.Add(color);
    }

    private bool TryPickMaxCrystalColor(NetworkPlayerState player, out BattleCrystalColor color)
    {
        colorBuffer.Clear();

        int maxAmount = Mathf.Max(player.CrystalRed.Value, player.CrystalBlue.Value, player.CrystalGreen.Value);
        if (maxAmount <= 0)
        {
            color = BattleCrystalColor.Red;
            return false;
        }

        if (player.CrystalRed.Value == maxAmount)
            colorBuffer.Add(BattleCrystalColor.Red);

        if (player.CrystalGreen.Value == maxAmount)
            colorBuffer.Add(BattleCrystalColor.Green);

        if (player.CrystalBlue.Value == maxAmount)
            colorBuffer.Add(BattleCrystalColor.Blue);

        color = colorBuffer[UnityEngine.Random.Range(0, colorBuffer.Count)];
        return true;
    }

    private void AddOneCrystal(NetworkPlayerState player, BattleCrystalColor color)
    {
        switch (color)
        {
            case BattleCrystalColor.Red:
                player.ServerAddCrystals(1, 0, 0);
                break;

            case BattleCrystalColor.Blue:
                player.ServerAddCrystals(0, 1, 0);
                break;

            case BattleCrystalColor.Green:
                player.ServerAddCrystals(0, 0, 1);
                break;
        }
    }

    private void PlayResultForAll(string triggerName)
    {
        NetworkPlayerRegistry.FillSpawnedPlayers(playerBuffer);
        for (int i = 0; i < playerBuffer.Count; i++)
        {
            NetworkPlayerState player = playerBuffer[i];
            if (!IsRoundPlayer(player))
                continue;

            PlayStandingTrigger(player.PlayerIndex.Value, triggerName);
        }
    }

    private void PlayResultForParticipants(List<BattleRoundParticipant> participants, string triggerName)
    {
        for (int i = 0; i < participants.Count; i++)
        {
            BattleRoundParticipant participant = participants[i];
            if (participant != null)
                PlayStandingTrigger(participant.PlayerIndex, triggerName);
        }
    }

    private void PlayStandingTrigger(int playerIndex, string triggerName)
    {
        if (playerIndex < 0 || string.IsNullOrWhiteSpace(triggerName))
            return;

        if (IsSpawned)
        {
            PlayStandingTriggerRpc(playerIndex, new FixedString64Bytes(triggerName));
            return;
        }

        BattlePlayerStandingRegistry.PlayCustom(playerIndex, triggerName);
    }

    private float GetActionWaitSeconds(BattleRoundParticipant participant)
    {
        float animationDuration = participant != null && (participant.IsMiss || participant.AttackCancelled || participant.DirectAttackSkipped)
            ? MissAnimationDuration
            : AttackAnimationDuration;

        return animationDuration + DelayAfterEachAction;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayStandingTriggerRpc(int playerIndex, FixedString64Bytes triggerName)
    {
        BattlePlayerStandingRegistry.PlayCustom(playerIndex, triggerName.ToString());
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowCrystalChoiceRpc(
        ulong ownerClientId,
        int requestId,
        FixedString64Bytes title,
        bool redEnabled,
        bool greenEnabled,
        bool blueEnabled)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.LocalClientId != ownerClientId)
            return;

        ShowLocalCrystalChoice(requestId, title.ToString(), redEnabled, greenEnabled, blueEnabled);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void HideCrystalChoiceRpc(ulong ownerClientId)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.LocalClientId != ownerClientId)
            return;

        BattleCrystalChoiceUI.Hide();
    }

    [Rpc(SendTo.Server)]
    public void SubmitCrystalChoiceRpc(int requestId, BattleCrystalColor color, RpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (requestId != pendingChoiceRequestId || senderId != pendingChoiceOwnerClientId)
            return;

        if (!IsChoiceColorEnabled(color))
            return;

        pendingChoiceColor = color;
        pendingChoiceResolved = true;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void BeginKnightShiftBlameTargetSelectionRpc(ulong ownerClientId, int requestId, int[] targetPlayerIndexes)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.LocalClientId != ownerClientId)
            return;

        BeginLocalKnightShiftBlameTargetSelection(requestId, targetPlayerIndexes);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void EndKnightShiftBlameTargetSelectionRpc(ulong ownerClientId)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.LocalClientId != ownerClientId)
            return;

        EndLocalKnightShiftBlameTargetSelection();
    }

    [Rpc(SendTo.Server)]
    private void RequestKnightShiftBlameTargetRpc(int requestId, int targetPlayerIndex, RpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (requestId != pendingKnightShiftBlameRequestId || senderId != pendingKnightShiftBlameOwnerClientId)
            return;

        if (!pendingKnightShiftBlameTargets.Contains(targetPlayerIndex))
            return;

        pendingKnightShiftBlameTargetIndex = targetPlayerIndex;
        pendingKnightShiftBlameTargetResolved = true;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void BeginDefenderTargetSelectionRpc(ulong ownerClientId, int requestId, int[] targetPlayerIndexes)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.LocalClientId != ownerClientId)
            return;

        BeginLocalDefenderTargetSelection(requestId, targetPlayerIndexes);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void EndDefenderTargetSelectionRpc(ulong ownerClientId)
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager != null && manager.LocalClientId != ownerClientId)
            return;

        EndLocalDefenderTargetSelection();
    }

    [Rpc(SendTo.Server)]
    private void RequestDefenderTargetRpc(int requestId, int targetPlayerIndex, RpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (requestId != pendingDefenderTargetRequestId || senderId != pendingDefenderTargetOwnerClientId)
            return;

        if (!pendingDefenderTargetChoices.Contains(targetPlayerIndex))
            return;

        pendingDefenderTargetIndex = targetPlayerIndex;
        pendingDefenderTargetResolved = true;

        if (pendingDefenderLoanSelection &&
            NetworkPlayerRegistry.TryGetByPlayerIndex(targetPlayerIndex, out NetworkPlayerState target) &&
            NetworkPlayerRegistry.TryGetByOwnerClientId(senderId, out NetworkPlayerState defender) &&
            DefenderAbility.IsDefender(defender) &&
            target != null)
        {
            DefenderAbility.SetCustomer(defender, target);
        }
    }

    private void ShowLocalCrystalChoice(
        int requestId,
        string title,
        bool redEnabled,
        bool greenEnabled,
        bool blueEnabled)
    {
        BattleCrystalChoiceUI.Show(
            title,
            redEnabled,
            greenEnabled,
            blueEnabled,
            selected =>
            {
                if (IsSpawned)
                {
                    SubmitCrystalChoiceRpc(requestId, selected);
                    return;
                }

                if (requestId != pendingChoiceRequestId || !IsChoiceColorEnabled(selected))
                    return;

                pendingChoiceColor = selected;
                pendingChoiceResolved = true;
            });
    }

    private void BeginLocalKnightShiftBlameTargetSelection(int requestId, int[] targetPlayerIndexes)
    {
        clientKnightShiftBlameTargets.Clear();
        if (targetPlayerIndexes != null)
        {
            for (int i = 0; i < targetPlayerIndexes.Length; i++)
                clientKnightShiftBlameTargets.Add(targetPlayerIndexes[i]);
        }

        clientKnightShiftBlameRequestId = requestId;
        clientKnightShiftBlameSelectionActive = clientKnightShiftBlameTargets.Count > 0;
        if (clientKnightShiftBlameSelectionActive)
            GameStatusMessageRouter.Show("책임 전가: 재화를 뺏을 플레이어를 클릭하세요.");
    }

    private void EndLocalKnightShiftBlameTargetSelection()
    {
        clientKnightShiftBlameSelectionActive = false;
        clientKnightShiftBlameTargets.Clear();
        clientKnightShiftBlameRequestId = 0;
    }

    private void BeginLocalDefenderTargetSelection(int requestId, int[] targetPlayerIndexes)
    {
        clientDefenderTargetChoices.Clear();
        if (targetPlayerIndexes != null)
        {
            for (int i = 0; i < targetPlayerIndexes.Length; i++)
                clientDefenderTargetChoices.Add(targetPlayerIndexes[i]);
        }

        clientDefenderTargetRequestId = requestId;
        clientDefenderTargetSelectionActive = clientDefenderTargetChoices.Count > 0;
        if (clientDefenderTargetSelectionActive)
            GameStatusMessageRouter.Show("Defender: click a target.");
    }

    private void EndLocalDefenderTargetSelection()
    {
        clientDefenderTargetSelectionActive = false;
        clientDefenderTargetChoices.Clear();
        clientDefenderTargetRequestId = 0;
    }

    private bool IsChoiceColorEnabled(BattleCrystalColor color)
    {
        return color switch
        {
            BattleCrystalColor.Red => pendingChoiceRedEnabled,
            BattleCrystalColor.Green => pendingChoiceGreenEnabled,
            BattleCrystalColor.Blue => pendingChoiceBlueEnabled,
            _ => false
        };
    }

    private void ClearPendingCrystalChoice(int requestId)
    {
        if (pendingChoiceRequestId != requestId)
            return;

        pendingChoiceRequestId = 0;
        pendingChoiceOwnerClientId = ulong.MaxValue;
        pendingChoiceRedEnabled = false;
        pendingChoiceGreenEnabled = false;
        pendingChoiceBlueEnabled = false;
        pendingChoiceResolved = false;
        pendingChoiceColor = BattleCrystalColor.Red;
    }

    private bool TryMoveOneCrystal(NetworkPlayerState from, NetworkPlayerState to, BattleCrystalColor color)
    {
        if (from == null || to == null || from.GetCrystalAmount(color) <= 0)
            return false;

        from.ServerSetCrystalAmount(color, from.GetCrystalAmount(color) - 1);
        BattleCurrencyUtility.AddOne(to, color);
        return true;
    }

    private void ClearSubmittedCards()
    {
        NetworkPlayerRegistry.FillSpawnedPlayers(playerBuffer);
        for (int i = 0; i < playerBuffer.Count; i++)
        {
            NetworkPlayerState player = playerBuffer[i];
            if (player == null)
                continue;

            player.ServerClearSubmittedCard();
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[swapIndex]) = (list[swapIndex], list[i]);
        }
    }

    private struct CrystalBundle
    {
        public int Red;
        public int Blue;
        public int Green;
    }
}
