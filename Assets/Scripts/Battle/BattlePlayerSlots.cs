using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[Serializable]
public class BattleStandingPrefabEntry
{
    public JobType jobType;
    public GameObject prefab;
}

public class BattlePlayerSlots : MonoBehaviour
{
    [Header("0번 = p1 위치, 1번 = p2 위치 ...")]
    [SerializeField] private Transform[] playerSlots;

    [Header("직업별 전투 스탠딩 프리펩")]
    [SerializeField] private BattleStandingPrefabEntry[] battleStandingPrefabs;

    [Header("자동 소환")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private float waitTimeout = 3f;

    private readonly Dictionary<int, BattlePlayerStanding> spawnedStandings
        = new Dictionary<int, BattlePlayerStanding>();

    public IReadOnlyDictionary<int, BattlePlayerStanding> SpawnedStandings => spawnedStandings;

    private IEnumerator Start()
    {
        if (!spawnOnStart)
            yield break;

        yield return SpawnWhenPlayersReady();
    }

    private IEnumerator SpawnWhenPlayersReady()
    {
        float elapsed = 0f;

        while (elapsed < waitTimeout)
        {
            NetworkPlayerState[] players =
                FindObjectsByType<NetworkPlayerState>(FindObjectsSortMode.None);

            if (HasSpawnablePlayers(players))
            {
                SpawnFromPlayers(players);
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogWarning("BattlePlayerSlots: 씬 안에서 유효한 NetworkPlayerState를 찾지 못했습니다.");
    }

    private bool HasSpawnablePlayers(NetworkPlayerState[] players)
    {
        if (players == null || players.Length == 0)
            return false;

        for (int i = 0; i < players.Length; i++)
        {
            if (IsSpawnable(players[i]))
                return true;
        }

        return false;
    }

    private bool IsSpawnable(NetworkPlayerState player)
    {
        if (player == null)
            return false;

        if (player.PlayerIndex.Value < 0)
            return false;

        if (player.Job.Value == JobType.None)
            return false;

        return true;
    }

    public void RespawnNow()
    {
        NetworkPlayerState[] players =
            FindObjectsByType<NetworkPlayerState>(FindObjectsSortMode.None);

        SpawnFromPlayers(players);
    }

    public void SpawnFromPlayers(NetworkPlayerState[] players)
    {
        ClearAll();

        if (players == null || players.Length == 0)
        {
            Debug.LogWarning("BattlePlayerSlots: 소환할 플레이어가 없습니다.");
            return;
        }

        List<NetworkPlayerState> validPlayers = new List<NetworkPlayerState>();

        for (int i = 0; i < players.Length; i++)
        {
            if (!IsSpawnable(players[i]))
                continue;

            validPlayers.Add(players[i]);
        }

        if (validPlayers.Count == 0)
        {
            Debug.LogWarning("BattlePlayerSlots: 아직 PlayerIndex / Job 데이터가 준비된 플레이어가 없습니다.");
            return;
        }

        validPlayers.Sort(CompareByPlayerIndex);

        for (int i = 0; i < validPlayers.Count; i++)
        {
            SpawnOne(validPlayers[i]);
        }
    }

    private int CompareByPlayerIndex(NetworkPlayerState a, NetworkPlayerState b)
    {
        return a.PlayerIndex.Value.CompareTo(b.PlayerIndex.Value);
    }

    private void SpawnOne(NetworkPlayerState player)
    {
        if (player == null)
            return;

        int playerIndex = player.PlayerIndex.Value;
        JobType jobType = player.Job.Value;

        if (playerIndex < 0 || playerIndex >= playerSlots.Length)
        {
            Debug.LogWarning($"BattlePlayerSlots: playerIndex {playerIndex} 에 해당하는 슬롯이 없습니다.");
            return;
        }

        if (!TryGetStandingPrefab(jobType, out GameObject prefab) || prefab == null)
        {
            Debug.LogWarning($"BattlePlayerSlots: 직업 '{jobType}' 에 대응되는 전투 스탠딩 프리펩이 없습니다.");
            return;
        }

        if (spawnedStandings.ContainsKey(playerIndex))
        {
            Debug.LogWarning($"BattlePlayerSlots: p{playerIndex + 1} 이 중복 감지되었습니다. 뒤의 항목은 무시합니다.");
            return;
        }

        Transform slot = playerSlots[playerIndex];

        GameObject instance = Instantiate(prefab, slot.position, slot.rotation, slot);
        instance.name = $"BattleStanding_p{playerIndex + 1}_{jobType}";

        BattlePlayerStanding standing = instance.GetComponent<BattlePlayerStanding>();
        if (standing == null)
        {
            Debug.LogWarning($"{instance.name}: BattlePlayerStanding 컴포넌트가 없어 자동 추가했습니다.");
            standing = instance.AddComponent<BattlePlayerStanding>();
        }

        ulong localClientId = ulong.MaxValue;
        ulong serverClientId = Unity.Netcode.NetworkManager.ServerClientId;

        if (Unity.Netcode.NetworkManager.Singleton != null)
        {
            localClientId = Unity.Netcode.NetworkManager.Singleton.LocalClientId;
        }

        bool isLocalPlayer = player.OwnerClientId == localClientId;
        bool isHostPlayer = player.OwnerClientId == serverClientId;
        string nickname = player.DisplayNickname.Value.ToString();

        standing.Initialize(playerIndex, nickname, isHostPlayer, isLocalPlayer);
        spawnedStandings[playerIndex] = standing;
    }

    private bool TryGetStandingPrefab(JobType jobType, out GameObject prefab)
    {
        if (battleStandingPrefabs != null)
        {
            for (int i = 0; i < battleStandingPrefabs.Length; i++)
            {
                BattleStandingPrefabEntry entry = battleStandingPrefabs[i];
                if (entry != null && entry.jobType == jobType)
                {
                    prefab = entry.prefab;
                    return prefab != null;
                }
            }
        }

        prefab = null;
        return false;
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

    public bool TryGetStanding(int playerIndex, out BattlePlayerStanding standing)
    {
        return spawnedStandings.TryGetValue(playerIndex, out standing);
    }
}