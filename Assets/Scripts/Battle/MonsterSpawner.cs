using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class MonsterPrefabEntry
{
    public string prefabName;
    public GameObject prefab;
}

public class MonsterSpawner : MonoBehaviour
{
#if UNITY_EDITOR
    private const string DefaultMonsterPrefabFolder = "Assets/Prefab/Monsters Prefabs";
#endif

    [Header("몬스터 데이터")]
    [SerializeField] private MonsterTable monsterTable;
    [SerializeField] private MonsterPrefabEntry[] monsterPrefabs;

    [Header("소환 위치")]
    [SerializeField] private Transform monsterSpawnPoint;
    [SerializeField] private string spawnPointObjectName = "Monster Spawn Point";
    [SerializeField] private Transform spawnedMonsterParent;
    [SerializeField] private bool removePreviousBeforeSpawn = true;

    [Header("프리펩 내부 연결")]
    [SerializeField] private string hpTextObjectName = "HP Text";

    private GameObject currentMonsterInstance;
    private MonsterData currentMonsterData;
    private MonsterBattleState currentMonsterState;
    private NetworkMonsterState currentNetworkMonsterState;

    public MonsterTable MonsterTable => monsterTable;
    public GameObject CurrentMonsterInstance => currentMonsterInstance;
    public MonsterData CurrentMonsterData => currentMonsterData;
    public MonsterBattleState CurrentMonsterState => currentMonsterState;
    public NetworkMonsterState CurrentNetworkMonsterState => currentNetworkMonsterState;
    public bool HasSpawnedMonster => currentMonsterInstance != null;

    private void Awake()
    {
        CacheSceneReferences();
        EnsureMonsterTableLoaded();
    }

    private void OnValidate()
    {
        CacheSceneReferences();
#if UNITY_EDITOR
        AutoFillMonsterPrefabs(false);
#endif
    }

