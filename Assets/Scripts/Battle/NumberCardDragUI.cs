using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class NumberCardDragUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Card")]
    [SerializeField, Range(1, 6)] private int cardValue = 1;
    [SerializeField] private float returnSpeed = 1800f;
    [SerializeField, Range(0f, 1f)] private float disabledAlpha = 0.35f;

    [Tooltip("비워두면 부모 RectTransform 을 사용합니다.")]
    [SerializeField] private RectTransform dragParentRect;

    private readonly Vector3[] worldCorners = new Vector3[4];

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private CardDrawPopupController owner;

    private Vector2 startAnchoredPosition;
    private float pointerOffsetY;
    private bool isInteractable = true;
    private bool isDragLocked;
    private bool isDragging;
    private bool isReturning;

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
        ApplyInteractableVisual();
    }

    private void Update()
    {
        if (!isReturning)
            return;

        rectTransform.anchoredPosition = Vector2.MoveTowards(
            rectTransform.anchoredPosition,
            startAnchoredPosition,
            returnSpeed * Time.unscaledDeltaTime);

        if ((rectTransform.anchoredPosition - startAnchoredPosition).sqrMagnitude <= 0.0001f)
        {
            rectTransform.anchoredPosition = startAnchoredPosition;
            isReturning = false;
        }
    }

    public void Bind(CardDrawPopupController controller)
    {
        owner = controller;

        if (dragParentRect == null)
            dragParentRect = rectTransform.parent as RectTransform;
    }

    public void SetInteractable(bool canUse)
    {
        isInteractable = canUse;

        if (!isInteractable)
        {
            isDragging = false;
            isReturning = false;
            ResetToStartImmediate();
        }

        ApplyInteractableVisual();
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
        ApplyInteractableVisual();
    }

    public void BeginReturnToStart()
    {
        isDragging = false;
        isReturning = true;
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

    private void ApplyInteractableVisual()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = isInteractable ? 1f : disabledAlpha;
        canvasGroup.interactable = isInteractable && !isDragLocked;
        canvasGroup.blocksRaycasts = isInteractable && !isDragLocked && !isDragging;
    }
}
