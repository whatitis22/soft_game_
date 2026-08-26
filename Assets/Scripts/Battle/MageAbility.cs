using System.Collections.Generic;
using UnityEngine;

public sealed class MageRoundState
{
    public bool DestructionSpellTriggered;
    public bool WitchHuntTriggered;
    public bool InstantKillApplied;
}

public static class MageAbility
{
    public static bool IsMage(NetworkPlayerState player)
    {
        return player != null && player.Job.Value == JobType.Witch;
    }

    public static void ApplyMediocreSpell(DamageContext context)
    {
        if (context == null || context.Participant == null)
            return;

        if (!BattleGimmickDebugSettings.MageMediocreSpellEnabled)
            return;

        if (!IsMage(context.Participant.Player))
            return;

        int submittedNumber = context.Participant.CardValue;
        if (submittedNumber < 1 || submittedNumber > 5)
            return;

        context.FinalDamage = Mathf.Max(0, context.BaseDamage - 1);
    }

    public static MageRoundState ResolveDestructionSpell(
        RoundContext context,
        IReadOnlyList<BattleRoundParticipant> participants,
        MonsterSpawner monsterSpawner,
        MonsterBattleState monsterState)
    {
        MageRoundState state = new MageRoundState();
        BattleRoundParticipant mage = FindSubmittedMage(participants);
        if (mage == null || mage.CardValue != 6)
            return state;

        if (IsNumberDuplicated(context, 6))
        {
            state.WitchHuntTriggered = BattleGimmickDebugSettings.MageWitchHuntEnabled;
            return state;
        }

        if (!BattleGimmickDebugSettings.MageDestructionSpellEnabled)
            return state;

        state.DestructionSpellTriggered = true;

        for (int i = 0; i < participants.Count; i++)
        {
            BattleRoundParticipant participant = participants[i];
            if (participant == null || participant == mage)
                continue;

            BattleRoundResolver.MarkAttackCancelled(
                context,
                participant,
                AttackCancelReason.MageDestructionSpell);
        }

        if (monsterSpawner != null && monsterState != null && !monsterState.IsDead)
        {
            int hpBefore = monsterState.CurrentHp;
            monsterSpawner.ApplyDamageToCurrentMonster(hpBefore, true);
            state.InstantKillApplied = true;
            BattleRoundResolver.RecordDamage(
                context,
                mage,
                hpBefore,
                monsterState.CurrentHp,
                hpBefore > monsterState.CurrentHp);
        }

        return state;
    }

    public static void ApplyRewardRankOverride(
        MageRoundState state,
        List<BattleRoundParticipant> rewardRanks)
    {
        if (state == null || !state.DestructionSpellTriggered || rewardRanks == null)
            return;

        if (!BattleGimmickDebugSettings.MageDestructionSpellEnabled)
            return;

        int mageIndex = rewardRanks.FindIndex(participant => participant != null && IsMage(participant.Player));
        if (mageIndex <= 0)
            return;

        BattleRoundParticipant mage = rewardRanks[mageIndex];
        rewardRanks.RemoveAt(mageIndex);
        rewardRanks.Insert(0, mage);
    }

    public static void ApplyMonsterAttackTargetOverride(
        MageRoundState state,
        IReadOnlyList<BattleRoundParticipant> participants,
        MonsterAttackContext context)
    {
        if (state == null || !state.WitchHuntTriggered || context == null)
            return;

        if (!BattleGimmickDebugSettings.MageWitchHuntEnabled)
            return;

        BattleRoundParticipant mage = FindSubmittedMage(participants);
        if (mage != null)
            context.AddForcedTarget(mage, ForcedTargetType.Replace);
    }

    private static BattleRoundParticipant FindSubmittedMage(IReadOnlyList<BattleRoundParticipant> participants)
    {
        if (participants == null)
            return null;

        for (int i = 0; i < participants.Count; i++)
        {
            BattleRoundParticipant participant = participants[i];
            if (participant != null && IsMage(participant.Player))
                return participant;
        }

        return null;
    }

    private static bool IsNumberDuplicated(RoundContext context, int number)
    {
        return context != null
            && context.NumberGroups.TryGetValue(number, out List<int> group)
            && group.Count >= 2;
    }
}