    public bool SpawnMonster(string monsterId)
    {
        CacheSceneReferences();
        EnsureMonsterTableLoaded();

        // 외부에서는 id만 넘겨도 현재 로드된 테이블에서 실제 몬스터 데이터를 찾을 수 있게 한다.
        if (monsterTable == null)
        {
            Debug.LogWarning("MonsterSpawner: MonsterTable 참조가 없습니다.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(monsterId))
        {
            Debug.LogWarning("MonsterSpawner: 소환할 몬스터 id가 비어 있습니다.");
            return false;
        }

        bool foundMonster = monsterTable.TryGetMonster(monsterId, out MonsterData monsterData);
        if (!foundMonster)
        {
            monsterTable.LoadMonsterTable();
            foundMonster = monsterTable.TryGetMonster(monsterId, out monsterData);
        }

        if (!foundMonster || monsterData == null)
        {
            Debug.LogWarning($"MonsterSpawner: 로드된 몬스터 테이블에서 '{monsterId}'를 찾지 못했습니다.");
            return false;
        }

        return SpawnMonster(monsterData);
    }

    public bool SpawnMonster(MonsterData monsterData)
    {
        CacheSceneReferences();

        // 같은 스포너가 관리하는 몬스터는 한 마리로 유지해서 제거 버튼과 상태가 어긋나지 않게 한다.
        if (monsterData == null)
        {
            Debug.LogWarning("MonsterSpawner: 소환할 몬스터 데이터가 없습니다.");
            return false;
        }

        if (currentMonsterInstance != null)
        {
            if (removePreviousBeforeSpawn)
            {
                RemoveCurrentMonster();
            }
            else
            {
                Debug.LogWarning("MonsterSpawner: 이미 소환된 몬스터가 있어 새 몬스터를 소환하지 않았습니다.");
                return false;
            }
        }

        if (!TryGetMonsterPrefab(monsterData.prefab_name, out GameObject prefab))
            return false;

        if (!TryGetSpawnPoint(out Transform spawnPoint))
            return false;

        bool useNetworkSpawn = ShouldUseNetworkSpawn();
        if (useNetworkSpawn && !CanRunServerNetworkSpawn())
        {
            Debug.LogWarning("MonsterSpawner: Network monster spawn must run on the server.");
            return false;
        }

        if (useNetworkSpawn && prefab.GetComponent<NetworkObject>() == null)
        {
            Debug.LogWarning($"MonsterSpawner: '{prefab.name}' prefab has no NetworkObject.");
            return false;
        }

        if (useNetworkSpawn && prefab.GetComponent<NetworkMonsterState>() == null)
        {
            Debug.LogWarning($"MonsterSpawner: '{prefab.name}' prefab has no NetworkMonsterState.");
            return false;
        }

        Transform parent = useNetworkSpawn ? null : spawnedMonsterParent != null ? spawnedMonsterParent : null;
        currentMonsterInstance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, parent);
        currentMonsterInstance.name = $"{prefab.name}_{monsterData.id}";
        currentMonsterData = monsterData;
        currentMonsterState = GetOrCreateMonsterState(currentMonsterInstance);
        currentNetworkMonsterState = currentMonsterInstance.GetComponent<NetworkMonsterState>();
        currentMonsterState.DeathAnimationFinished += HandleCurrentMonsterDeathAnimationFinished;

        if (currentNetworkMonsterState != null)
            currentNetworkMonsterState.InitializeBeforeSpawn(monsterData, hpTextObjectName);
        else
            currentMonsterState.Initialize(monsterData, hpTextObjectName);

        if (useNetworkSpawn)
            currentMonsterInstance.GetComponent<NetworkObject>().Spawn();

        Debug.Log($"MonsterSpawner: {monsterData.monster_name}({monsterData.id}) 소환 완료");
        return true;
    }

    public bool ApplyDamageToCurrentMonster(int damage)
    {
        return ApplyDamageToCurrentMonster(damage, false);
    }

    public bool ApplyDamageToCurrentMonster(int damage, bool deferDeathAnimation)
    {
        if (!TryGetCurrentMonsterState(out MonsterBattleState state))
            return false;

        // 외부 전투 판정은 이 함수만 호출해도 현재 몬스터 HP와 애니메이션을 함께 갱신할 수 있다.
        bool applied = state.ApplyDamage(damage, deferDeathAnimation);
        SyncCurrentMonsterNetworkHp();
        return applied;
    }

    public bool HealCurrentMonster(int amount)
    {
        if (!TryGetCurrentMonsterState(out MonsterBattleState state))
            return false;

        bool healed = state.Heal(amount);
        SyncCurrentMonsterNetworkHp();
        return healed;
    }

    public bool SetCurrentMonsterHp(int hp)
    {
        if (!TryGetCurrentMonsterState(out MonsterBattleState state))
            return false;

        state.SetCurrentHp(hp);
        SyncCurrentMonsterNetworkHp();
        return true;
    }

    public bool ResetCurrentMonsterHp()
    {
        if (!TryGetCurrentMonsterState(out MonsterBattleState state))
            return false;

        state.ResetHp();
        SyncCurrentMonsterNetworkHp();
        return true;
    }

    public bool PlayCurrentMonsterPendingDeathAnimation()
    {
        if (!TryGetCurrentMonsterState(out MonsterBattleState state))
            return false;

        if (currentNetworkMonsterState != null && CanRunServerNetworkSpawn())
            currentNetworkMonsterState.ServerPlayDeathAnimation();
        else
            state.PlayPendingDeathAnimation();

        return true;
    }

    public bool CanSpawnMonster(MonsterData monsterData)
    {
        if (monsterData == null)
            return false;

        if (!TryGetMonsterPrefab(monsterData.prefab_name, out GameObject prefab, false))
            return false;

        if (ShouldUseNetworkSpawn())
            return prefab.GetComponent<NetworkObject>() != null && prefab.GetComponent<NetworkMonsterState>() != null;

        return true;
    }

    public bool TryGetCurrentMonsterState(out MonsterBattleState state)
    {
        state = currentMonsterState;

        if (state == null && currentMonsterInstance != null)
        {
            state = currentMonsterInstance.GetComponent<MonsterBattleState>();
            currentMonsterState = state;
        }

        return state != null;
    }

    public bool RemoveCurrentMonster()
    {
        if (currentMonsterInstance == null)
        {
            currentMonsterData = null;
            currentMonsterState = null;
            currentNetworkMonsterState = null;
            return false;
        }

        // 죽음 애니메이션의 투명화와 실제 오브젝트 제거는 분리해서 관리한다.
        GameObject target = currentMonsterInstance;
        if (currentMonsterState != null)
            currentMonsterState.DeathAnimationFinished -= HandleCurrentMonsterDeathAnimationFinished;

        currentMonsterInstance = null;
        currentMonsterData = null;
        currentMonsterState = null;
        currentNetworkMonsterState = null;

        NetworkObject networkObject = target.GetComponent<NetworkObject>();
        if (Application.isPlaying)
        {
            if (networkObject != null && networkObject.IsSpawned && CanRunServerNetworkSpawn())
                networkObject.Despawn(true);
            else
                Destroy(target);
        }
        else
            DestroyImmediate(target);

        Debug.Log("MonsterSpawner: 소환된 몬스터를 제거했습니다.");
        return true;
    }

    private void HandleCurrentMonsterDeathAnimationFinished(MonsterBattleState finishedState)
    {
        if (finishedState == null || finishedState != currentMonsterState)
            return;

        // 죽음 애니메이션을 끝까지 보여준 뒤 스포너가 소유한 현재 몬스터 제거 흐름으로 정리한다.
        RemoveCurrentMonster();
    }

    public void CacheSceneReferences()
    {
        if (monsterTable == null)
            monsterTable = FindAnyObjectByType<MonsterTable>();

        if (monsterSpawnPoint == null && !string.IsNullOrWhiteSpace(spawnPointObjectName))
        {
            GameObject spawnPointObject = GameObject.Find(spawnPointObjectName);
            if (spawnPointObject != null)
                monsterSpawnPoint = spawnPointObject.transform;
        }
    }

#if UNITY_EDITOR
    public bool AutoFillMonsterPrefabs(bool forceRefresh)
    {
        if (!forceRefresh && HasUsableMonsterPrefabEntries())
            return false;

        string[] searchFolders = AssetDatabase.IsValidFolder(DefaultMonsterPrefabFolder)
            ? new[] { DefaultMonsterPrefabFolder }
            : new[] { "Assets" };

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", searchFolders);
        List<MonsterPrefabEntry> foundEntries = new List<MonsterPrefabEntry>();

        // 현재 프로젝트의 몬스터 프리펩 에셋을 이름 기준으로 등록해 JSON의 prefab_name과 맞춘다.
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
                continue;

            foundEntries.Add(new MonsterPrefabEntry
            {
                prefabName = prefab.name,
                prefab = prefab
            });
        }

        foundEntries.Sort((a, b) => string.Compare(a.prefabName, b.prefabName, StringComparison.OrdinalIgnoreCase));

        if (foundEntries.Count == 0)
            return false;

        if (HasSameMonsterPrefabEntries(foundEntries))
            return false;

        monsterPrefabs = foundEntries.ToArray();
        return true;
    }

