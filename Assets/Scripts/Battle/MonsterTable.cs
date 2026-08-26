using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MonsterData
{
    private const string MissingCharacterLine = "... ";

    public string id;
    public string monster_name;
    public int danger;
    public string spawn_stage;
    public int HP;
    public string gimmick;
    public string reward_1st;
    public string reward_2nd;
    public string reward_3rd;
    public string prefab_name;
    public string preview_name;
    public string preview_text;
    public string stage_description;
    public MonsterCharacterLine[] character_lines;
    public string dec;

    public string GetCharacterLine(JobType job)
    {
        return GetCharacterLine(job.ToString());
    }

    public string GetCharacterLine(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId) || character_lines == null)
            return MissingCharacterLine;

        for (int i = 0; i < character_lines.Length; i++)
        {
            MonsterCharacterLine characterLine = character_lines[i];
            if (characterLine == null)
                continue;

            if (!string.Equals(characterLine.character_id, characterId, StringComparison.OrdinalIgnoreCase))
                continue;

            return string.IsNullOrWhiteSpace(characterLine.line)
                ? MissingCharacterLine
                : characterLine.line;
        }

        return MissingCharacterLine;
    }
}

[Serializable]
public class MonsterCharacterLine
{
    public string character_id;
    public string line;
}

[Serializable]
public class JsonArrayWrapper<T>
{
    public T[] items;
}

public class MonsterTable : MonoBehaviour
{
    [Header("JSON 파일")]
    [SerializeField] private TextAsset monsterJsonFile;

    [Header("로드 결과 (읽기 전용)")]
    [SerializeField] private List<MonsterData> monsters = new List<MonsterData>();

    private Dictionary<string, MonsterData> monsterById = new Dictionary<string, MonsterData>();

    public IReadOnlyList<MonsterData> Monsters => monsters;

    private void Awake()
    {
        LoadMonsterTable();
    }

    [ContextMenu("Load Monster Table")]
    public void LoadMonsterTable()
    {
        monsters.Clear();
        monsterById.Clear();

        if (monsterJsonFile == null)
        {
            Debug.LogWarning("[MonsterTable] monsterJsonFile 이 비어 있습니다.");
            return;
        }

        MonsterData[] loadedArray = JsonArrayHelper.FromJson<MonsterData>(monsterJsonFile.text);

        if (loadedArray == null || loadedArray.Length == 0)
        {
            Debug.LogWarning("[MonsterTable] 몬스터 데이터가 비어 있습니다.");
            return;
        }

        for (int i = 0; i < loadedArray.Length; i++)
        {
            MonsterData data = loadedArray[i];

            if (data == null)
                continue;

            monsters.Add(data);

            if (string.IsNullOrWhiteSpace(data.id))
            {
                Debug.LogWarning($"[MonsterTable] index={i} 데이터의 id 가 비어 있습니다.");
                continue;
            }

            if (monsterById.ContainsKey(data.id))
            {
                Debug.LogWarning($"[MonsterTable] 중복 id 발견: {data.id}");
                continue;
            }

            monsterById.Add(data.id, data);
        }

        Debug.Log($"[MonsterTable] 몬스터 {monsters.Count}개 로드 완료");
    }

    public bool TryGetMonster(string monsterId, out MonsterData data)
    {
        return monsterById.TryGetValue(monsterId, out data);
    }

    public MonsterData GetMonster(string monsterId)
    {
        if (monsterById.TryGetValue(monsterId, out MonsterData data))
            return data;

        Debug.LogWarning($"[MonsterTable] 존재하지 않는 몬스터 ID: {monsterId}");
        return null;
    }

    public List<MonsterData> GetMonstersByStage(string stageName)
    {
        List<MonsterData> result = new List<MonsterData>();

        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i].spawn_stage == stageName)
                result.Add(monsters[i]);
        }

        return result;
    }
}

public static class JsonArrayHelper
{
    public static T[] FromJson<T>(string json)
    {
        string wrappedJson = "{ \"items\": " + json + "}";
        JsonArrayWrapper<T> wrapper = JsonUtility.FromJson<JsonArrayWrapper<T>>(wrappedJson);

        if (wrapper == null || wrapper.items == null)
            return Array.Empty<T>();

        return wrapper.items;
    }
}