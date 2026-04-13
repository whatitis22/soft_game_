using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatNarrationBlockView : MonoBehaviour
{
    [SerializeField] private TMP_Text narrationText;

    public void Bind(string message)
    {
        if (narrationText != null)
            narrationText.text = message;

        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
    }
}