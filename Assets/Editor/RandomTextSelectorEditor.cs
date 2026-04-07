using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RandomTextSelector))]
public class RandomTextSelectorEditor : Editor
{
    private SerializedProperty targetTextProp;
    private SerializedProperty entriesProp;
    private SerializedProperty printDebugLogProp;

    private struct EntryPreview
    {
        public string text;
        public bool useCustom;
        public float inputProbability;
        public float assignedWeight;
        public float actualChance;
    }

    private struct AnalysisResult
    {
        public float specifiedSum;
        public int autoCount;
        public float remaining;
        public float autoProbability;
        public bool exceeds100;
        public bool noEntries;
        public bool allZero;
        public bool normalizedMode;
        public float totalWeight;
        public List<EntryPreview> previews;
    }

    private enum PendingActionType
    {
        None,
        MoveUp,
        MoveDown,
        Duplicate,
        Delete
    }

    private struct PendingAction
    {
        public PendingActionType type;
        public int index;
    }

    private void OnEnable()
    {
        targetTextProp = serializedObject.FindProperty("targetText");
        entriesProp = serializedObject.FindProperty("entries");
        printDebugLogProp = serializedObject.FindProperty("printDebugLog");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawHeader();
        EditorGUILayout.Space(4f);

        DrawMainFields();
        EditorGUILayout.Space(8f);

        AnalysisResult analysis = AnalyzeEntries();

        DrawSummary(analysis);
        EditorGUILayout.Space(8f);

        DrawPreviewTable(analysis);
        EditorGUILayout.Space(8f);

        DrawEntriesList(analysis);
        EditorGUILayout.Space(8f);

        DrawBottomButtons(analysis);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Random Text Selector", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "씬 시작 시 1회 문자열을 랜덤 선택하여 TMP에 출력합니다.\n" +
            "직접 확률을 지정하지 않은 항목은 남은 확률을 균등 분배받습니다.",
            MessageType.Info);
    }

    private void DrawMainFields()
    {
        EditorGUILayout.PropertyField(targetTextProp);
        EditorGUILayout.PropertyField(printDebugLogProp);
    }

    private void DrawSummary(AnalysisResult analysis)
    {
        EditorGUILayout.LabelField("확률 요약", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("직접 지정 확률 합", $"{analysis.specifiedSum:0.###}%");
            EditorGUILayout.LabelField("자동 분배 대상 수", analysis.autoCount.ToString());
            EditorGUILayout.LabelField("남은 확률", $"{analysis.remaining:0.###}%");

            if (analysis.autoCount > 0 && !analysis.exceeds100)
                EditorGUILayout.LabelField("자동 분배 1개당", $"{analysis.autoProbability:0.###}%");

            if (analysis.noEntries)
            {
                EditorGUILayout.HelpBox("문자열 항목이 없습니다.", MessageType.Warning);
            }
            else if (analysis.exceeds100)
            {
                EditorGUILayout.HelpBox(
                    $"직접 지정한 확률 합이 100%를 초과했습니다. 현재 합: {analysis.specifiedSum:0.###}%",
                    MessageType.Error);
            }
            else if (analysis.allZero)
            {
                EditorGUILayout.HelpBox(
                    "모든 확률값이 0입니다. 현재 상태로는 유효한 선택이 불가능합니다.",
                    MessageType.Warning);
            }
            else if (analysis.normalizedMode)
            {
                EditorGUILayout.HelpBox(
                    "자동 분배 대상이 없고 총합이 100 미만입니다.\n" +
                    "런타임에서는 현재 값들을 가중치로 사용하여 정규화된 비율로 선택합니다.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox("현재 설정이 정상입니다.", MessageType.None);
            }
        }
    }

    private void DrawPreviewTable(AnalysisResult analysis)
    {
        EditorGUILayout.LabelField("최종 확률 미리보기", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            if (analysis.noEntries)
            {
                EditorGUILayout.LabelField("표시할 항목이 없습니다.");
                return;
            }

            if (analysis.exceeds100)
            {
                EditorGUILayout.LabelField("확률 합이 100을 초과하여 미리보기를 표시할 수 없습니다.");
                return;
            }

            if (analysis.allZero)
            {
                EditorGUILayout.LabelField("모든 확률이 0이어서 미리보기를 표시할 수 없습니다.");
                return;
            }

            for (int i = 0; i < analysis.previews.Count; i++)
            {
                EntryPreview p = analysis.previews[i];
                string name = string.IsNullOrWhiteSpace(p.text) ? $"<Empty #{i}>" : Shorten(p.text, 28);
                string source = p.useCustom ? "직접 입력" : "자동 분배";

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{i}. {name}", GUILayout.MinWidth(120));
                EditorGUILayout.LabelField(source, GUILayout.Width(70));
                EditorGUILayout.LabelField($"가중치 {p.assignedWeight:0.###}", GUILayout.Width(90));
                EditorGUILayout.LabelField($"실제 확률 {p.actualChance:0.###}%", GUILayout.Width(110));
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    private void DrawEntriesList(AnalysisResult analysis)
    {
        EditorGUILayout.LabelField("문자열 목록", EditorStyles.boldLabel);

        PendingAction pending = new PendingAction
        {
            type = PendingActionType.None,
            index = -1
        };

        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            SerializedProperty element = entriesProp.GetArrayElementAtIndex(i);
            SerializedProperty textProp = element.FindPropertyRelative("text");
            SerializedProperty useCustomProp = element.FindPropertyRelative("useCustomProbability");
            SerializedProperty probabilityProp = element.FindPropertyRelative("probability");

            EntryPreview preview = default;
            if (analysis.previews != null && i < analysis.previews.Count)
                preview = analysis.previews[i];

            string headerText = BuildHeaderText(i, textProp.stringValue, preview);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, headerText, true);

                if (element.isExpanded)
                {
                    EditorGUI.indentLevel++;

                    EditorGUILayout.PropertyField(textProp, new GUIContent("문자열"), true);
                    EditorGUILayout.PropertyField(useCustomProp, new GUIContent("직접 확률 사용"));

                    if (useCustomProp.boolValue)
                    {
                        float clamped = Mathf.Max(0f, probabilityProp.floatValue);
                        if (!Mathf.Approximately(clamped, probabilityProp.floatValue))
                            probabilityProp.floatValue = clamped;

                        EditorGUILayout.PropertyField(probabilityProp, new GUIContent("출현 확률"));
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(
                            "이 항목은 남은 확률을 자동 분배받습니다.",
                            MessageType.None);
                    }

                    DrawEntryPreviewBox(preview);

                    EditorGUILayout.Space(4f);
                    EditorGUILayout.BeginHorizontal();

                    GUI.enabled = i > 0;
                    if (GUILayout.Button("▲ 위로")) pending = new PendingAction { type = PendingActionType.MoveUp, index = i };

                    GUI.enabled = i < entriesProp.arraySize - 1;
                    if (GUILayout.Button("▼ 아래로")) pending = new PendingAction { type = PendingActionType.MoveDown, index = i };

                    GUI.enabled = true;
                    if (GUILayout.Button("복제")) pending = new PendingAction { type = PendingActionType.Duplicate, index = i };
                    if (GUILayout.Button("삭제")) pending = new PendingAction { type = PendingActionType.Delete, index = i };

                    EditorGUILayout.EndHorizontal();

                    EditorGUI.indentLevel--;
                }
            }

            if (pending.type != PendingActionType.None)
                break;
        }

        ApplyPendingAction(pending);

        EditorGUILayout.Space(4f);

        if (GUILayout.Button("항목 추가"))
        {
            int newIndex = entriesProp.arraySize;
            entriesProp.InsertArrayElementAtIndex(newIndex);

            SerializedProperty newElement = entriesProp.GetArrayElementAtIndex(newIndex);
            SerializedProperty textProp = newElement.FindPropertyRelative("text");
            SerializedProperty useCustomProp = newElement.FindPropertyRelative("useCustomProbability");
            SerializedProperty probabilityProp = newElement.FindPropertyRelative("probability");

            textProp.stringValue = string.Empty;
            useCustomProp.boolValue = false;
            probabilityProp.floatValue = 0f;
            newElement.isExpanded = true;
        }
    }

    private void DrawEntryPreviewBox(EntryPreview preview)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.textArea))
        {
            EditorGUILayout.LabelField("미리보기", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("입력값", $"{preview.inputProbability:0.###}");
            EditorGUILayout.LabelField("최종 가중치", $"{preview.assignedWeight:0.###}");
            EditorGUILayout.LabelField("실제 선택 확률", $"{preview.actualChance:0.###}%");
        }
    }

    private void DrawBottomButtons(AnalysisResult analysis)
    {
        EditorGUILayout.LabelField("도구", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("확률표 콘솔 출력"))
        {
            Debug.Log(BuildPreviewLog(analysis), target);
        }

        GUI.enabled = Application.isPlaying;
        if (GUILayout.Button("지금 랜덤 선택 실행"))
        {
            RandomTextSelector selector = (RandomTextSelector)target;
            selector.ReselectNow();
        }
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "랜덤 실행 버튼은 플레이 모드에서만 동작합니다.",
                MessageType.None);
        }
    }

    private void ApplyPendingAction(PendingAction pending)
    {
        if (pending.type == PendingActionType.None || pending.index < 0 || pending.index >= entriesProp.arraySize)
            return;

        switch (pending.type)
        {
            case PendingActionType.MoveUp:
                entriesProp.MoveArrayElement(pending.index, pending.index - 1);
                break;

            case PendingActionType.MoveDown:
                entriesProp.MoveArrayElement(pending.index, pending.index + 1);
                break;

            case PendingActionType.Duplicate:
                {
                    entriesProp.InsertArrayElementAtIndex(pending.index);
                    SerializedProperty src = entriesProp.GetArrayElementAtIndex(pending.index + 1);
                    SerializedProperty dst = entriesProp.GetArrayElementAtIndex(pending.index);

                    dst.FindPropertyRelative("text").stringValue =
                        src.FindPropertyRelative("text").stringValue;
                    dst.FindPropertyRelative("useCustomProbability").boolValue =
                        src.FindPropertyRelative("useCustomProbability").boolValue;
                    dst.FindPropertyRelative("probability").floatValue =
                        src.FindPropertyRelative("probability").floatValue;
                    dst.isExpanded = true;
                    break;
                }

            case PendingActionType.Delete:
                entriesProp.DeleteArrayElementAtIndex(pending.index);
                break;
        }
    }

    private AnalysisResult AnalyzeEntries()
    {
        AnalysisResult result = new AnalysisResult
        {
            previews = new List<EntryPreview>()
        };

        result.noEntries = entriesProp == null || entriesProp.arraySize == 0;
        if (result.noEntries)
            return result;

        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            SerializedProperty element = entriesProp.GetArrayElementAtIndex(i);
            SerializedProperty textProp = element.FindPropertyRelative("text");
            SerializedProperty useCustomProp = element.FindPropertyRelative("useCustomProbability");
            SerializedProperty probabilityProp = element.FindPropertyRelative("probability");

            bool useCustom = useCustomProp.boolValue;
            float inputProbability = Mathf.Max(0f, probabilityProp.floatValue);

            if (useCustom)
                result.specifiedSum += inputProbability;
            else
                result.autoCount++;

            result.previews.Add(new EntryPreview
            {
                text = textProp.stringValue,
                useCustom = useCustom,
                inputProbability = inputProbability,
                assignedWeight = 0f,
                actualChance = 0f
            });
        }

        result.remaining = Mathf.Max(0f, 100f - result.specifiedSum);
        result.exceeds100 = result.specifiedSum > 100f;

        if (result.exceeds100)
            return result;

        if (result.autoCount > 0)
        {
            result.autoProbability = result.remaining / result.autoCount;
            result.totalWeight = 100f;

            for (int i = 0; i < result.previews.Count; i++)
            {
                EntryPreview p = result.previews[i];
                p.assignedWeight = p.useCustom ? p.inputProbability : result.autoProbability;
                p.actualChance = p.assignedWeight;
                result.previews[i] = p;
            }

            result.allZero = Mathf.Approximately(result.totalWeight, 0f);
            return result;
        }

        result.totalWeight = result.specifiedSum;
        result.allZero = result.totalWeight <= 0f;
        result.normalizedMode = !result.allZero && !Mathf.Approximately(result.specifiedSum, 100f);

        if (result.allZero)
            return result;

        for (int i = 0; i < result.previews.Count; i++)
        {
            EntryPreview p = result.previews[i];
            p.assignedWeight = p.inputProbability;
            p.actualChance = (p.assignedWeight / result.totalWeight) * 100f;
            result.previews[i] = p;
        }

        return result;
    }

    private string BuildHeaderText(int index, string rawText, EntryPreview preview)
    {
        string title = string.IsNullOrWhiteSpace(rawText) ? "<Empty>" : Shorten(rawText, 24);
        string mode = preview.useCustom ? "직접" : "자동";
        return $"{index}. {title}  [{mode}]  {preview.actualChance:0.###}%";
    }

    private string BuildPreviewLog(AnalysisResult analysis)
    {
        if (analysis.noEntries)
            return "[RandomTextSelectorEditor] 항목이 없습니다.";

        if (analysis.exceeds100)
            return $"[RandomTextSelectorEditor] 확률 합이 100%를 초과했습니다. 현재 합: {analysis.specifiedSum:0.###}%";

        if (analysis.allZero)
            return "[RandomTextSelectorEditor] 모든 확률이 0입니다.";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("[RandomTextSelectorEditor] 최종 확률 미리보기");

        for (int i = 0; i < analysis.previews.Count; i++)
        {
            EntryPreview p = analysis.previews[i];
            string text = string.IsNullOrWhiteSpace(p.text) ? $"<Empty #{i}>" : p.text.Replace("\n", " ");
            sb.AppendLine(
                $"- {text} | source={(p.useCustom ? "custom" : "auto")} | " +
                $"weight={p.assignedWeight:0.###} | chance={p.actualChance:0.###}%");
        }

        return sb.ToString();
    }

    private string Shorten(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        text = text.Replace("\n", " ");
        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength) + "...";
    }
}