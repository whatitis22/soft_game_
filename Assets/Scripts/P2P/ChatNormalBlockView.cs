using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatNormalBlockView : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;

    public void Bind(string nickname, string message)
    {
        if (messageText != null)
            messageText.text = $"{nickname}: {message}";

        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
    }
}