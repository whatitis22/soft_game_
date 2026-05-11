using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class BattleFlowController : NetworkBehaviour
{
    public static BattleFlowController Instance { get; private set; }

    private const string FallbackStageName = "Grassland";

    [Header("References")]
    [SerializeField] private MonsterSpawner monsterSpawner;
    [SerializeField] private MonsterTable monsterTable;

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

    private Coroutine resolveCoroutine;
    private bool resolvingRound;
    private bool lastRoundLocked;

    public bool IsResolvingRound => resolvingRound || resolveCoroutine != null;
    public float ResolveDelayAfterLock => Mathf.Max(0f, resolveDelayAfterLock);
    public float AttackAnimationDuration => Mathf.Max(0f, attackAnimationDuration);
    public float MissAnimationDuration => Mathf.Max(0f, missAnimationDuration);
    public float DelayAfterEachAction => Mathf.Max(0f, delayAfterEachAction);
    public float AttackStepDelay => AttackAnimationDuration + DelayAfterEachAction;
    public MonsterData CurrentMonsterData => monsterSpawner != null ? monsterSpawner.CurrentMonsterData : null;

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

    private void OnValidate()
    {
        currentStageTableChance = Mathf.Clamp01(currentStageTableChance);
        resolveDelayAfterLock = Mathf.Max(0f, resolveDelayAfterLock);
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

        MarkMonsterAppeared(selectedMonster);
        ClearSubmittedCards();
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

        ApplyMissFlags(participantBuffer);
        ApplyAttackPowerModifiers(participantBuffer, monsterSpawner.CurrentMonsterData, monsterState);
        BuildAttackOrder(participantBuffer, attackOrderBuffer);
        BuildRewardRanks(participantBuffer, rewardRankBuffer);
        BuildInsuranceRanks(participantBuffer, insuranceRankBuffer);

        for (int i = 0; i < attackOrderBuffer.Count; i++)
        {
            BattleRoundParticipant participant = attackOrderBuffer[i];
            PlayStandingTrigger(participant.PlayerIndex, participant.IsMiss ? missTriggerName : attackTriggerName);

            if (!participant.IsMiss && participant.AttackPower > 0 && !monsterState.IsDead)
                participant.AppliedDamage = monsterSpawner.ApplyDamageToCurrentMonster(participant.AttackPower, true);

            float actionWait = GetActionWaitSeconds(participant);
            if (actionWait > 0f)
                yield return new WaitForSeconds(actionWait);
        }

        if (monsterState.IsDead)
        {
            GrantMonsterRewards(rewardRankBuffer, monsterSpawner.CurrentMonsterData);
            DistributeInsuranceCrystals(insuranceRankBuffer);
            PlayResultForAll(victoryTriggerName);

            if (resultAnimationDelay > 0f)
                yield return new WaitForSeconds(resultAnimationDelay);

            monsterSpawner.PlayCurrentMonsterPendingDeathAnimation();
        }
        else
        {
            List<BattleRoundParticipant> penaltyTargets = ApplyFailurePenalty(participantBuffer);
            PlayResultForParticipants(penaltyTargets, defeatTriggerName);
        }

        FinishRoundResolution(true);
    }

    private void FinishRoundResolution(bool clearSubmittedCards)
    {
        if (clearSubmittedCards)
            ClearSubmittedCards();

        NetworkSessionState.Instance?.ServerResetRoundLock();
        resolvingRound = false;
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
            if (participant.IsMiss)
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

            participant.AttackPower = attackPower;
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

        target.Sort((a, b) => a.CardValue.CompareTo(b.CardValue));

        for (int i = 0; i < rewardRankModifiers.Count; i++)
        {
            IBattleRewardRankModifier modifier = rewardRankModifiers[i];
            if (modifier == null)
                continue;

            modifier.ModifyRewardRanks(target);
        }
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
        }
    }

    private void DistributeInsuranceCrystals(List<BattleRoundParticipant> insuranceRanks)
    {
        NetworkSessionState session = NetworkSessionState.Instance;
        if (session == null || insuranceRanks.Count == 0)
            return;

        int safetyGuard = 0;
        while (session.GetTotalInsuranceCrystals() > 0 && safetyGuard < 10000)
        {
            for (int i = 0; i < insuranceRanks.Count && session.GetTotalInsuranceCrystals() > 0; i++)
            {
                NetworkPlayerState player = insuranceRanks[i].Player;
                if (player == null)
                    continue;

                if (!TryPickRandomInsuranceColor(session, out BattleCrystalColor color))
                    return;

                if (!session.ServerTryRemoveOneInsuranceCrystal(color))
                    continue;

                AddOneCrystal(player, color);
                safetyGuard++;
            }
        }
    }

    private List<BattleRoundParticipant> ApplyFailurePenalty(List<BattleRoundParticipant> participants)
    {
        List<BattleRoundParticipant> penaltyTargets = new List<BattleRoundParticipant>();
        if (participants.Count == 0)
            return penaltyTargets;

        int minCardValue = int.MaxValue;
        for (int i = 0; i < participants.Count; i++)
            minCardValue = Mathf.Min(minCardValue, participants[i].CardValue);

        NetworkSessionState session = NetworkSessionState.Instance;
        for (int i = 0; i < participants.Count; i++)
        {
            BattleRoundParticipant participant = participants[i];
            if (participant.CardValue != minCardValue)
                continue;

            penaltyTargets.Add(participant);
            if (session == null || participant.Player == null)
                continue;

            if (!TryPickMaxCrystalColor(participant.Player, out BattleCrystalColor color))
                continue;

            int removedAmount = participant.Player.ServerRemoveAllCrystals(color);
            session.ServerAddInsuranceCrystals(color, removedAmount);
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
        AddInsuranceColorIfAvailable(session, BattleCrystalColor.Blue);
        AddInsuranceColorIfAvailable(session, BattleCrystalColor.Green);

        if (colorBuffer.Count == 0)
        {
            color = BattleCrystalColor.Red;
            return false;
        }

        color = colorBuffer[UnityEngine.Random.Range(0, colorBuffer.Count)];
        return true;
    }

    private void AddInsuranceColorIfAvailable(NetworkSessionState session, BattleCrystalColor color)
    {
        if (session.GetInsuranceAmount(color) > 0)
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

        if (player.CrystalBlue.Value == maxAmount)
            colorBuffer.Add(BattleCrystalColor.Blue);

        if (player.CrystalGreen.Value == maxAmount)
            colorBuffer.Add(BattleCrystalColor.Green);

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
        float animationDuration = participant != null && participant.IsMiss
            ? MissAnimationDuration
            : AttackAnimationDuration;

        return animationDuration + DelayAfterEachAction;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayStandingTriggerRpc(int playerIndex, FixedString64Bytes triggerName)
    {
        BattlePlayerStandingRegistry.PlayCustom(playerIndex, triggerName.ToString());
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
