using System.Collections.Generic;

public sealed class BerserkerStageState
{
    public int OneMoreStack;
}

public static class BerserkerAbility
{
    public static bool IsBerserker(NetworkPlayerState player)
    {
        return player != null && player.Job.Value == JobType.Butcher;
    }

    public static void ApplyOneMore(BerserkerStageState state, DamageContext context)
    {
        if (state == null || context == null || context.Participant == null)
            return;

        if (!BattleGimmickDebugSettings.BerserkerOneMoreEnabled)
            return;

        if (!IsBerserker(context.Participant.Player))
            return;

        context.FinalDamage += state.OneMoreStack;
    }

    public static void ResolveAfterSuccessfulAttack(
        BerserkerStageState state,
        BattleRoundParticipant participant,
        int calculatedDamage)
    {
        if (state == null || participant == null || !IsBerserker(participant.Player))
            return;

        if (!BattleGimmickDebugSettings.BerserkerOneMoreEnabled)
            return;

        state.OneMoreStack++;

        if (calculatedDamage >= 7 && BattleGimmickDebugSettings.BerserkerOverexcitedEnabled)
            ResolveOverexcited(state, participant.Player);
    }

    public static void ResetStage(BerserkerStageState state)
    {
        if (state != null)
            state.OneMoreStack = 0;
    }

    private static void ResolveOverexcited(BerserkerStageState state, NetworkPlayerState berserker)
    {
        state.OneMoreStack = 0;

        List<BattleCrystalColor> mostOwned = BattleCurrencyUtility.GetMostOwnedColors(berserker);
        if (mostOwned.Count == 0)
            return;

        BattleCrystalColor selected = BattleCurrencyUtility.PickRandom(mostOwned, BattleCrystalColor.Red);
        BattleCurrencyUtility.RemoveAll(berserker, selected);
    }
}
