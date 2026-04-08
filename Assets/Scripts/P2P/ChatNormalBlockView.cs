using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatNormalBlockView : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private float extraVerticalPadding = 8f;

    private LayoutElement layoutElement;
    private RectTransform rectTransform;
    private bool initialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        rectTransform = (RectTransform)transform;

        if (layoutElement == null)
            layoutElement = GetComponent<LayoutElement>();

        if (layoutElement == null)
            layoutElement = gameObject.AddComponent<LayoutElement>();

        if (messageText == null)
            messageText = GetComponentInChildren<TMP_Text>(true);

        initialized = true;
    }

    public void Bind(string nickname, string message)
    {
        EnsureInitialized();

        if (messageText == null)
        {
            Debug.LogError($"[{nameof(ChatNormalBlockView)}] messageText 가 없습니다.", this);
            return;
        }

        string fullText = $"{nickname}: {message}";
        messageText.text = fullText;

        Canvas.ForceUpdateCanvases();
        messageText.ForceMeshUpdate();

        float width = messageText.rectTransform.rect.width;
        if (width <= 0f)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            Canvas.ForceUpdateCanvases();
            width = messageText.rectTransform.rect.width;
        }

        if (width <= 0f)
        {
            // 폭이 아직 0이면 일단 최소 높이라도 보장
            layoutElement.preferredHeight = messageText.fontSize + extraVerticalPadding;
            return;
        }

        float preferredHeight = messageText.GetPreferredValues(fullText, width, 0f).y;
        layoutElement.preferredHeight = preferredHeight + extraVerticalPadding;

        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }
}