using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(MonsterSpawnTester))]
public class MonsterSpawnTesterEditor : Editor
{
    private SerializedProperty spawnerProperty;
    private SerializedProperty monsterTableProperty;
    private SerializedProperty selectedMonsterIndexProperty;
    private SerializedProperty testDamageAmountProperty;
    private SerializedProperty testHealAmountProperty;

    private void OnEnable()
    {
        spawnerProperty = serializedObject.FindProperty("spawner");
        monsterTableProperty = serializedObject.FindProperty("monsterTable");
        selectedMonsterIndexProperty = serializedObject.FindProperty("selectedMonsterIndex");
        testDamageAmountProperty = serializedObject.FindProperty("testDamageAmount");
        testHealAmountProperty = serializedObject.FindProperty("testHealAmount");
    }

    public override void OnInspectorGUI()
    {
        MonsterSpawnTester tester = (MonsterSpawnTester)target;

        serializedObject.Update();

        EditorGUILayout.PropertyField(spawnerProperty);
        EditorGUILayout.PropertyField(monsterTableProperty);

        DrawMonsterSelector(tester);

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        DrawControlButtons(tester);
        DrawDamageButtons(tester);
        DrawCurrentState(tester);
    }

    private void DrawMonsterSelector(MonsterSpawnTester tester)
    {
        string[] options = tester.GetMonsterOptions();

        if (options.Length == 0)
        {
            EditorGUILayout.HelpBox("로드된 몬스터가 없습니다. MonsterTable 참조와 JSON 파일을 확인하세요.", MessageType.Warning);
            return;
        }

        int currentIndex = Mathf.Clamp(selectedMonsterIndexProperty.intValue, 0, options.Length - 1);

        EditorGUI.BeginChangeCheck();
        int selectedIndex = EditorGUILayout.Popup("소환할 몬스터", currentIndex, options);
        if (EditorGUI.EndChangeCheck())
            selectedMonsterIndexProperty.intValue = selectedIndex;
    }

    private void DrawControlButtons(MonsterSpawnTester tester)
    {
        if (GUILayout.Button("몬스터 테이블 다시 로드"))
        {
            Undo.RecordObject(tester, "Reload Monster Table");
            tester.RefreshMonsterTable();
            EditorUtility.SetDirty(tester);
            Repaint();
        }

        bool canSpawn = tester.Spawner != null && tester.GetLoadedMonsters().Count > 0;
        using (new EditorGUI.DisabledScope(!canSpawn))
        {
            if (GUILayout.Button("선택 몬스터 소환"))
            {
                if (tester.SpawnSelectedMonster())
                    MarkDirty(tester);

                Repaint();
            }
        }

        using (new EditorGUI.DisabledScope(!tester.CanRemove))
        {
            if (GUILayout.Button("소환된 몬스터 제거"))
            {
                if (tester.RemoveSpawnedMonster())
                    MarkDirty(tester);

                Repaint();
            }
        }
    }

    private void DrawDamageButtons(MonsterSpawnTester tester)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("몬스터 HP 테스트", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(testDamageAmountProperty, new GUIContent("데미지"));
        EditorGUILayout.PropertyField(testHealAmountProperty, new GUIContent("회복량"));
        serializedObject.ApplyModifiedProperties();

        bool hasMonster = tester.Spawner != null && tester.Spawner.HasSpawnedMonster;

        using (new EditorGUI.DisabledScope(!hasMonster))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("데미지 적용"))
            {
                tester.ApplyTestDamage();
                MarkDirty(tester);
                Repaint();
            }

            if (GUILayout.Button("회복"))
            {
                tester.HealTestMonster();
                MarkDirty(tester);
                Repaint();
            }

            if (GUILayout.Button("HP 리셋"))
            {
                tester.ResetTestMonsterHp();
                MarkDirty(tester);
                Repaint();
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawCurrentState(MonsterSpawnTester tester)
    {
        if (tester.Spawner == null)
            return;

        MonsterData currentMonster = tester.Spawner.CurrentMonsterData;
        if (tester.Spawner.HasSpawnedMonster && currentMonster != null)
        {
            string hpText = string.Empty;
            if (tester.Spawner.TryGetCurrentMonsterState(out MonsterBattleState state))
                hpText = $" / HP {state.CurrentHp} / {state.MaxHp}";

            EditorGUILayout.HelpBox($"현재 소환됨: {currentMonster.monster_name} ({currentMonster.id}){hpText}", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("현재 소환된 몬스터가 없습니다.", MessageType.None);
        }
    }

    private void MarkDirty(MonsterSpawnTester tester)
    {
        EditorUtility.SetDirty(tester);

        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(tester.gameObject.scene);
    }
}
