using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BattleGimmickDebugSettings))]
public class BattleGimmickDebugSettingsEditor : Editor
{
    private SerializedProperty enableKnightGimmicks;
    private SerializedProperty enableKnightCalculatedVictory;
    private SerializedProperty enableKnightFailedPlan;
    private SerializedProperty enableKnightShiftBlame;
    private SerializedProperty enableSaintGimmicks;
    private SerializedProperty enableSaintRevelation;
    private SerializedProperty enableSaintAmen;
    private SerializedProperty enableSaintDamnGod;
    private SerializedProperty enableMageGimmicks;
    private SerializedProperty enableMageMediocreSpell;
    private SerializedProperty enableMageDestructionSpell;
    private SerializedProperty enableMageWitchHunt;
    private SerializedProperty enableArcherGimmicks;
    private SerializedProperty enableArcherSupportAttack;
    private SerializedProperty enableArcherScream;
    private SerializedProperty enableArcherContributionReward;
    private SerializedProperty enableFighterGimmicks;
    private SerializedProperty enableFighterTopCommand;
    private SerializedProperty enableFighterHarmony;
    private SerializedProperty enableFighterConflict;
    private SerializedProperty enableFighterLuckyReward;
    private SerializedProperty enableBerserkerGimmicks;
    private SerializedProperty enableBerserkerOneMore;
    private SerializedProperty enableBerserkerOverexcited;

    private void OnEnable()
    {
        enableKnightGimmicks = serializedObject.FindProperty("enableKnightGimmicks");
        enableKnightCalculatedVictory = serializedObject.FindProperty("enableKnightCalculatedVictory");
        enableKnightFailedPlan = serializedObject.FindProperty("enableKnightFailedPlan");
        enableKnightShiftBlame = serializedObject.FindProperty("enableKnightShiftBlame");
        enableSaintGimmicks = serializedObject.FindProperty("enableSaintGimmicks");
        enableSaintRevelation = serializedObject.FindProperty("enableSaintRevelation");
        enableSaintAmen = serializedObject.FindProperty("enableSaintAmen");
        enableSaintDamnGod = serializedObject.FindProperty("enableSaintDamnGod");
        enableMageGimmicks = serializedObject.FindProperty("enableMageGimmicks");
        enableMageMediocreSpell = serializedObject.FindProperty("enableMageMediocreSpell");
        enableMageDestructionSpell = serializedObject.FindProperty("enableMageDestructionSpell");
        enableMageWitchHunt = serializedObject.FindProperty("enableMageWitchHunt");
        enableArcherGimmicks = serializedObject.FindProperty("enableArcherGimmicks");
        enableArcherSupportAttack = serializedObject.FindProperty("enableArcherSupportAttack");
        enableArcherScream = serializedObject.FindProperty("enableArcherScream");
        enableArcherContributionReward = serializedObject.FindProperty("enableArcherContributionReward");
        enableFighterGimmicks = serializedObject.FindProperty("enableFighterGimmicks");
        enableFighterTopCommand = serializedObject.FindProperty("enableFighterTopCommand");
        enableFighterHarmony = serializedObject.FindProperty("enableFighterHarmony");
        enableFighterConflict = serializedObject.FindProperty("enableFighterConflict");
        enableFighterLuckyReward = serializedObject.FindProperty("enableFighterLuckyReward");
        enableBerserkerGimmicks = serializedObject.FindProperty("enableBerserkerGimmicks");
        enableBerserkerOneMore = serializedObject.FindProperty("enableBerserkerOneMore");
        enableBerserkerOverexcited = serializedObject.FindProperty("enableBerserkerOverexcited");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("전투 기믹 디버그 설정", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("플레이 중 값을 바꾸면 이후 전투 판정부터 바로 반영됩니다.", MessageType.Info);

        DrawJobGroup(
            "기사",
            enableKnightGimmicks,
            ("계산된 승리", enableKnightCalculatedVictory),
            ("실패한 계획", enableKnightFailedPlan),
            ("책임 전가", enableKnightShiftBlame));

        DrawJobGroup(
            "성녀",
            enableSaintGimmicks,
            ("계시", enableSaintRevelation),
            ("아멘", enableSaintAmen),
            ("신을 원망하라", enableSaintDamnGod));

        DrawJobGroup(
            "마법사",
            enableMageGimmicks,
            ("시시한 주문", enableMageMediocreSpell),
            ("파괴 주문", enableMageDestructionSpell),
            ("마녀 사냥", enableMageWitchHunt));

        DrawJobGroup(
            "궁수",
            enableArcherGimmicks,
            ("지원 공격", enableArcherSupportAttack),
            ("비명", enableArcherScream),
            ("기여 보상", enableArcherContributionReward));

        DrawJobGroup(
            "격투가",
            enableFighterGimmicks,
            ("지휘권", enableFighterTopCommand),
            ("조화", enableFighterHarmony),
            ("갈등", enableFighterConflict),
            ("행운 보상", enableFighterLuckyReward));

        DrawJobGroup(
            "광전사",
            enableBerserkerGimmicks,
            ("한 번 더", enableBerserkerOneMore),
            ("과열", enableBerserkerOverexcited));

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawJobGroup(
        string title,
        SerializedProperty masterToggle,
        params (string Label, SerializedProperty Property)[] toggles)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(masterToggle, new GUIContent($"{title} 기믹 전체"));

        using (new EditorGUI.IndentLevelScope())
        using (new EditorGUI.DisabledScope(!masterToggle.boolValue))
        {
            for (int i = 0; i < toggles.Length; i++)
                EditorGUILayout.PropertyField(toggles[i].Property, new GUIContent(toggles[i].Label));
        }
    }
}