    private bool HasUsableMonsterPrefabEntries()
    {
        if (monsterPrefabs == null)
            return false;

        for (int i = 0; i < monsterPrefabs.Length; i++)
        {
            MonsterPrefabEntry entry = monsterPrefabs[i];
            if (entry != null && entry.prefab != null)
                return true;
        }

        return false;
    }

    private bool HasSameMonsterPrefabEntries(List<MonsterPrefabEntry> foundEntries)
    {
        if (monsterPrefabs == null || monsterPrefabs.Length != foundEntries.Count)
            return false;

        for (int i = 0; i < foundEntries.Count; i++)
        {
            MonsterPrefabEntry current = monsterPrefabs[i];
            MonsterPrefabEntry found = foundEntries[i];

            if (current == null || current.prefab != found.prefab || current.prefabName != found.prefabName)
                return false;
        }

        return true;
    }
#endif

    private void EnsureMonsterTableLoaded()
    {
        if (monsterTable == null)
            return;

        // 에디터 인스펙터 버튼처럼 Awake가 보장되지 않는 경로에서도 테이블을 준비한다.
        if (monsterTable.Monsters == null || monsterTable.Monsters.Count == 0)
            monsterTable.LoadMonsterTable();
    }

    private bool TryGetMonsterPrefab(string prefabName, out GameObject prefab, bool logWarnings = true)
    {
        prefab = null;

        if (string.IsNullOrWhiteSpace(prefabName))
        {
            if (logWarnings)
                Debug.LogWarning("MonsterSpawner: 몬스터 데이터의 prefab_name이 비어 있습니다.");

            return false;
        }

        if (monsterPrefabs != null)
        {
            for (int i = 0; i < monsterPrefabs.Length; i++)
            {
                MonsterPrefabEntry entry = monsterPrefabs[i];
                if (entry == null || entry.prefab == null)
                    continue;

                string registeredName = string.IsNullOrWhiteSpace(entry.prefabName)
                    ? entry.prefab.name
                    : entry.prefabName;

                if (!string.Equals(registeredName, prefabName, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(entry.prefab.name, prefabName, StringComparison.OrdinalIgnoreCase))
                    continue;

                prefab = entry.prefab;
                return true;
            }
        }

        if (logWarnings)
            Debug.LogWarning($"MonsterSpawner: prefab_name '{prefabName}'에 해당하는 프리펩 참조가 없습니다.");

        return false;
    }

    private bool TryGetSpawnPoint(out Transform spawnPoint)
    {
        spawnPoint = monsterSpawnPoint;

        if (spawnPoint != null)
            return true;

        if (!string.IsNullOrWhiteSpace(spawnPointObjectName))
        {
            GameObject spawnPointObject = GameObject.Find(spawnPointObjectName);
            if (spawnPointObject != null)
            {
                monsterSpawnPoint = spawnPointObject.transform;
                spawnPoint = monsterSpawnPoint;
                return true;
            }
        }

        Debug.LogWarning($"MonsterSpawner: 씬에서 '{spawnPointObjectName}' 스폰 포인트를 찾지 못했습니다.");
        return false;
    }

    private MonsterBattleState GetOrCreateMonsterState(GameObject monsterInstance)
    {
        MonsterBattleState state = monsterInstance.GetComponent<MonsterBattleState>();
        if (state == null)
            state = monsterInstance.AddComponent<MonsterBattleState>();

        return state;
    }

    private void SyncCurrentMonsterNetworkHp()
    {
        if (currentNetworkMonsterState == null)
            return;

        currentNetworkMonsterState.ServerRefreshHpFromState();
    }

    private bool ShouldUseNetworkSpawn()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return Application.isPlaying && manager != null && manager.IsListening;
    }

    private bool CanRunServerNetworkSpawn()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager == null || !manager.IsListening || manager.IsServer;
    }
}
