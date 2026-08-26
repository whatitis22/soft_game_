using System.Collections.Generic;

public sealed class KnightRoundState
{
    public bool CalculatedVictoryTriggered;
    public bool FailedPlanTriggered;
    public bool LostCurrency;
    public BattleCrystalColor LostCurrencyType;
    public int LostCurrencyAmount;
    public bool ShiftBlameAvailable;
    public bool ShiftBlameUsed;
    public readonly List<int> ShiftBlameTargets = new List<int>();
}

public static class KnightAbility
{
    public static bool IsKnight(NetworkPlayerState player)
    {
        return player != null && player.Job.Value == JobType.Hero;
    }

    public static void ResolvePostAttack(
        KnightRoundState state,
        RoundContext context,
        IReadOnlyList<BattleRoundParticipant> participants)
    {
        BattleRoundParticipant knight = FindKnight(participants);
        if (state == null || context == null || knight == null)
            return;

        if (BattleGimmickDebugSettings.KnightCalculatedVictoryEnabled
            && !context.AnyDuplicate
            && context.AllAttacksHit
            && context.MonsterKilled)
        {
            state.CalculatedVictoryTriggered = true;
        }

        if (!BattleGimmickDebugSettings.KnightFailedPlanEnabled)
            return;

        if (!CanTriggerFailedPlan(context))
            return;

        state.FailedPlanTriggered = true;
        state.ShiftBlameTargets.Clear();
        foreach (int playerIndex in context.DuplicatePlayerIndexes)
        {
            if (playerIndex != knight.PlayerIndex)
                state.ShiftBlameTargets.Add(playerIndex);
        }
    }

    public static void GrantCalculatedVictoryReward(
        KnightRoundState state,
        NetworkSessionState session,
        NetworkPlayerState knight)
    {
        if (state == null || !state.CalculatedVictoryTriggered || session == null || knight == null)
            return;

        if (!BattleGimmickDebugSettings.KnightCalculatedVictoryEnabled)
            return;

        List<BattleCrystalColor> leastOwned = BattleCurrencyUtility.GetLeastOwnedColors(knight);
        for (int i = 0; i < leastOwned.Count; i++)
        {
            BattleCrystalColor color = leastOwned[i];
            if (session.GetInsuranceAmount(color) <= 0)
                continue;

            if (!session.ServerTryRemoveOneInsuranceCrystal(color))
                continue;

            BattleCurrencyUtility.AddOne(knight, color);
            DefenderAbility.RecordReward(knight);
            return;
        }
    }

    public static void ApplyMonsterAttackAdditionalTarget(
        KnightRoundState state,
        IReadOnlyList<BattleRoundParticipant> participants,
        MonsterAttackContext context)
    {
        if (state == null || !state.FailedPlanTriggered || context == null)
            return;

        if (!BattleGimmickDebugSettings.KnightFailedPlanEnabled)
            return;

        BattleRoundParticipant knight = FindKnight(participants);
        if (knight != null)
            context.AddForcedTarget(knight, ForcedTargetType.Add);
    }

    public static void RecordFailedPlanLoss(
        KnightRoundState state,
        BattleRoundParticipant participant,
        BattleCrystalColor color,
        int amount)
    {
        if (state == null || participant == null || !IsKnight(participant.Player) || !state.FailedPlanTriggered)
            return;

        if (!BattleGimmickDebugSettings.KnightFailedPlanEnabled)
            return;

        state.LostCurrency = amount > 0;
        state.LostCurrencyType = color;
        state.LostCurrencyAmount = amount;
        state.ShiftBlameAvailable = BattleGimmickDebugSettings.KnightShiftBlameEnabled
            && state.FailedPlanTriggered
            && amount > 0;
    }

    private static bool CanTriggerFailedPlan(RoundContext context)
    {
        if (context == null || context.DuplicatePlayerIndexes.Count == 0 || context.MonsterKilled)
            return false;

        foreach (AttackResult result in context.AttackResultsByPlayerIndex.Values)
        {
            if (result.WasCancelled && result.CancelReason == AttackCancelReason.DuplicateNumber)
                return true;
        }

        return false;
    }

    public static BattleRoundParticipant FindKnight(IReadOnlyList<BattleRoundParticipant> participants)
    {
        if (participants == null)
            return null;

        for (int i = 0; i < participants.Count; i++)
        {
            BattleRoundParticipant participant = participants[i];
            if (participant != null && IsKnight(participant.Player))
                return participant;
        }

        return null;
    }
}
