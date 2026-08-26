using System.Collections.Generic;
using UnityEngine;

public static class DefenderAbility
{
    public static bool IsDefender(NetworkPlayerState player)
    {
        return player != null && player.Job.Value == JobType.Defender;
    }

    public static bool CanUseLoanCard(NetworkPlayerState defender)
    {
        return IsDefender(defender)
            && BattleGimmickDebugSettings.DefenderLoanEnabled
            && defender.SubmittedCard.Value < 1
            && GetAvailableNumbers(defender).Count > 0;
    }

    public static bool TryUseLoanCard(NetworkPlayerState defender)
    {
        if (!CanUseLoanCard(defender))
            return false;

        List<int> available = GetAvailableNumbers(defender);
        int number = available[Random.Range(0, available.Count)];
        return defender.ServerForceSubmitNumberCard(number, true);
    }

    public static void SetCustomer(NetworkPlayerState defender, NetworkPlayerState customer)
    {
        if (!IsDefender(defender) || customer == null || customer == defender)
            return;

        customer.ServerSetDefenderCustomer(true);
        customer.ServerSetDefenderActiveProtector(defender.PlayerIndex.Value);
    }

    public static void ApplyPendingDebtAtRoundStart(IReadOnlyList<NetworkPlayerState> players)
    {
        if (players == null)
            return;

        for (int i = 0; i < players.Count; i++)
        {
            NetworkPlayerState player = players[i];
            if (player == null)
                continue;

            player.ServerClearDefenderRoundProtection();
            int pendingDebt = player.DefenderPendingDebt.Value;
            if (pendingDebt > 0)
            {
                player.ServerAddDefenderDebt(pendingDebt);
                player.ServerClearDefenderPendingDebt();
            }
        }
    }

    public static void ResetStageDebtBlocks(IReadOnlyList<NetworkPlayerState> players)
    {
        if (players == null)
            return;

        for (int i = 0; i < players.Count; i++)
            players[i]?.ServerSetDefenderDebtBlockedThisStage(false);
    }

    public static void ApplyMonsterAttackTargetOverride(
        NetworkPlayerState defender,
        MonsterAttackContext context)
    {
        if (!IsDefender(defender) || context == null || !BattleGimmickDebugSettings.DefenderLoanEnabled)
            return;

        bool defenderWillProtect = false;
        for (int i = 0; i < context.OriginalTargets.Count; i++)
        {
            NetworkPlayerState customer = context.OriginalTargets[i]?.Player;
            if (customer == null || !customer.IsDefenderCustomer.Value)
                continue;

            if (customer.DefenderActiveProtectorIndex.Value != defender.PlayerIndex.Value)
                continue;

            customer.ServerAddDefenderPendingDebt(1);
            defenderWillProtect = true;
        }

        if (!defenderWillProtect)
            return;

        BattleRoundParticipant defenderParticipant = FindParticipant(context.OriginalTargets, defender.PlayerIndex.Value);
        if (defenderParticipant == null)
            defenderParticipant = new BattleRoundParticipant(defender, Mathf.Max(1, defender.SubmittedCard.Value));

        context.AddForcedTarget(defenderParticipant, ForcedTargetType.Replace);
    }

    public static void ResolveUnprotectedCustomerHits(IReadOnlyList<BattleRoundParticipant> hitTargets)
    {
        if (hitTargets == null || !BattleGimmickDebugSettings.DefenderBadDebtEnabled)
            return;

        for (int i = 0; i < hitTargets.Count; i++)
        {
            NetworkPlayerState hitPlayer = hitTargets[i]?.Player;
            if (hitPlayer == null || !hitPlayer.IsDefenderCustomer.Value)
                continue;

            ClearAllCustomerDebt();
            hitPlayer.ServerSetDefenderDebtBlockedThisStage(true);
        }
    }

    public static void RecordReward(NetworkPlayerState player)
    {
        if (player == null || !player.IsDefenderCustomer.Value)
            return;

        if (!BattleGimmickDebugSettings.DefenderRewardDebtEnabled)
            return;

        player.ServerAddDefenderDebt(1);
    }

    public static List<NetworkPlayerState> GetHighestDebtCustomers()
    {
        List<NetworkPlayerState> result = new List<NetworkPlayerState>();
        int highestDebt = 0;

        foreach (NetworkPlayerState player in NetworkPlayerRegistry.GetSpawnedPlayers())
        {
            if (player == null || !player.IsDefenderCustomer.Value || player.DefenderDebt.Value <= 0)
                continue;

            int debt = player.DefenderDebt.Value;
            if (debt > highestDebt)
            {
                highestDebt = debt;
                result.Clear();
            }

            if (debt == highestDebt)
                result.Add(player);
        }

        return result;
    }

    public static void SettleCustomer(NetworkPlayerState defender, NetworkPlayerState customer)
    {
        if (!IsDefender(defender) || customer == null || !customer.IsDefenderCustomer.Value)
            return;

        int remaining = Mathf.Max(0, customer.DefenderDebt.Value);
        while (remaining > 0 && BattleCurrencyUtility.GetTotalCrystals(customer) > 0)
        {
            if (!TryMovePreferredCrystal(customer, defender))
                break;

            remaining--;
        }

        customer.ServerClearDefenderCustomerState(true);
    }

    public static NetworkPlayerState FindDefender()
    {
        foreach (NetworkPlayerState player in NetworkPlayerRegistry.GetSpawnedPlayers())
        {
            if (IsDefender(player))
                return player;
        }

        return null;
    }

    private static bool TryMovePreferredCrystal(NetworkPlayerState from, NetworkPlayerState to)
    {
        List<BattleCrystalColor> preferred = BattleCurrencyUtility.GetLeastOwnedColors(to);
        for (int i = 0; i < preferred.Count; i++)
        {
            BattleCrystalColor color = preferred[i];
            if (from.GetCrystalAmount(color) <= 0)
                continue;

            BattleCurrencyUtility.RemoveOne(from, color);
            BattleCurrencyUtility.AddOne(to, color);
            return true;
        }

        for (int i = 0; i < BattleCurrencyUtility.AllColors.Length; i++)
        {
            BattleCrystalColor color = BattleCurrencyUtility.AllColors[i];
            if (from.GetCrystalAmount(color) <= 0)
                continue;

            BattleCurrencyUtility.RemoveOne(from, color);
            BattleCurrencyUtility.AddOne(to, color);
            return true;
        }

        return false;
    }

    private static void ClearAllCustomerDebt()
    {
        foreach (NetworkPlayerState player in NetworkPlayerRegistry.GetSpawnedPlayers())
        {
            if (player == null || !player.IsDefenderCustomer.Value)
                continue;

            player.ServerSetDefenderDebt(0);
            player.ServerClearDefenderPendingDebt();
        }
    }

    private static BattleRoundParticipant FindParticipant(
        IReadOnlyList<BattleRoundParticipant> participants,
        int playerIndex)
    {
        if (participants == null)
            return null;

        for (int i = 0; i < participants.Count; i++)
        {
            BattleRoundParticipant participant = participants[i];
            if (participant != null && participant.PlayerIndex == playerIndex)
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
}
