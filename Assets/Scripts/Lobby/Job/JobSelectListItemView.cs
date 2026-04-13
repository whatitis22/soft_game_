using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JobSelectListItemView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text stateText;

    public void Bind(string displayName, bool isCurrent, bool isTakenByOther, Action onClick)
    {
        labelText.text = displayName;

        if (isCurrent)
            stateText.text = "현재 선택";
        else if (isTakenByOther)
            stateText.text = "선택 불가";
        else
            stateText.text = "";

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick?.Invoke());
    }
}