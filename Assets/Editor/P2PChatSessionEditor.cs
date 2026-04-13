#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(P2PChatSession))]
public class P2PChatSessionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(8f);
        GUI.enabled = Application.isPlaying;

        P2PChatSession session = (P2PChatSession)target;

        if (GUILayout.Button("테스트 나레이션 추가"))
            session.Editor_AddTestNarration();

        if (GUILayout.Button("테스트 구분선 추가"))
            session.Editor_AddTestDivider();

        GUI.enabled = true;
    }
}
#endif