using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbySpeechBubbleView : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;

    [Header("크기 설정")]
    [SerializeField] private float maxTextWidth = 260f;
    [SerializeField] private float minBubbleHeight = 64f;
    [SerializeField] private Vector2 padding = new Vector2(20f, 16f);

    private RectTransform rootRect;
    private RectTransform textRect;
    private bool initialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (initialized)
            return;

        rootRect = (RectTransform)transform;

        if (messageText == null)
            messageText = GetComponentInChildren<TMP_Text>(true);

        if (messageText != null)
            textRect = messageText.rectTransform;

        initialized = true;
    }

    public void Bind(string message)
    {
        EnsureInitialized();

        if (messageText == null || textRect == null)
        {
            Debug.LogError("[LobbySpeechBubbleView] messageText 연결이 없습니다.", this);
            return;
        }

        rootRect.anchorMin = new Vector2(0.5f, 0f);
        rootRect.anchorMax = new Vector2(0.5f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);

        messageText.enableWordWrapping = true;
        messageText.text = message ?? string.Empty;

        float bubbleWidth = maxTextWidth + padding.x * 2f;

        rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, bubbleWidth);

        textRect.offsetMin = new Vector2(padding.x, padding.y);
        textRect.offsetMax = new Vector2(-padding.x, -padding.y);

        Canvas.ForceUpdateCanvases();
        messageText.ForceMeshUpdate();

        float preferredTextHeight = messageText.GetPreferredValues(messageText.text, maxTextWidth, 0f).y;
        float bubbleHeight = Mathf.Max(minBubbleHeight, preferredTextHeight + padding.y * 2f);

        rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, bubbleHeight);
    }
}