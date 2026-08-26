using System.Collections.Generic;
using UnityEngine;

public static class BattleCurrencyUtility
{
    public static readonly BattleCrystalColor[] AllColors =
    {
        BattleCrystalColor.Red,
        BattleCrystalColor.Green,
        BattleCrystalColor.Blue
    };

    public static int GetTotalCrystals(NetworkPlayerState player)
    {
        if (player == null)
            return 0;

        return Mathf.Max(0, player.CrystalRed.Value)
            + Mathf.Max(0, player.CrystalBlue.Value)
            + Mathf.Max(0, player.CrystalGreen.Value);
    }

    public static List<BattleCrystalColor> GetMostOwnedColors(NetworkPlayerState player)
    {
        List<BattleCrystalColor> result = new List<BattleCrystalColor>();
        if (player == null)
            return result;

        int maxAmount = 0;
        for (int i = 0; i < AllColors.Length; i++)
            maxAmount = Mathf.Max(maxAmount, player.GetCrystalAmount(AllColors[i]));

        if (maxAmount <= 0)
            return result;

        for (int i = 0; i < AllColors.Length; i++)
        {
            BattleCrystalColor color = AllColors[i];
            if (player.GetCrystalAmount(color) == maxAmount)
                result.Add(color);
        }

        return result;
    }

    public static List<BattleCrystalColor> GetLeastOwnedColors(NetworkPlayerState player)
    {
        List<BattleCrystalColor> result = new List<BattleCrystalColor>();
        if (player == null)
            return result;

        int minAmount = int.MaxValue;
        for (int i = 0; i < AllColors.Length; i++)
            minAmount = Mathf.Min(minAmount, player.GetCrystalAmount(AllColors[i]));

        for (int i = 0; i < AllColors.Length; i++)
        {
            BattleCrystalColor color = AllColors[i];
            if (player.GetCrystalAmount(color) == minAmount)
                result.Add(color);
        }

        return result;
    }

    public static List<BattleCrystalColor> GetAvailableInsuranceColors(NetworkSessionState session)
    {
        List<BattleCrystalColor> result = new List<BattleCrystalColor>();
        if (session == null)
            return result;

        for (int i = 0; i < AllColors.Length; i++)
        {
            BattleCrystalColor color = AllColors[i];
            if (session.GetInsuranceAmount(color) > 0)
                result.Add(color);
        }

        return result;
    }

    public static BattleCrystalColor PickRandom(IReadOnlyList<BattleCrystalColor> colors, BattleCrystalColor fallback)
    {
        if (colors == null || colors.Count == 0)
            return fallback;

        return colors[Random.Range(0, colors.Count)];
    }

    public static void AddOne(NetworkPlayerState player, BattleCrystalColor color)
    {
        if (player == null)
            return;

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

    public static bool RemoveOne(NetworkPlayerState player, BattleCrystalColor color)
    {
        if (player == null || player.GetCrystalAmount(color) <= 0)
            return false;

        player.ServerSetCrystalAmount(color, player.GetCrystalAmount(color) - 1);
        return true;
    }

    public static int RemoveAll(NetworkPlayerState player, BattleCrystalColor color)
    {
        return player != null ? player.ServerRemoveAllCrystals(color) : 0;
    }
}
