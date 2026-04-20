using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class CardSubmittedEvent : UnityEvent<int> { }

public class CardDrawPopupController : MonoBehaviour
{
    [Header("Popup")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private RectTransform submitLineRect;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private NumberCardDragUI[] cards;

    [Header("Behavior")]
    [SerializeField] private bool closeOnSubmit = true;
    [SerializeField] private bool resetCardPositionsOnOpen = true;
    [SerializeField] private bool resetCardPositionsOnClose = true;

    [Tooltip("1~6 카드 보유 상태. 기본값은 0b0011_1111 = 전부 사용 가능")]
    [SerializeField, Range(0, 63)] private int initialRemainingCardMask = 0b0011_1111;

    [Header("Events")]
    [SerializeField] private CardSubmittedEvent onCardSubmitted = new CardSubmittedEvent();

    private readonly Vector3[] lineCorners = new Vector3[4];

    private byte currentRemainingCardMask;
    private int temporarySelectedNumber = -1;

    public bool IsOpen => popupRoot != null && popupRoot.activeSelf;
    public bool HasTemporarySelection => temporarySelectedNumber >= 1 && temporarySelectedNumber <= 6;
    public int TemporarySelectedNumber => temporarySelectedNumber;

    private void Awake()
    {
        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        AutoCollectCardsIfNeeded();
        BindCards();

        currentRemainingCardMask = (byte)(initialRemainingCardMask & 0b0011_1111);

        if (IsOpen)
        {
            if (resetCardPositionsOnOpen)
                ResetAllCardsImmediate();

            RefreshCardInteractableState();
        }
    }

    private void OnValidate()
    {
        initialRemainingCardMask &= 0b0011_1111;

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        AutoCollectCardsIfNeeded();
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (IsRightClickPressedThisFrame())
            ClosePopup();
    }

    public void OpenPopup()
    {
        if (popupRoot == null)
        {
            Debug.LogWarning("CardDrawPopupController: popupRoot 가 비어 있습니다.");
            return;
        }

        popupRoot.SetActive(true);

        AutoCollectCardsIfNeeded();
        BindCards();

        if (resetCardPositionsOnOpen)
            ResetAllCardsImmediate();

        RefreshCardInteractableState();
    }

    public void ClosePopup()
    {
        if (resetCardPositionsOnClose)
            ResetAllCardsImmediate();

        if (popupRoot != null)
            popupRoot.SetActive(false);
    }

    public void ClearTemporarySelection()
    {
        temporarySelectedNumber = -1;
    }

    public bool TryGetTemporarySelectedNumber(out int number)
    {
        number = temporarySelectedNumber;
        return HasTemporarySelection;
    }

    public void SetRemainingCardMask(byte mask)
    {
        currentRemainingCardMask = (byte)(mask & 0b0011_1111);
        RefreshCardInteractableState();
    }

    public void ResetRemainingCardMask()
    {
        currentRemainingCardMask = (byte)(initialRemainingCardMask & 0b0011_1111);
        RefreshCardInteractableState();
    }

    public void MarkCardAsUsed(int cardValue)
    {
        if (!IsValidCardValue(cardValue))
            return;

        int bit = 1 << (cardValue - 1);
        currentRemainingCardMask = (byte)(currentRemainingCardMask & ~bit);
        RefreshCardInteractableState();
    }

    public bool IsCardUsable(int cardValue)
    {
        if (!IsValidCardValue(cardValue))
            return false;

        int bit = 1 << (cardValue - 1);
        return (currentRemainingCardMask & bit) != 0;
    }

    public Camera GetUiEventCamera()
    {
        if (rootCanvas == null)
            return null;

        if (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return rootCanvas.worldCamera;
    }

    public void NotifyCardReleased(NumberCardDragUI card)
    {
        if (card == null)
            return;

        if (CanSubmit(card))
        {
            SubmitCard(card);
            return;
        }

        card.BeginReturnToStart();
    }

    public bool CanSubmit(NumberCardDragUI card)
    {
        if (card == null)
            return false;

        if (!IsCardUsable(card.CardValue))
            return false;

        if (submitLineRect == null)
            return false;

        float cardBottomY = card.GetBottomWorldY();
        float lineTopY = GetSubmitLineTopWorldY();

        return cardBottomY > lineTopY;
    }

    private void SubmitCard(NumberCardDragUI card)
    {
        temporarySelectedNumber = card.CardValue;
        onCardSubmitted.Invoke(temporarySelectedNumber);

        if (closeOnSubmit)
        {
            ClosePopup();
        }
        else
        {
            card.BeginReturnToStart();
        }
    }

    private float GetSubmitLineTopWorldY()
    {
        if (submitLineRect == null)
            return float.MaxValue;

        submitLineRect.GetWorldCorners(lineCorners);

        float maxY = lineCorners[0].y;
        for (int i = 1; i < lineCorners.Length; i++)
        {
            if (lineCorners[i].y > maxY)
                maxY = lineCorners[i].y;
        }

        return maxY;
    }

    private void RefreshCardInteractableState()
    {
        AutoCollectCardsIfNeeded();
        BindCards();

        if (cards == null)
            return;

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null)
                continue;

            cards[i].SetInteractable(IsCardUsable(cards[i].CardValue));
        }
    }

    private void ResetAllCardsImmediate()
    {
        AutoCollectCardsIfNeeded();

        if (cards == null)
            return;

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null)
                continue;

            cards[i].ResetToStartImmediate();
        }
    }

    private void BindCards()
    {
        if (cards == null)
            return;

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null)
                continue;

            cards[i].Bind(this);
        }
    }

    private void AutoCollectCardsIfNeeded()
    {
        if (cards != null && cards.Length > 0)
            return;

        if (popupRoot != null)
        {
            cards = popupRoot.GetComponentsInChildren<NumberCardDragUI>(true);
            return;
        }

        cards = GetComponentsInChildren<NumberCardDragUI>(true);
    }

    private bool IsValidCardValue(int cardValue)
    {
        return cardValue >= 1 && cardValue <= 6;
    }

    private bool IsRightClickPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null &&
            UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetMouseButtonDown(1))
            return true;
#endif

        return false;
    }
}