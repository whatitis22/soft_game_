using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MonsterSpawnTester : MonoBehaviour
{
    [SerializeField] private MonsterSpawner spawner;
    [SerializeField] private MonsterTable monsterTable;
    [SerializeField] private int selectedMonsterIndex;

    private static readonly MonsterData[] EmptyMonsters = Array.Empty<MonsterData>();

    public MonsterSpawner Spawner
    {
        get
        {
            CacheSceneReferences();
            return spawner;
        }
    }

    public MonsterTable Table
    {
        get
        {
            CacheSceneReferences();
            return monsterTable;
        }
    }

    public int SelectedMonsterIndex
    {
        get => selectedMonsterIndex;
        set
        {
            selectedMonsterIndex = value;
            ClampSelectedMonsterIndex();
        }
    }

    public bool CanRemove => Spawner != null && Spawner.HasSpawnedMonster;

    private void Awake()
    {
        CacheSceneReferences();
        RefreshMonsterTable();
    }

    private void OnValidate()
    {
        CacheSceneReferences();
        ClampSelectedMonsterIndex();
    }

    public void RefreshMonsterTable()
    {
        CacheSceneReferences();

        // 인스펙터 테스트는 플레이 전에도 누를 수 있으므로 필요할 때 직접 JSON을 다시 읽는다.
        if (monsterTable != null)
            monsterTable.LoadMonsterTable();

        ClampSelectedMonsterIndex();
    }

    public IReadOnlyList<MonsterData> GetLoadedMonsters()
    {
        if (Table == null || Table.Monsters == null)
            return EmptyMonsters;

        return Table.Monsters;
    }

    public string[] GetMonsterOptions()
    {
        IReadOnlyList<MonsterData> monsters = GetLoadedMonsters();
        if (monsters.Count == 0)
            return Array.Empty<string>();

        string[] options = new string[monsters.Count];
        for (int i = 0; i < monsters.Count; i++)
        {
            MonsterData monster = monsters[i];
            options[i] = monster == null
                ? "<비어 있는 몬스터>"
                : monster.monster_name;
        }

        return options;
    }

    public bool SpawnSelectedMonster()
    {
        RefreshMonsterTable();

        IReadOnlyList<MonsterData> monsters = GetLoadedMonsters();
        if (monsters.Count == 0)
        {
            Debug.LogWarning("MonsterSpawnTester: 로드된 몬스터가 없습니다.");
            return false;
        }

        ClampSelectedMonsterIndex();

        // 선택된 데이터 자체를 넘겨서 id 검색과 prefab_name 검색 경로를 한 번에 검증한다.
        MonsterData selectedMonster = monsters[selectedMonsterIndex];
        if (Spawner == null)
        {
            Debug.LogWarning("MonsterSpawnTester: MonsterSpawner 참조가 없습니다.");
            return false;
        }

        return Spawner.SpawnMonster(selectedMonster);
    }

    public bool RemoveSpawnedMonster()
    {
        if (Spawner == null)
            return false;

        return Spawner.RemoveCurrentMonster();
    }

    private void CacheSceneReferences()
    {
        if (spawner == null)
            spawner = GetComponent<MonsterSpawner>();

        if (spawner == null)
            spawner = FindAnyObjectByType<MonsterSpawner>();

        if (monsterTable == null && spawner != null)
            monsterTable = spawner.MonsterTable;

        if (monsterTable == null)
            monsterTable = FindAnyObjectByType<MonsterTable>();
    }

    private void ClampSelectedMonsterIndex()
    {
        IReadOnlyList<MonsterData> monsters = monsterTable != null && monsterTable.Monsters != null
            ? monsterTable.Monsters
            : EmptyMonsters;

        if (monsters.Count == 0)
        {
            selectedMonsterIndex = 0;
            return;
        }

        selectedMonsterIndex = Mathf.Clamp(selectedMonsterIndex, 0, monsters.Count - 1);
    }
}
