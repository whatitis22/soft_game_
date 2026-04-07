using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class RandomTextSelector : MonoBehaviour
{
    [System.Serializable]
    public class TextEntry
    {
        [TextArea(2, 5)]
        public string text;

        [Header("체크하면 아래 확률 사용, 끄면 자동 분배")]
        public bool useCustomProbability = false;

        [Min(0f)]
        public float probability = 0f;
    }

    [Header("출력 대상 TMP")]
    [SerializeField] private TMP_Text targetText;

    [Header("문자열 목록")]
    [SerializeField] private List<TextEntry> entries = new List<TextEntry>();

    [Header("디버그 로그 출력")]
    [SerializeField] private bool printDebugLog = true;

    private bool hasSelected = false;

    private void Start()
    {
        SelectOnce();
    }

    [ContextMenu("지금 랜덤 선택 실행")]
    public void SelectOnce()
    {
        SelectInternal(forceReselect: false);
    }

    public void ReselectNow()
    {
        SelectInternal(forceReselect: true);
    }

    private void SelectInternal(bool forceReselect)
    {
        if (hasSelected && !forceReselect)
            return;

        if (targetText == null)
        {
            Debug.LogWarning("[RandomTextSelector] targetText가 비어 있습니다.");
            return;
        }

        if (entries == null || entries.Count == 0)
        {
            Debug.LogWarning("[RandomTextSelector] entries가 비어 있습니다.");
            return;
        }

        float specifiedSum = 0f;
        int autoCount = 0;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] == null) continue;

            if (entries[i].useCustomProbability)
                specifiedSum += Mathf.Max(0f, entries[i].probability);
            else
                autoCount++;
        }

        if (specifiedSum > 100f)
        {
            Debug.LogWarning(
                $"[RandomTextSelector] 입력된 확률 합이 100을 넘었습니다. 현재 합: {specifiedSum}%");
            return;
        }

        float[] finalProbabilities = new float[entries.Count];

        if (autoCount > 0)
        {
            float remain = 100f - specifiedSum;
            float autoProbability = remain / autoCount;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].useCustomProbability)
                    finalProbabilities[i] = Mathf.Max(0f, entries[i].probability);
                else
                    finalProbabilities[i] = autoProbability;
            }
        }
        else
        {
            if (specifiedSum <= 0f)
            {
                Debug.LogWarning("[RandomTextSelector] 모든 확률이 0이라 선택할 수 없습니다.");
                return;
            }

            for (int i = 0; i < entries.Count; i++)
                finalProbabilities[i] = Mathf.Max(0f, entries[i].probability);

            if (specifiedSum < 100f)
            {
                Debug.LogWarning(
                    $"[RandomTextSelector] 확률 합이 100 미만입니다. (현재 {specifiedSum}%) " +
                    $"미지정 항목이 없으므로 현재 값들의 비율대로 선택합니다.");
            }
        }

        float total = 0f;
        for (int i = 0; i < finalProbabilities.Length; i++)
            total += finalProbabilities[i];

        if (total <= 0f)
        {
            Debug.LogWarning("[RandomTextSelector] 최종 확률 총합이 0이라 선택할 수 없습니다.");
            return;
        }

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        int selectedIndex = 0;

        for (int i = 0; i < finalProbabilities.Length; i++)
        {
            cumulative += finalProbabilities[i];
            if (roll <= cumulative)
            {
                selectedIndex = i;
                break;
            }
        }

        hasSelected = true;
        targetText.text = entries[selectedIndex].text;

        if (printDebugLog)
        {
            string log = forceReselect
                ? "[RandomTextSelector] 강제 재선택 결과\n"
                : "[RandomTextSelector] 최종 확률표\n";

            for (int i = 0; i < entries.Count; i++)
            {
                log += $"- {entries[i].text} : {finalProbabilities[i]}%\n";
            }

            log += $"=> 선택 결과: {entries[selectedIndex].text}";
            Debug.Log(log);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (entries == null) return;

        float specifiedSum = 0f;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] == null) continue;

            if (entries[i].useCustomProbability)
                specifiedSum += Mathf.Max(0f, entries[i].probability);
        }

        if (specifiedSum > 100f)
        {
            Debug.LogWarning(
                $"[RandomTextSelector] 입력된 확률 합이 100을 넘었습니다. 현재 합: {specifiedSum}%");
        }
    }
#endif
}