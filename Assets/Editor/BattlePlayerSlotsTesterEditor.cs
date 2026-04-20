using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BattlePlayerSlotsTester))]
public class BattlePlayerSlotsTesterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        BattlePlayerSlotsTester tester = (BattlePlayerSlotsTester)target;

        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Battle Player Test Controller", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 모드에서 현재 생성된 Battle Player Standing 들을 원격 조작할 수 있습니다.", MessageType.Info);
            return;
        }

        if (tester.Slots == null)
        {
            EditorGUILayout.HelpBox("BattlePlayerSlots 컴포넌트를 찾지 못했습니다.", MessageType.Warning);
            return;
        }

        List<int> playerIndices = tester.GetCurrentPlayerIndices();

        if (playerIndices.Count == 0)
        {
            EditorGUILayout.HelpBox("현재 생성된 플레이어 스탠딩이 없습니다.", MessageType.Info);

            if (GUILayout.Button("다시 탐색"))
                Repaint();

            return;
        }

        for (int i = 0; i < playerIndices.Count; i++)
        {
            int playerIndex = playerIndices[i];
            string[] options = tester.GetAnimationOptions(playerIndex);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"{playerIndex + 1}p", EditorStyles.boldLabel);

            if (options == null || options.Length == 0)
            {
                EditorGUILayout.HelpBox("이 플레이어 프리펩에 등록된 테스트용 애니메이션이 없습니다.", MessageType.Warning);
            }
            else
            {
                string current = tester.GetSelectedAnimation(playerIndex);
                int currentIndex = 0;

                for (int j = 0; j < options.Length; j++)
                {
                    if (options[j] == current)
                    {
                        currentIndex = j;
                        break;
                    }
                }

                EditorGUILayout.BeginHorizontal();

                int newIndex = EditorGUILayout.Popup(currentIndex, options);
                if (newIndex >= 0 && newIndex < options.Length)
                {
                    if (options[newIndex] != current)
                    {
                        Undo.RecordObject(tester, "Change Test Animation");
                        tester.SetSelectedAnimation(playerIndex, options[newIndex]);
                        EditorUtility.SetDirty(tester);
                    }
                }

                if (GUILayout.Button("실행", GUILayout.Width(70)))
                {
                    tester.PlaySelected(playerIndex);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("인스펙터 새로고침"))
        {
            Repaint();
        }
    }
}