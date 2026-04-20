using System.Collections.Generic;
using UnityEngine;

public static class BattlePlayerStandingRegistry
{
    private static readonly Dictionary<int, BattlePlayerStanding> byIndex = new();
    private static readonly Dictionary<string, BattlePlayerStanding> byKey = new();

    public static string ToKey(int playerIndex)
    {
        return $"p{playerIndex + 1}";
    }

    public static void Register(BattlePlayerStanding standing)
    {
        if (standing == null)
            return;

        if (standing.PlayerIndex < 0)
            return;

        string key = standing.PlayerKey;
        if (string.IsNullOrEmpty(key))
            key = ToKey(standing.PlayerIndex);

        byIndex[standing.PlayerIndex] = standing;
        byKey[key] = standing;
    }

    public static void Unregister(BattlePlayerStanding standing)
    {
        if (standing == null)
            return;

        if (standing.PlayerIndex >= 0)
        {
            if (byIndex.TryGetValue(standing.PlayerIndex, out var currentByIndex) && currentByIndex == standing)
                byIndex.Remove(standing.PlayerIndex);
        }

        if (!string.IsNullOrEmpty(standing.PlayerKey))
        {
            if (byKey.TryGetValue(standing.PlayerKey, out var currentByKey) && currentByKey == standing)
                byKey.Remove(standing.PlayerKey);
        }
    }

    public static bool TryGet(int playerIndex, out BattlePlayerStanding standing)
    {
        return byIndex.TryGetValue(playerIndex, out standing);
    }

    public static bool TryGet(string playerKey, out BattlePlayerStanding standing)
    {
        return byKey.TryGetValue(playerKey, out standing);
    }

    public static BattlePlayerStanding Get(int playerIndex)
    {
        byIndex.TryGetValue(playerIndex, out var standing);
        return standing;
    }

    public static BattlePlayerStanding Get(string playerKey)
    {
        byKey.TryGetValue(playerKey, out var standing);
        return standing;
    }

    public static void Play(int playerIndex, BattleStandingAction action)
    {
        if (!byIndex.TryGetValue(playerIndex, out var standing) || standing == null)
        {
            Debug.LogWarning($"BattlePlayerStandingRegistry: p{playerIndex + 1} 을 찾지 못했습니다.");
            return;
        }

        standing.Play(action);
    }

    public static void Play(string playerKey, BattleStandingAction action)
    {
        if (!byKey.TryGetValue(playerKey, out var standing) || standing == null)
        {
            Debug.LogWarning($"BattlePlayerStandingRegistry: '{playerKey}' 를 찾지 못했습니다.");
            return;
        }

        standing.Play(action);
    }

    public static void PlayHit(int playerIndex) => Play(playerIndex, BattleStandingAction.Hit);
    public static void PlayVictory(int playerIndex) => Play(playerIndex, BattleStandingAction.Victory);
    public static void PlayDefeat(int playerIndex) => Play(playerIndex, BattleStandingAction.Defeat);
    public static void PlayIdle(int playerIndex) => Play(playerIndex, BattleStandingAction.Idle);

    public static void PlayHit(string playerKey) => Play(playerKey, BattleStandingAction.Hit);
    public static void PlayVictory(string playerKey) => Play(playerKey, BattleStandingAction.Victory);
    public static void PlayDefeat(string playerKey) => Play(playerKey, BattleStandingAction.Defeat);
    public static void PlayIdle(string playerKey) => Play(playerKey, BattleStandingAction.Idle);

    public static void PlayCustom(int playerIndex, string triggerName)
    {
        if (!byIndex.TryGetValue(playerIndex, out var standing) || standing == null)
        {
            Debug.LogWarning($"BattlePlayerStandingRegistry: p{playerIndex + 1} 을 찾지 못했습니다.");
            return;
        }

        standing.PlayCustom(triggerName);
    }

    public static void PlayCustom(string playerKey, string triggerName)
    {
        if (!byKey.TryGetValue(playerKey, out var standing) || standing == null)
        {
            Debug.LogWarning($"BattlePlayerStandingRegistry: '{playerKey}' 를 찾지 못했습니다.");
            return;
        }

        standing.PlayCustom(triggerName);
    }

    public static void Clear()
    {
        byIndex.Clear();
        byKey.Clear();
    }
}