using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BattlePlayerSpawnData
{
    [Tooltip("0이면 p1, 1이면 p2, 2이면 p3 ...")]
    public int playerIndex;

    public JobDefinition jobDefinition;
}

public class BattlePlayerSlots : MonoBehaviour
{
    [Header("playerIndex와 1:1 대응")]
    [Tooltip("0번 슬롯 = p1 위치, 1번 슬롯 = p2 위치 ...")]
    [SerializeField] private Transform[] playerSlots;

    private readonly Dictionary<int, BattlePlayerStanding> spawnedStandings
        = new Dictionary<int, BattlePlayerStanding>();

    public IReadOnlyDictionary<int, BattlePlayerStanding> SpawnedStandings => spawnedStandings;

    public void Build(IReadOnlyList<BattlePlayerSpawnData> players)
    {
        ClearAll();

        if (players == null || players.Count == 0)
        {
            Debug.LogWarning("BattlePlayerSlots: 소환할 플레이어가 없습니다.");
            return;
        }

        for (int i = 0; i < players.Count; i++)
        {
            Spawn(players[i]);
        }
    }

    public void ClearAll()
    {
        foreach (var pair in spawnedStandings)
        {
            if (pair.Value != null)
                Destroy(pair.Value.gameObject);
        }

        spawnedStandings.Clear();
    }

    private void Spawn(BattlePlayerSpawnData data)
    {
        if (data == null)
            return;

        int playerIndex = data.playerIndex;

        if (playerIndex < 0 || playerIndex >= playerSlots.Length)
        {
            Debug.LogWarning($"BattlePlayerSlots: playerIndex {playerIndex} 에 대응되는 슬롯이 없습니다.");
            return;
        }

        if (data.jobDefinition == null)
        {
            Debug.LogWarning($"BattlePlayerSlots: p{playerIndex + 1} 의 JobDefinition이 비어 있습니다.");
            return;
        }

        GameObject prefab = data.jobDefinition.BattlePlayerStandingPrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"BattlePlayerSlots: {data.jobDefinition.name} 의 BattlePlayerStandingPrefab이 비어 있습니다.");
            return;
        }

        Transform slot = playerSlots[playerIndex];
        GameObject instance = Instantiate(prefab, slot.position, slot.rotation, slot);
        instance.name = $"BattleStanding_{BattlePlayerStandingRegistry.ToKey(playerIndex)}_{data.jobDefinition.name}";

        BattlePlayerStanding standing = instance.GetComponent<BattlePlayerStanding>();
        if (standing == null)
        {
            Debug.LogWarning($"{instance.name}: BattlePlayerStanding 컴포넌트가 없어 자동으로 추가했습니다. Animator 트리거명이 기본값과 같은지 확인하세요.");
            standing = instance.AddComponent<BattlePlayerStanding>();
        }

        standing.Initialize(playerIndex);
        spawnedStandings[playerIndex] = standing;
    }

    public bool TryGetStanding(int playerIndex, out BattlePlayerStanding standing)
    {
        return spawnedStandings.TryGetValue(playerIndex, out standing);
    }
}