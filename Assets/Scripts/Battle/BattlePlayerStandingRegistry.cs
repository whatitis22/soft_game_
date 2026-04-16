using System.Collections.Generic;
using UnityEngine;
using static JobDefinition;

public static class BattlePlayerStandingRegistry
{
    private static readonly Dictionary<string, BattlePlayerStanding> byKey
        = new Dictionary<string, BattlePlayerStanding>();

    public static string ToKey(int playerIndex)
    {
        return $"p{playerIndex + 1}";
    }

    public static void Register(BattlePlayerStanding standing)
    {
        if (standing == null || string.IsNullOrEmpty(standing.PlayerKey))
            return;

        byKey[standing.PlayerKey] = standing;
    }

    public static void Unregister(BattlePlayerStanding standing)
    {
        if (standing == null || string.IsNullOrEmpty(standing.PlayerKey))
            return;

        if (byKey.TryGetValue(standing.PlayerKey, out var current) && current == standing)
            byKey.Remove(standing.PlayerKey);
    }

    public static bool TryGet(string playerKey, out BattlePlayerStanding standing)
    {
        return byKey.TryGetValue(playerKey, out standing);
    }

    public static bool TryGet(int playerIndex, out BattlePlayerStanding standing)
    {
        return byKey.TryGetValue(ToKey(playerIndex), out standing);
    }

    public static BattlePlayerStanding Get(string playerKey)
    {
        byKey.TryGetValue(playerKey, out var standing);
        return standing;
    }

    public static BattlePlayerStanding Get(int playerIndex)
    {
        byKey.TryGetValue(ToKey(playerIndex), out var standing);
        return standing;
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

    public static void Play(int playerIndex, BattleStandingAction action)
    {
        Play(ToKey(playerIndex), action);
    }

    public static void PlayHit(string playerKey) => Play(playerKey, BattleStandingAction.Attack);
    public static void PlayVictory(string playerKey) => Play(playerKey, BattleStandingAction.Victory);
    public static void PlayDefeat(string playerKey) => Play(playerKey, BattleStandingAction.Defeat);
    public static void PlayIdle(string playerKey) => Play(playerKey, BattleStandingAction.Idle);

    public static void PlayHit(int playerIndex) => Play(playerIndex, BattleStandingAction.Attack);
    public static void PlayVictory(int playerIndex) => Play(playerIndex, BattleStandingAction.Victory);
    public static void PlayDefeat(int playerIndex) => Play(playerIndex, BattleStandingAction.Defeat);
    public static void PlayIdle(int playerIndex) => Play(playerIndex, BattleStandingAction.Idle);
}