using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class NumberCardDragUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Card")]
    [SerializeField, Range(1, 6)] private int cardValue = 1;
    [SerializeField] private float returnSpeed = 1800f;
    [SerializeField, Range(0f, 1f)] private float disabledAlpha = 0.35f;

    [Header("Artwork")]
    [SerializeField] private Image cardFaceImage;
    [SerializeField] private Image cardOutlineImage;
    [SerializeField] private TMP_Text cardValueLabel;
    [SerializeField] private Color cardValueLabelColor = new Color(0.08f, 0.075f, 0.06f, 1f);
    [SerializeField, Min(1f)] private float cardValueLabelFontSize = 82f;

    [Header("Hover")]
    [SerializeField, Min(0f)] private float hoverLift = 24f;
    [SerializeField] private Vector2 hoverSkillLabelSize = new Vector2(180f, 42f);
    [SerializeField, Min(0f)] private float hoverSkillLabelOffsetY = 10f;
    [SerializeField] private Color hoverSkillLabelBackgroundColor = new Color(0f, 0f, 0f, 0.74f);
    [SerializeField] private Color hoverSkillLabelTextColor = Color.white;
    [SerializeField, Min(1f)] private float hoverSkillLabelFontSize = 22f;

    [Tooltip("비워두면 부모 RectTransform 을 사용합니다.")]
    [SerializeField] private RectTransform dragParentRect;

    private readonly Vector3[] worldCorners = new Vector3[4];

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private RectTransform hoverSkillLabelRoot;
    private TMP_Text hoverSkillLabelText;
    private CardDrawPopupController owner;

    private Vector2 startAnchoredPosition;
    private Quaternion startLocalRotation;
    private float pointerOffsetY;
    private bool isInteractable = true;
    private bool isDragLocked;
    private bool isDragging;
    private bool isReturning;
    private bool isPointerHovering;
    private bool hasStoredSiblingIndex;
    private int storedSiblingIndex;

    public int CardValue => cardValue;

    private void OnValidate()
    {
        cardValue = Mathf.Clamp(cardValue, 1, 6);
    }

    private void Awake()
    {
        cardValue = Mathf.Clamp(cardValue, 1, 6);

        rectTransform = (RectTransform)transform;
        canvasGroup = GetComponent<CanvasGroup>();

        if (dragParentRect == null)
            dragParentRect = rectTransform.parent as RectTransform;

        startAnchoredPosition = rectTransform.anchoredPosition;
        startLocalRotation = rectTransform.localRotation;
        EnsureArtworkLayers();
        RefreshCardValueLabel();
        ApplyInteractableVisual();
        EnsureHoverSkillLabel();
        SetHoverSkillLabel(string.Empty);
    }

    private void Update()
    {
        if (!isReturning)
            return;

        Vector2 targetHomePosition = GetCurrentHomePosition();
        rectTransform.anchoredPosition = Vector2.MoveTowards(
            rectTransform.anchoredPosition,
            targetHomePosition,
            returnSpeed * Time.unscaledDeltaTime);

        if ((rectTransform.anchoredPosition - targetHomePosition).sqrMagnitude <= 0.0001f)
        {
            rectTransform.anchoredPosition = targetHomePosition;
            isReturning = false;
        }
    }

    public void Bind(CardDrawPopupController controller)
    {
        owner = controller;

        if (dragParentRect == null)
            dragParentRect = rectTransform.parent as RectTransform;
    }

    public void ApplyArtwork(Sprite faceSprite, Sprite outlineSprite)
    {
        EnsureArtworkLayers();

        if (cardFaceImage != null)
        {
            if (faceSprite != null)
                cardFaceImage.sprite = faceSprite;

            cardFaceImage.color = Color.white;
            cardFaceImage.type = Image.Type.Simple;
            cardFaceImage.preserveAspect = false;
        }

        if (cardOutlineImage != null)
        {
            cardOutlineImage.sprite = outlineSprite;
            cardOutlineImage.color = outlineSprite != null ? Color.white : Color.clear;
            cardOutlineImage.type = Image.Type.Simple;
            cardOutlineImage.preserveAspect = false;
        }

        RefreshCardValueLabel();
    }

    public void SetInteractable(bool canUse)
    {
        isInteractable = canUse;

        if (!isInteractable)
        {
            isDragging = false;
            isReturning = false;
            ClearHoverState();
            ResetToStartImmediate();
        }

        ApplyInteractableVisual();
    }

    public void SetVisible(bool visible)
    {
        if (!visible)
        {
            isDragging = false;
            isReturning = false;
            ClearHoverState();
        }

        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    public void SetDragLocked(bool locked)
    {
        isDragLocked = locked;

        if (isDragLocked)
        {
            // 제출된 카드가 있을 때는 카드 위치는 유지하되 새 드래그만 막는다.
            isDragging = false;
            isReturning = false;
        }

        ApplyInteractableVisual();
    }

    public void ResetToStartImmediate()
    {
        isDragging = false;
        isReturning = false;
        rectTransform.anchoredPosition = startAnchoredPosition;
        rectTransform.localRotation = startLocalRotation;
        ApplyInteractableVisual();
    }

    public void SetHomePose(Vector2 anchoredPosition, float zRotationDegrees)
    {
        if (rectTransform == null)
            rectTransform = (RectTransform)transform;

        startAnchoredPosition = anchoredPosition;
        startLocalRotation = Quaternion.Euler(0f, 0f, zRotationDegrees);

        if (!isDragging && !isReturning)
        {
            rectTransform.anchoredPosition = GetCurrentHomePosition();
            rectTransform.localRotation = startLocalRotation;
        }
    }

    public void AcceptCurrentSiblingIndex()
    {
        hasStoredSiblingIndex = false;
    }

    public void BeginReturnToStart()
    {
        isDragging = false;
        isReturning = true;
        if (!isPointerHovering)
            RestoreSiblingIndexIfNeeded();

        ApplyInteractableVisual();
    }

    public float GetBottomWorldY()
    {
        rectTransform.GetWorldCorners(worldCorners);

        float minY = worldCorners[0].y;
        for (int i = 1; i < worldCorners.Length; i++)
        {
            if (worldCorners[i].y < minY)
                minY = worldCorners[i].y;
        }

        return minY;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanApplyHover())
            return;

        isPointerHovering = true;
        StoreSiblingIndexIfNeeded();
        rectTransform.SetAsLastSibling();

        if (!isDragging && !isReturning)
            rectTransform.anchoredPosition = GetCurrentHomePosition();

        ShowHoverSkillLabel();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerHovering = false;
        if (!isDragging)
            RestoreSiblingIndexIfNeeded();

        if (!isDragging && !isReturning)
            rectTransform.anchoredPosition = startAnchoredPosition;

        SetHoverSkillLabel(string.Empty);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!isInteractable || isDragLocked)
            return;

        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (dragParentRect == null)
            return;

        if (cardValue < 1 || cardValue > 6)
        {
            Debug.LogWarning($"{name}: cardValue 가 잘못되었습니다. 현재 값 = {cardValue}");
            return;
        }

        Debug.Log($"{name}: 드래그 시작 / cardValue = {cardValue}");

        StoreSiblingIndexIfNeeded();
        rectTransform.SetAsLastSibling();

        isDragging = true;
        isReturning = false;
        ApplyInteractableVisual();

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dragParentRect,
            eventData.position,
            owner != null ? owner.GetUiEventCamera() : eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            pointerOffsetY = rectTransform.anchoredPosition.y - localPoint.y;
        }
        else
        {
            pointerOffsetY = 0f;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        if (!isInteractable || isDragLocked)
            return;

        if (dragParentRect == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            dragParentRect,
            eventData.position,
            owner != null ? owner.GetUiEventCamera() : eventData.pressEventCamera,
            out Vector2 localPoint))
        {
            return;
        }

        float targetY = localPoint.y + pointerOffsetY;

        if (targetY < startAnchoredPosition.y)
            targetY = startAnchoredPosition.y;

        rectTransform.anchoredPosition = new Vector2(startAnchoredPosition.x, targetY);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
            return;

        isDragging = false;
        ApplyInteractableVisual();

        if (owner == null)
        {
            BeginReturnToStart();
            return;
        }

        owner.NotifyCardReleased(this);
    }

    private Vector2 GetCurrentHomePosition()
    {
        if (!CanApplyHover() || !isPointerHovering)
            return startAnchoredPosition;

        return startAnchoredPosition + Vector2.up * hoverLift;
    }

    private bool CanApplyHover()
    {
        return isInteractable && !isDragLocked && rectTransform != null;
    }

    private void StoreSiblingIndexIfNeeded()
    {
        if (hasStoredSiblingIndex || rectTransform == null)
            return;

        storedSiblingIndex = rectTransform.GetSiblingIndex();
        hasStoredSiblingIndex = true;
    }

    private void RestoreSiblingIndexIfNeeded()
    {
        if (!hasStoredSiblingIndex || rectTransform == null || rectTransform.parent == null)
            return;

        int maxSiblingIndex = Mathf.Max(0, rectTransform.parent.childCount - 1);
        rectTransform.SetSiblingIndex(Mathf.Clamp(storedSiblingIndex, 0, maxSiblingIndex));
        hasStoredSiblingIndex = false;
    }

    private void ClearHoverState()
    {
        isPointerHovering = false;
        RestoreSiblingIndexIfNeeded();
        SetHoverSkillLabel(string.Empty);
    }

    private void ApplyInteractableVisual()
    {
        if (canvasGroup == null)
            return;

        if (!isInteractable || isDragLocked)
            ClearHoverState();

        canvasGroup.alpha = isInteractable ? 1f : disabledAlpha;
        canvasGroup.interactable = isInteractable && !isDragLocked;
        canvasGroup.blocksRaycasts = isInteractable && !isDragLocked && !isDragging;
    }

    private void EnsureHoverSkillLabel()
    {
        if (hoverSkillLabelRoot != null)
            return;

        GameObject labelObject = new GameObject("Hover Skill Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        labelObject.transform.SetParent(transform, false);

        hoverSkillLabelRoot = (RectTransform)labelObject.transform;
        hoverSkillLabelRoot.anchorMin = new Vector2(0.5f, 1f);
        hoverSkillLabelRoot.anchorMax = new Vector2(0.5f, 1f);
        hoverSkillLabelRoot.pivot = new Vector2(0.5f, 0f);
        hoverSkillLabelRoot.sizeDelta = hoverSkillLabelSize;
        hoverSkillLabelRoot.anchoredPosition = new Vector2(0f, hoverSkillLabelOffsetY);

        Image background = labelObject.GetComponent<Image>();
        background.color = hoverSkillLabelBackgroundColor;
        background.raycastTarget = false;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(labelObject.transform, false);

        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 4f);
        textRect.offsetMax = new Vector2(-10f, -4f);

        hoverSkillLabelText = textObject.GetComponent<TextMeshProUGUI>();
        hoverSkillLabelText.alignment = TextAlignmentOptions.Center;
        hoverSkillLabelText.color = hoverSkillLabelTextColor;
        hoverSkillLabelText.fontSize = hoverSkillLabelFontSize;
        hoverSkillLabelText.fontStyle = FontStyles.Bold;
        hoverSkillLabelText.textWrappingMode = TextWrappingModes.NoWrap;
        hoverSkillLabelText.raycastTarget = false;

        SetHoverSkillLabel(string.Empty);
    }

    private void EnsureArtworkLayers()
    {
        if (cardFaceImage == null)
            cardFaceImage = GetComponent<Image>();

        if (cardFaceImage != null)
            cardFaceImage.raycastTarget = true;

        if (cardValueLabel == null)
        {
            Transform existingLabel = transform.Find("Card Value Label");
            if (existingLabel != null)
                cardValueLabel = existingLabel.GetComponent<TMP_Text>();
        }

        if (cardValueLabel == null)
        {
            GameObject labelObject = new GameObject("Card Value Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(transform, false);

            RectTransform labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            cardValueLabel = labelObject.GetComponent<TextMeshProUGUI>();
            cardValueLabel.alignment = TextAlignmentOptions.Center;
            cardValueLabel.fontStyle = FontStyles.Bold;
            cardValueLabel.raycastTarget = false;
        }

        if (cardOutlineImage == null)
        {
            Transform existingOutline = transform.Find("Card Outline");
            if (existingOutline != null)
                cardOutlineImage = existingOutline.GetComponent<Image>();
        }

        if (cardOutlineImage == null)
        {
            GameObject outlineObject = new GameObject("Card Outline", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            outlineObject.transform.SetParent(transform, false);

            RectTransform outlineRect = (RectTransform)outlineObject.transform;
            outlineRect.anchorMin = Vector2.zero;
            outlineRect.anchorMax = Vector2.one;
            outlineRect.offsetMin = Vector2.zero;
            outlineRect.offsetMax = Vector2.zero;

            cardOutlineImage = outlineObject.GetComponent<Image>();
        }

        if (cardOutlineImage != null)
        {
            cardOutlineImage.raycastTarget = false;
            cardOutlineImage.transform.SetAsLastSibling();
        }
    }

    private void RefreshCardValueLabel()
    {
        if (cardValueLabel == null)
            return;

        cardValueLabel.text = cardValue.ToString();
        cardValueLabel.color = cardValueLabelColor;
        cardValueLabel.fontSize = cardValueLabelFontSize;
        cardValueLabel.alignment = TextAlignmentOptions.Center;
        cardValueLabel.raycastTarget = false;
    }

    private void ShowHoverSkillLabel()
    {
        string skillName = owner != null ? owner.GetHoverSkillNameText(cardValue) : string.Empty;
        SetHoverSkillLabel(skillName);
    }

    private void SetHoverSkillLabel(string skillName)
    {
        EnsureHoverSkillLabel();

        bool visible = !string.IsNullOrWhiteSpace(skillName);
        hoverSkillLabelRoot.gameObject.SetActive(visible);
        if (hoverSkillLabelText != null)
            hoverSkillLabelText.text = visible ? skillName : string.Empty;
    }
}
