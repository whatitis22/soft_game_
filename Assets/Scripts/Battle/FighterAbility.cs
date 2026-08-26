using System.Collections.Generic;
using UnityEngine;

public sealed class FighterRoundState
{
    public int HarmonyNumber;
    public int ConflictNumber;
    public bool HarmonyTriggered;
    public bool ConflictTriggered;
    public bool ConflictRewardTriggered;
}

public static class FighterAbility
{
    public static bool IsFighter(NetworkPlayerState player)
    {
        return player != null && player.Job.Value == JobType.Fighter;
    }

    public static FighterRoundState GenerateForRound(NetworkPlayerState fighter)
    {
        FighterRoundState state = new FighterRoundState();
        if (!IsFighter(fighter))
            return state;

        if (!BattleGimmickDebugSettings.FighterTopCommandEnabled)
            return state;

        List<int> available = GetAvailableNumbers(fighter);
        if (available.Count == 0)
            return state;

        state.HarmonyNumber = PickAndRemove(available);
        state.ConflictNumber = available.Count > 0 ? PickAndRemove(available) : 0;
        return state;
    }

    public static void ApplyDamageModifier(FighterRoundState state, DamageContext context)
    {
        if (state == null || context == null || context.Participant == null)
            return;

        if (!IsFighter(context.Participant.Player))
            return;

        if (!BattleGimmickDebugSettings.FighterTopCommandEnabled)
            return;

        if (BattleGimmickDebugSettings.FighterHarmonyEnabled && context.Participant.CardValue == state.HarmonyNumber)
        {
            state.HarmonyTriggered = true;
            context.FinalDamage += 1;
        }
        else if (BattleGimmickDebugSettings.FighterConflictEnabled && context.Participant.CardValue == state.ConflictNumber)
        {
            state.ConflictTriggered = true;
            context.FinalDamage = Mathf.Max(0, context.FinalDamage - 1);
        }
    }

    public static void ApplyPostAttackRewardModifier(
        FighterRoundState state,
        MonsterBattleState monsterState,
        IReadOnlyList<BattleRoundParticipant> participants)
    {
        if (state == null || !state.ConflictTriggered || monsterState == null || monsterState.IsDead)
            return;

        if (!BattleGimmickDebugSettings.FighterTopCommandEnabled)
            return;

        if (!BattleGimmickDebugSettings.FighterConflictEnabled)
            return;

        BattleRoundParticipant fighter = FindFighter(participants);
        if (fighter != null)
            fighter.RewardPriority = Mathf.Max(0, fighter.CardValue - 1);
    }

    public static void GrantLuckyReward(FighterRoundState state, IReadOnlyList<BattleRoundParticipant> participants)
    {
        if (state == null || !state.ConflictTriggered || state.ConflictRewardTriggered)
            return;

        if (!BattleGimmickDebugSettings.FighterTopCommandEnabled)
            return;

        if (!BattleGimmickDebugSettings.FighterLuckyRewardEnabled)
            return;

        BattleRoundParticipant fighter = FindFighter(participants);
        if (fighter == null || !fighter.ReceivedBaseReward)
            return;

        List<BattleCrystalColor> least = BattleCurrencyUtility.GetLeastOwnedColors(fighter.Player);
        BattleCrystalColor color = BattleCurrencyUtility.PickRandom(least, BattleCrystalColor.Red);
        BattleCurrencyUtility.AddOne(fighter.Player, color);
        DefenderAbility.RecordReward(fighter.Player);
        state.ConflictRewardTriggered = true;
    }

    public static BattleRoundParticipant FindFighter(IReadOnlyList<BattleRoundParticipant> participants)
    {
        if (participants == null)
            return null;

        for (int i = 0; i < participants.Count; i++)
        {
            BattleRoundParticipant participant = participants[i];
            if (participant != null && IsFighter(participant.Player))
                return participant;
        }

        return null;
    }

    private static List<int> GetAvailableNumbers(NetworkPlayerState player)
    {
        List<int> available = new List<int>();
        if (player == null)
            return available;

        for (int number = 1; number <= 6; number++)
        {
            if (player.HasNumberCard(number))
                available.Add(number);
        }

        return available;
    }

    private static int PickAndRemove(List<int> available)
    {
        int index = Random.Range(0, available.Count);
        int value = available[index];
        available.RemoveAt(index);
        return value;
    }
}
