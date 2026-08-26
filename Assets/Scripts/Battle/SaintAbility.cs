using System.Collections.Generic;
using UnityEngine;

public sealed class SaintRoundState
{
    public bool RevelationAvailable;
    public bool RevelationUsed;
    public int RevelationNumber;
    public bool AmenTriggered;
    public bool DamnGodTriggered;
}

public static class SaintAbility
{
    public static bool IsSaint(NetworkPlayerState player)
    {
        return player != null && player.Job.Value == JobType.Saint;
    }

    public static bool CanUseRevelation(NetworkPlayerState saint, SaintRoundState state)
    {
        return IsSaint(saint)
            && state != null
            && BattleGimmickDebugSettings.SaintRevelationEnabled
            && !state.RevelationUsed
            && BattleCurrencyUtility.GetTotalCrystals(saint) >= 1
            && saint.SubmittedCard.Value < 1;
    }

    public static bool TryUseRevelation(NetworkPlayerState saint, SaintRoundState state)
    {
        if (!CanUseRevelation(saint, state))
            return false;

        List<int> available = GetAvailableNumbers(saint);
        if (available.Count == 0)
            return false;

        int number = available[Random.Range(0, available.Count)];
        if (!saint.ServerForceSubmitNumberCard(number, true))
            return false;

        List<BattleCrystalColor> mostOwned = BattleCurrencyUtility.GetMostOwnedColors(saint);
        if (mostOwned.Count == 0)
            return false;

        BattleCrystalColor costColor = BattleCurrencyUtility.PickRandom(mostOwned, BattleCrystalColor.Red);
        if (!BattleCurrencyUtility.RemoveOne(saint, costColor))
            return false;

        state.RevelationUsed = true;
        state.RevelationNumber = number;
        return true;
    }

    public static void ResolveRevealResult(SaintRoundState state, RoundContext context)
    {
        if (state == null || !state.RevelationUsed || context == null)
            return;

        bool duplicated = context.NumberGroups.TryGetValue(state.RevelationNumber, out List<int> group)
            && group.Count > 1;

        state.AmenTriggered = !duplicated && BattleGimmickDebugSettings.SaintAmenEnabled;
        state.DamnGodTriggered = duplicated && BattleGimmickDebugSettings.SaintDamnGodEnabled;
    }

    public static void ResolveAmenReward(
        SaintRoundState state,
        NetworkSessionState session,
        NetworkPlayerState saint)
    {
        if (state == null || !state.AmenTriggered || saint == null || session == null)
            return;

        if (!BattleGimmickDebugSettings.SaintAmenEnabled)
            return;

        if (session.GetTotalInsuranceCrystals() <= 0)
        {
            // TODO: Treasury currency choice UI. The rule requires player choice, so no automatic treasury grant here.
            return;
        }

        for (int i = 0; i < 2; i++)
        {
            List<BattleCrystalColor> leastOwned = BattleCurrencyUtility.GetLeastOwnedColors(saint);
            if (!TryFindAvailableInsuranceColor(session, leastOwned, out BattleCrystalColor selected))
                break;

            if (!session.ServerTryRemoveOneInsuranceCrystal(selected))
                break;

            BattleCurrencyUtility.AddOne(saint, selected);
            DefenderAbility.RecordReward(saint);
        }
    }

    public static void ApplyMonsterAttackTargetOverride(
        SaintRoundState state,
        IReadOnlyList<BattleRoundParticipant> participants,
        MonsterAttackContext context)
    {
        if (state == null || !state.DamnGodTriggered || context == null)
            return;

        if (!BattleGimmickDebugSettings.SaintDamnGodEnabled)
            return;

        BattleRoundParticipant saint = FindSaint(participants);
        if (saint != null)
            context.AddForcedTarget(saint, ForcedTargetType.Replace);
    }

    public static BattleRoundParticipant FindSaint(IReadOnlyList<BattleRoundParticipant> participants)
    {
        if (participants == null)
            return null;

        for (int i = 0; i < participants.Count; i++)
        {
            BattleRoundParticipant participant = participants[i];
            if (participant != null && IsSaint(participant.Player))
                return participant;
        }

        return null;
    }

    private static bool TryFindAvailableInsuranceColor(
        NetworkSessionState session,
        IReadOnlyList<BattleCrystalColor> preferredColors,
        out BattleCrystalColor selected)
    {
        selected = BattleCrystalColor.Red;
        if (session == null || preferredColors == null)
            return false;

        for (int i = 0; i < preferredColors.Count; i++)
        {
            BattleCrystalColor color = preferredColors[i];
            if (session.GetInsuranceAmount(color) > 0)
            {
                selected = color;
                return true;
            }
        }

        return false;
    }

    private static List<int> GetAvailableNumbers(NetworkPlayerState player)
    {
        List<int> available = new List<int>();
        for (int number = 1; number <= 6; number++)
        {
            if (player.HasNumberCard(number))
                available.Add(number);
        }

        return available;
    }
}
