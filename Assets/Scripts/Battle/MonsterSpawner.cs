using System;
using System.Collections.Generic;
using TMPro;
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

    public MonsterTable MonsterTable => monsterTable;
    public GameObject CurrentMonsterInstance => currentMonsterInstance;
    public MonsterData CurrentMonsterData => currentMonsterData;
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

        Transform parent = spawnedMonsterParent != null ? spawnedMonsterParent : null;
        currentMonsterInstance = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, parent);
        currentMonsterInstance.name = $"{prefab.name}_{monsterData.id}";
        currentMonsterData = monsterData;

        ApplyHpText(currentMonsterInstance, monsterData.HP);

        Debug.Log($"MonsterSpawner: {monsterData.monster_name}({monsterData.id}) 소환 완료");
        return true;
    }

    public bool RemoveCurrentMonster()
    {
        if (currentMonsterInstance == null)
        {
            currentMonsterData = null;
            return false;
        }

        // 죽음 애니메이션의 투명화와 실제 오브젝트 제거는 분리해서 관리한다.
        GameObject target = currentMonsterInstance;
        currentMonsterInstance = null;
        currentMonsterData = null;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);

        Debug.Log("MonsterSpawner: 소환된 몬스터를 제거했습니다.");
        return true;
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

    private bool TryGetMonsterPrefab(string prefabName, out GameObject prefab)
    {
        prefab = null;

        if (string.IsNullOrWhiteSpace(prefabName))
        {
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

    private void ApplyHpText(GameObject monsterInstance, int hp)
    {
        if (monsterInstance == null)
            return;

        // 프리펩 자식 이름이 HP Text인 경우를 우선하고, 없으면 첫 TMP_Text에라도 HP를 주입한다.
        TMP_Text[] textComponents = monsterInstance.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text hpText = null;

        for (int i = 0; i < textComponents.Length; i++)
        {
            TMP_Text textComponent = textComponents[i];
            if (textComponent != null && textComponent.gameObject.name == hpTextObjectName)
            {
                hpText = textComponent;
                break;
            }
        }

        if (hpText == null && textComponents.Length > 0)
            hpText = textComponents[0];

        if (hpText == null)
        {
            Debug.LogWarning($"{monsterInstance.name}: HP를 표시할 TMP_Text를 찾지 못했습니다.");
            return;
        }

        hpText.text = hp.ToString();
    }
}
