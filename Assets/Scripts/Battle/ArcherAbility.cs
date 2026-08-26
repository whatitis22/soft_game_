using System.Collections.Generic;

public sealed class ArcherRoundState
{
    public bool SupportMode;
    public int SupportDamageCount;
    public int TotalSupportDamage;
    public bool ScreamTriggered;
    public bool ContributionRewardAvailable;
    public bool ContributionRewardReceived;
}

public static class ArcherAbility
{
    public static bool IsArcher(NetworkPlayerState player)
    {
        return player != null && player.Job.Value == JobType.Archer;
    }

    public static ArcherRoundState PrepareRound(IReadOnlyList<BattleRoundParticipant> participants)
    {
        ArcherRoundState state = new ArcherRoundState();
        BattleRoundParticipant archer = FindArcher(participants);
        if (archer == null || archer.IsMiss)
            return state;

        if (!BattleGimmickDebugSettings.ArcherSupportAttackEnabled)
            return state;

        state.SupportMode = archer.CardValue == 5 || archer.CardValue == 6;
        if (state.SupportMode)
            archer.DirectAttackSkipped = true;

        return state;
    }

    public static bool ShouldSkipDirectAttack(BattleRoundParticipant participant)
    {
        return participant != null && IsArcher(participant.Player) && participant.DirectAttackSkipped;
    }

    public static bool TryCancelKillingDamage(
        ArcherRoundState state,
        BattleRoundParticipant participant,
        MonsterBattleState monsterState,
        RoundContext context)
    {
        if (participant == null || monsterState == null || !IsArcher(participant.Player))
            return false;

        if (!BattleGimmickDebugSettings.ArcherScreamEnabled)
            return false;

        if (participant.AttackPower <= 0 || monsterState.CurrentHp - participant.AttackPower > 0)
            return false;

        state.ScreamTriggered = true;
        BattleRoundResolver.MarkAttackCancelled(context, participant, AttackCancelReason.ArcherScream);
        return true;
    }

    public static bool TryApplySupportDamage(
        ArcherRoundState state,
        IReadOnlyList<BattleRoundParticipant> participants,
        BattleRoundParticipant resolvedAttacker,
        MonsterSpawner monsterSpawner,
        MonsterBattleState monsterState,
        RoundContext context)
    {
        if (state == null || !state.SupportMode || resolvedAttacker == null || monsterState == null || monsterState.IsDead)
            return false;

        if (!BattleGimmickDebugSettings.ArcherSupportAttackEnabled)
            return false;

        BattleRoundParticipant archer = FindArcher(participants);
        if (archer == null || !IsAheadOfArcher(resolvedAttacker, archer))
            return false;

        if (monsterState.CurrentHp - 1 <= 0 && BattleGimmickDebugSettings.ArcherScreamEnabled)
        {
            state.ScreamTriggered = true;
            return false;
        }

        int hpBefore = monsterState.CurrentHp;
        bool applied = monsterSpawner != null && monsterSpawner.ApplyDamageToCurrentMonster(1, true);
        if (!applied)
            return false;

        state.SupportDamageCount++;
        state.TotalSupportDamage++;
        BattleRoundResolver.RecordSupportDamage(context, archer, hpBefore, monsterState.CurrentHp, true);
        return true;
    }

    public static void GrantContributionReward(
        ArcherRoundState state,
        NetworkSessionState session,
        NetworkPlayerState archer)
    {
        if (state == null || !state.ContributionRewardAvailable || state.ContributionRewardReceived)
            return;

        if (!BattleGimmickDebugSettings.ArcherContributionRewardEnabled)
            return;

        if (session == null || archer == null)
            return;

        for (int i = 0; i < 2; i++)
        {
            List<BattleCrystalColor> colors = BattleCurrencyUtility.GetAvailableInsuranceColors(session);
            if (colors.Count == 0)
                break;

            BattleCrystalColor color = BattleCurrencyUtility.PickRandom(colors, BattleCrystalColor.Red);
            if (!session.ServerTryRemoveOneInsuranceCrystal(color))
                continue;

            BattleCurrencyUtility.AddOne(archer, color);
            state.ContributionRewardReceived = true;
        }
    }

    public static BattleRoundParticipant FindArcher(IReadOnlyList<BattleRoundParticipant> participants)
    {
        if (participants == null)
            return null;

        for (int i = 0; i < participants.Count; i++)
        {
            BattleRoundParticipant participant = participants[i];
            if (participant != null && IsArcher(participant.Player))
                return participant;
        }

        return null;
    }

    private static bool IsAheadOfArcher(BattleRoundParticipant attacker, BattleRoundParticipant archer)
    {
        return attacker != null
            && archer != null
            && attacker != archer
            && !attacker.IsMiss
            && !attacker.AttackCancelled
            && attacker.CardValue < archer.CardValue;
    }
}
