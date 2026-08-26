using System.Collections.Generic;

public enum AttackCancelReason
{
    None,
    DuplicateNumber,
    MageDestructionSpell,
    ArcherScream,
    CharacterEffect
}

public enum ForcedTargetType
{
    Replace,
    Add
}

public sealed class RoundContext
{
    public int RoundIndex;
    public readonly Dictionary<int, int> SubmittedNumbersByPlayerIndex = new Dictionary<int, int>();
    public readonly Dictionary<int, List<int>> NumberGroups = new Dictionary<int, List<int>>();
    public readonly HashSet<int> DuplicatePlayerIndexes = new HashSet<int>();
    public readonly Dictionary<int, AttackResult> AttackResultsByPlayerIndex = new Dictionary<int, AttackResult>();
    public readonly List<int> SuccessfulAttackerIndexes = new List<int>();
    public readonly List<DamageRecord> DamageRecords = new List<DamageRecord>();
    public readonly List<int> MonsterAttackTargetIndexes = new List<int>();

    public bool AnyDuplicate;
    public bool AnyAttackCancelled;
    public bool AllAttacksHit;
    public bool MonsterKilled;
    public int MonsterHpBefore;
    public int MonsterHpAfter;
}

public sealed class MonsterAttackContext
{
    public readonly List<BattleRoundParticipant> OriginalTargets = new List<BattleRoundParticipant>();
    public readonly List<BattleRoundParticipant> FinalTargets = new List<BattleRoundParticipant>();
    public readonly List<BattleRoundParticipant> ReplacementTargets = new List<BattleRoundParticipant>();
    public readonly List<BattleRoundParticipant> AdditionalTargets = new List<BattleRoundParticipant>();

    public void AddForcedTarget(BattleRoundParticipant participant, ForcedTargetType type)
    {
        if (participant == null)
            return;

        List<BattleRoundParticipant> target = type == ForcedTargetType.Replace
            ? ReplacementTargets
            : AdditionalTargets;

        if (!target.Contains(participant))
            target.Add(participant);
    }

    public void ResolveFinalTargets()
    {
        FinalTargets.Clear();

        if (ReplacementTargets.Count > 0)
            FinalTargets.AddRange(ReplacementTargets);
        else
            FinalTargets.AddRange(OriginalTargets);

        for (int i = 0; i < AdditionalTargets.Count; i++)
        {
            BattleRoundParticipant participant = AdditionalTargets[i];
            if (participant != null && !FinalTargets.Contains(participant))
                FinalTargets.Add(participant);
        }
    }
}

public sealed class AttackResult
{
    public int AttackerPlayerIndex;
    public int SubmittedNumber;
    public int BaseDamage;
    public int FinalDamage;
    public bool WasAttempted;
    public bool WasCancelled;
    public bool DidHit;
    public AttackCancelReason CancelReason;
    public bool WasSupportDamage;
    public int MonsterHpBefore;
    public int MonsterHpAfter;
}

public sealed class DamageContext
{
    public BattleRoundParticipant Participant;
    public MonsterData MonsterData;
    public MonsterBattleState MonsterState;
    public int BaseDamage;
    public int FinalDamage;
    public bool IsCancelled;
    public AttackCancelReason CancelReason;
}

public readonly struct DamageRecord
{
    public DamageRecord(int playerIndex, int amount, int monsterHpBefore, int monsterHpAfter)
    {
        PlayerIndex = playerIndex;
        Amount = amount;
        MonsterHpBefore = monsterHpBefore;
        MonsterHpAfter = monsterHpAfter;
    }

    public int PlayerIndex { get; }
    public int Amount { get; }
    public int MonsterHpBefore { get; }
    public int MonsterHpAfter { get; }
}
