using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(MonsterSpawner))]
public class MonsterSpawnerEditor : Editor
{
    private void OnEnable()
    {
        MonsterSpawner spawner = (MonsterSpawner)target;
        if (spawner.AutoFillMonsterPrefabs(false))
            MarkDirty(spawner);
    }

    public override void OnInspectorGUI()
    {
        MonsterSpawner spawner = (MonsterSpawner)target;

        DrawDefaultInspector();

        EditorGUILayout.Space();

        if (GUILayout.Button("몬스터 프리펩 다시 채우기"))
        {
            Undo.RecordObject(spawner, "Refresh Monster Prefabs");

            if (spawner.AutoFillMonsterPrefabs(true))
                MarkDirty(spawner);

            Repaint();
        }
    }

    private void MarkDirty(MonsterSpawner spawner)
    {
        EditorUtility.SetDirty(spawner);

        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
    }
}
