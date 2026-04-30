using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BattleFlowTester))]
public class BattleFlowTesterEditor : Editor
{
    private SerializedProperty controllerProperty;
    private SerializedProperty stageNameProperty;

    private void OnEnable()
    {
        controllerProperty = serializedObject.FindProperty("controller");
        stageNameProperty = serializedObject.FindProperty("stageName");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(controllerProperty);
        EditorGUILayout.PropertyField(stageNameProperty);
        serializedObject.ApplyModifiedProperties();

        BattleFlowTester tester = (BattleFlowTester)target;
        BattleFlowController controller = tester.Controller;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Battle Flow Test", EditorStyles.boldLabel);

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("전투 흐름 테스트 버튼은 Play Mode에서 사용하세요.", MessageType.Info);

        using (new EditorGUI.DisabledScope(!Application.isPlaying || controller == null))
        {
            if (GUILayout.Button("스테이지 적용"))
                tester.ApplyStageName();

            if (GUILayout.Button("전투 시작"))
                tester.StartBattle();

            if (GUILayout.Button("현재 라운드 강제 판정"))
                tester.ResolveCurrentRoundNow();

            if (GUILayout.Button("몬스터 등장 기록 초기화"))
                tester.ResetAppearedMonsterHistory();
        }

        if (controller == null)
            EditorGUILayout.HelpBox("BattleFlowController를 찾지 못했습니다.", MessageType.Warning);
        else if (controller.IsResolvingRound)
            EditorGUILayout.HelpBox("현재 라운드 판정이 진행 중입니다.", MessageType.Info);
        else if (controller.CurrentMonsterData != null)
            EditorGUILayout.HelpBox($"현재 몬스터: {controller.CurrentMonsterData.monster_name} ({controller.CurrentMonsterData.id})", MessageType.Info);
    }
}
