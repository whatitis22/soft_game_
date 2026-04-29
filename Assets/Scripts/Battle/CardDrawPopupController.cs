using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public class CardSubmittedEvent : UnityEvent<int> { }

public class CardDrawPopupController : MonoBehaviour
{
    [Header("Popup")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private RectTransform submitLineRect;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private NumberCardDragUI[] cards;

    [Header("Open Button")]
    [SerializeField] private Selectable openPopupButton;

    [Header("Refresh Button")]
    [SerializeField] private Button refreshButton;
    [SerializeField] private bool autoCreateRefreshButton = true;
    [SerializeField] private string refreshButtonLabel = "새로고침";
    [SerializeField] private Vector2 refreshButtonSize = new Vector2(140f, 42f);
    [SerializeField] private Vector2 refreshButtonAnchoredPosition = new Vector2(-92f, -48f);

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
    private int locallyCanceledSubmittedCard = -1;
    private bool dragLockedBySubmittedCard;
    private CardPopupRightClickCloser rightClickCloser;

    public bool IsOpen => popupRoot != null && popupRoot.activeSelf;
    public bool HasTemporarySelection => temporarySelectedNumber >= 1 && temporarySelectedNumber <= 6;
    public int TemporarySelectedNumber => temporarySelectedNumber;

    private void Awake()
    {
        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        AutoCollectCardsIfNeeded();
        BindCards();
        EnsureRefreshButton();
        BindRefreshButton();
        EnsureRightClickCloser();

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

    private void OnDestroy()
    {
        if (refreshButton != null)
            refreshButton.onClick.RemoveListener(OnClickRefreshButton);
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (IsRightClickPressedThisFrame())
            CancelAndClosePopup();
    }

    public void OpenPopup()
    {
        if (popupRoot == null)
        {
            Debug.LogWarning("CardDrawPopupController: popupRoot 가 비어 있습니다.");
            return;
        }

        popupRoot.SetActive(true);
        SetOpenButtonInteractable(false);

        AutoCollectCardsIfNeeded();
        BindCards();
        EnsureRefreshButton();
        BindRefreshButton();
        EnsureRightClickCloser();
        SyncLocalPlayerCardState();

        if (resetCardPositionsOnOpen)
            ResetAllCardsImmediate();

        RefreshCardInteractableState();
    }

    public void CancelAndClosePopup()
    {
        ClosePopup(reactivateOpenButton: true);
    }

    public void ClosePopupAfterSubmit()
    {
        ClosePopup(reactivateOpenButton: true);
    }

    public void ClosePopup(bool reactivateOpenButton)
    {
        if (resetCardPositionsOnClose)
            ResetAllCardsImmediate();

        if (popupRoot != null)
            popupRoot.SetActive(false);

        if (reactivateOpenButton)
            SetOpenButtonInteractable(true);
    }

    public void SetOpenButtonInteractable(bool interactable)
    {
        if (openPopupButton != null)
        {
            openPopupButton.interactable = interactable;
            openPopupButton.gameObject.SetActive(interactable);
        }
    }

    public void ClearTemporarySelection()
    {
        temporarySelectedNumber = -1;
    }

    public void RejectTemporarySubmission()
    {
        if (HasTemporarySelection)
            currentRemainingCardMask = ReturnCardToMask(currentRemainingCardMask, temporarySelectedNumber);

        // 서버 제출까지 가지 못한 임시 선택은 원래 카드 상태로 되돌린다.
        temporarySelectedNumber = -1;
        SyncLocalPlayerCardState();
        RefreshCardInteractableState();
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

        if (IsRoundLocked())
            return false;

        if (dragLockedBySubmittedCard)
            return false;

        if (!IsCardUsable(card.CardValue))
            return false;

        if (submitLineRect == null)
            return false;

        float cardBottomY = card.GetBottomWorldY();
        float lineTopY = GetSubmitLineTopWorldY();

        return cardBottomY > lineTopY;
    }

    public void RefreshSubmittedCard()
    {
        if (IsRoundLocked())
        {
            GameStatusMessageRouter.Show("라운드가 잠겨 카드 제출을 변경할 수 없습니다.");
            SyncLocalPlayerCardState();
            RefreshCardInteractableState();
            return;
        }

        if (!TryGetLocalPlayerState(out NetworkPlayerState localPlayer))
            return;

        int submittedCard = localPlayer.SubmittedCard.Value;
        if (!IsValidCardValue(submittedCard))
        {
            if (HasTemporarySelection)
            {
                submittedCard = temporarySelectedNumber;
                localPlayer.RequestCancelSubmittedCardRpc();
                locallyCanceledSubmittedCard = submittedCard;
                temporarySelectedNumber = -1;
                currentRemainingCardMask = ReturnCardToMask(currentRemainingCardMask, submittedCard);
                dragLockedBySubmittedCard = false;
                RefreshCardInteractableState();
                return;
            }

            SyncLocalPlayerCardState();
            RefreshCardInteractableState();
            return;
        }

        // 새로고침은 서버에 제출 취소를 요청하고, 로컬 UI는 즉시 카드 반환처럼 보이게 맞춘다.
        localPlayer.RequestCancelSubmittedCardRpc();
        locallyCanceledSubmittedCard = submittedCard;
        temporarySelectedNumber = -1;
        currentRemainingCardMask = ReturnCardToMask(currentRemainingCardMask, submittedCard);
        dragLockedBySubmittedCard = false;
        RefreshCardInteractableState();
    }

    private void SubmitCard(NumberCardDragUI card)
    {
        if (card == null)
            return;

        if (card.CardValue < 1 || card.CardValue > 6)
        {
            Debug.LogWarning($"CardDrawPopupController: {card.name} 의 CardValue 가 잘못되었습니다. ({card.CardValue})");
            card.BeginReturnToStart();
            return;
        }

        temporarySelectedNumber = card.CardValue;
        Debug.Log($"카드 제출 감지: {card.name} / 값 = {temporarySelectedNumber}");

        currentRemainingCardMask = ConsumeCardFromMask(currentRemainingCardMask, temporarySelectedNumber);
        dragLockedBySubmittedCard = true;
        RefreshCardInteractableState();

        onCardSubmitted.Invoke(temporarySelectedNumber);

        if (closeOnSubmit)
        {
            ClosePopupAfterSubmit();
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
            cards[i].SetDragLocked(dragLockedBySubmittedCard);
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

    private void EnsureRefreshButton()
    {
        if (refreshButton != null || !autoCreateRefreshButton || popupRoot == null)
            return;

        GameObject buttonObject = new GameObject("Card Refresh Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(popupRoot.transform, false);

        RectTransform buttonRect = (RectTransform)buttonObject.transform;
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.sizeDelta = refreshButtonSize;
        buttonRect.anchoredPosition = refreshButtonAnchoredPosition;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(1f, 1f, 1f, 0.92f);

        refreshButton = buttonObject.GetComponent<Button>();

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = refreshButtonLabel;
        label.fontSize = 24f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.black;
    }

    private void BindRefreshButton()
    {
        if (refreshButton == null)
            return;

        // 자동 생성/수동 연결 어느 쪽이든 리스너가 중복되지 않게 다시 묶는다.
        refreshButton.onClick.RemoveListener(OnClickRefreshButton);
        refreshButton.onClick.AddListener(OnClickRefreshButton);
    }

    private void EnsureRightClickCloser()
    {
        if (popupRoot == null)
            return;

        if (rightClickCloser == null)
            rightClickCloser = popupRoot.GetComponent<CardPopupRightClickCloser>();

        if (rightClickCloser == null)
            rightClickCloser = popupRoot.AddComponent<CardPopupRightClickCloser>();

        // 팝업 버튼 오브젝트가 꺼져 컨트롤러 Update가 멈춰도, 팝업 루트가 우클릭 닫기를 대신 감시한다.
        rightClickCloser.Bind(this);
    }

    private void OnClickRefreshButton()
    {
        RefreshSubmittedCard();
    }

    private void SyncLocalPlayerCardState()
    {
        if (!TryGetLocalPlayerState(out NetworkPlayerState localPlayer))
        {
            dragLockedBySubmittedCard = HasTemporarySelection;
            if (HasTemporarySelection)
                currentRemainingCardMask = ConsumeCardFromMask(currentRemainingCardMask, temporarySelectedNumber);

            return;
        }

        currentRemainingCardMask = (byte)(localPlayer.RemainingNumberCardsMask.Value & 0b0011_1111);

        int submittedCard = localPlayer.SubmittedCard.Value;
        if (IsValidCardValue(submittedCard) && submittedCard == locallyCanceledSubmittedCard)
        {
            submittedCard = -1;
            currentRemainingCardMask = ReturnCardToMask(currentRemainingCardMask, locallyCanceledSubmittedCard);
        }
        else if (!IsValidCardValue(submittedCard))
        {
            locallyCanceledSubmittedCard = -1;
        }

        if (!IsValidCardValue(submittedCard) && HasTemporarySelection)
            submittedCard = temporarySelectedNumber;

        dragLockedBySubmittedCard = IsRoundLocked() || IsValidCardValue(submittedCard);

        if (IsValidCardValue(submittedCard))
            currentRemainingCardMask = ConsumeCardFromMask(currentRemainingCardMask, submittedCard);
    }

    private bool TryGetLocalPlayerState(out NetworkPlayerState localPlayer)
    {
        return NetworkPlayerRegistry.TryGetLocalPlayer(out localPlayer);
    }

    private bool IsRoundLocked()
    {
        return NetworkSessionState.Instance != null && NetworkSessionState.Instance.IsRoundLocked.Value;
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

    private byte ConsumeCardFromMask(byte sourceMask, int cardValue)
    {
        if (!IsValidCardValue(cardValue))
            return sourceMask;

        int bit = 1 << (cardValue - 1);
        return (byte)(sourceMask & ~bit);
    }

    private byte ReturnCardToMask(byte sourceMask, int cardValue)
    {
        if (!IsValidCardValue(cardValue))
            return sourceMask;

        int bit = 1 << (cardValue - 1);
        return (byte)(sourceMask | bit);
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

public class CardPopupRightClickCloser : MonoBehaviour
{
    private CardDrawPopupController owner;

    public void Bind(CardDrawPopupController controller)
    {
        owner = controller;
    }

    private void Update()
    {
        if (owner == null || !owner.IsOpen)
            return;

        if (IsRightClickPressedThisFrame())
            owner.CancelAndClosePopup();
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
