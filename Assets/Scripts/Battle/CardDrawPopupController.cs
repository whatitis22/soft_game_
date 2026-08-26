using System;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public class CardSubmittedEvent : UnityEvent<int> { }

public class CardDrawPopupController : MonoBehaviour
{
    public static CardDrawPopupController Instance { get; private set; }

    [Header("Popup")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private RectTransform submitLineRect;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private NumberCardDragUI[] cards;

    [Header("Card Artwork")]
    [SerializeField] private Sprite cardFrontSprite;
    [SerializeField] private Sprite cardBackSprite;
    [SerializeField] private Sprite cardOutlineSprite;

    [Header("Open Button")]
    [SerializeField] private Selectable openPopupButton;

    [Header("Refresh Button")]
    [SerializeField] private Button refreshButton;
    [SerializeField] private bool autoCreateRefreshButton = true;
    [SerializeField] private string refreshButtonLabel = "새로고침";
    [SerializeField] private Vector2 refreshButtonSize = new Vector2(140f, 42f);
    [SerializeField] private Vector2 refreshButtonAnchoredPosition = new Vector2(-92f, -48f);

    [Header("Active Skill Button")]
    [SerializeField] private Button revelationButton;
    [SerializeField] private bool autoCreateRevelationButton = true;
    [SerializeField] private string revelationButtonLabel = "계시";
    [SerializeField] private Vector2 revelationButtonSize = new Vector2(160f, 54f);
    [SerializeField] private Vector2 revelationButtonAnchoredPosition = new Vector2(0f, -150f);

    [Header("Card Fan Layout")]
    [SerializeField] private bool useFanLayout = true;
    [SerializeField] private Vector2 fanCenter = new Vector2(0f, -360f);
    [SerializeField] private Vector2 fanCardSize = new Vector2(210f, 286f);
    [SerializeField, Min(1f)] private float fanCardSpacing = 118f;
    [SerializeField, Range(0f, 30f)] private float fanMaxRotation = 13f;
    [SerializeField, Range(0f, 12f)] private float fanMinRotation = 4f;
    [SerializeField, Min(0f)] private float fanEdgeDrop = 34f;
    [SerializeField, Min(0f)] private float fanCenterLift = 0f;

    [Header("Behavior")]
    [SerializeField] private bool openOnSceneEntry = true;
    [SerializeField] private bool closeOnSubmit = true;
    [SerializeField] private bool resetCardPositionsOnOpen = true;
    [SerializeField] private bool resetCardPositionsOnClose = true;

    [Tooltip("1~6 카드 보유 상태. 기본값은 0b0011_1111 = 전부 사용 가능")]
    [SerializeField, Range(0, 63)] private int initialRemainingCardMask = 0b0011_1111;

    [Header("Events")]
    [SerializeField] private CardSubmittedEvent onCardSubmitted = new CardSubmittedEvent();

    private readonly Vector3[] lineCorners = new Vector3[4];
    private readonly List<NumberCardDragUI> layoutCards = new List<NumberCardDragUI>();

    private byte currentRemainingCardMask;
    private int temporarySelectedNumber = -1;
    private int locallyCanceledSubmittedCard = -1;
    private bool dragLockedBySubmittedCard;
    private bool localSubmissionPending;
    private bool focusHidden;
    private bool restorePopupWasOpen;
    private bool restoreOpenButtonWasActive;
    private bool restoreOpenButtonWasInteractable;
    private NetworkPlayerState observedLocalPlayer;
    private NetworkSessionState observedSession;
    private NetworkVariable<bool>.OnValueChangedDelegate roundLockChangedHandler;
    private CardPopupRightClickCloser rightClickCloser;

    public bool IsOpen => popupRoot != null && popupRoot.activeSelf;
    public bool HasTemporarySelection => temporarySelectedNumber >= 1 && temporarySelectedNumber <= 6;
    public int TemporarySelectedNumber => temporarySelectedNumber;
    public Sprite CardFrontSprite => cardFrontSprite;
    public Sprite CardBackSprite => cardBackSprite;
    public Sprite CardOutlineSprite => cardOutlineSprite;

    public static CardDrawPopupController GetAny()
    {
        if (Instance != null)
            return Instance;

        return FindAnyObjectByType<CardDrawPopupController>(FindObjectsInactive.Include);
    }

    public void SetFocusHidden(bool hidden)
    {
        if (focusHidden == hidden)
            return;

        focusHidden = hidden;

        if (hidden)
        {
            restorePopupWasOpen = popupRoot != null && popupRoot.activeSelf;
            restoreOpenButtonWasActive = openPopupButton != null && openPopupButton.gameObject.activeSelf;
            restoreOpenButtonWasInteractable = openPopupButton != null && openPopupButton.interactable;

            if (popupRoot != null)
                popupRoot.SetActive(false);

            if (openPopupButton != null)
                openPopupButton.gameObject.SetActive(false);

            return;
        }

        if (popupRoot != null)
            popupRoot.SetActive(restorePopupWasOpen);

        if (openPopupButton != null)
        {
            openPopupButton.interactable = restoreOpenButtonWasInteractable;
            openPopupButton.gameObject.SetActive(restoreOpenButtonWasActive);
        }

        if (restorePopupWasOpen)
            RefreshCardInteractableState();

        RefreshOpenButtonVisibility();
    }

    public void RestoreForBattleSceneEntry()
    {
        focusHidden = false;
        restorePopupWasOpen = false;
        restoreOpenButtonWasActive = false;
        restoreOpenButtonWasInteractable = false;

        BindObservedSessionIfNeeded();
        BindObservedLocalPlayerIfNeeded();
        SyncLocalPlayerCardState();
        RefreshCardInteractableState();
        RefreshOpenButtonVisibility();
        RefreshRevelationButtonVisibility();

        if (openOnSceneEntry && !IsOpen && !IsRoundLocked())
            OpenPopup();
    }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        currentRemainingCardMask = (byte)(initialRemainingCardMask & 0b0011_1111);

        AutoCollectCardsIfNeeded();
        BindCards();
        RefreshCardInteractableState();
        EnsureRefreshButton();
        BindRefreshButton();
        EnsureRevelationButton();
        BindRevelationButton();
        EnsureRightClickCloser();
        BindObservedSessionIfNeeded();

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
        ApplyFanLayout();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (refreshButton != null)
            refreshButton.onClick.RemoveListener(OnClickRefreshButton);

        if (revelationButton != null)
            revelationButton.onClick.RemoveListener(OnClickRevelationButton);

        UnbindObservedLocalPlayer();
        UnbindObservedSession();
    }

    private void Start()
    {
        if (!openOnSceneEntry)
            return;

        OpenPopup();
    }

    private void Update()
    {
        BindObservedSessionIfNeeded();
        BindObservedLocalPlayerIfNeeded();
        RefreshOpenButtonVisibility();
        RefreshRevelationButtonVisibility();

        if (!IsOpen)
            return;

        if (IsRightClickPressedThisFrame())
            CancelAndClosePopup();
    }

    public void OpenPopup()
    {
        if (focusHidden)
            return;

        if (IsRoundLocked())
            return;

        if (popupRoot == null)
        {
            Debug.LogWarning("CardDrawPopupController: popupRoot 가 비어 있습니다.");
            return;
        }

        BindObservedSessionIfNeeded();
        BindObservedLocalPlayerIfNeeded();

        popupRoot.SetActive(true);
        SetOpenButtonInteractable(false);

        AutoCollectCardsIfNeeded();
        BindCards();
        EnsureRefreshButton();
        BindRefreshButton();
        EnsureRevelationButton();
        BindRevelationButton();
        EnsureRightClickCloser();
        SyncLocalPlayerCardState();
        RefreshCardInteractableState();
        RefreshRevelationButtonVisibility();

        if (resetCardPositionsOnOpen)
            ResetAllCardsImmediate();
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
        if (focusHidden)
            return;

        if (openPopupButton != null)
        {
            bool visible = interactable && CanShowOpenPopupButton();
            openPopupButton.interactable = visible;
            openPopupButton.gameObject.SetActive(visible);
        }
    }

    public void ClearTemporarySelection()
    {
        temporarySelectedNumber = -1;
        localSubmissionPending = false;
    }

    public void RejectTemporarySubmission()
    {
        if (HasTemporarySelection)
            currentRemainingCardMask = ReturnCardToMask(currentRemainingCardMask, temporarySelectedNumber);

        // 서버 제출까지 가지 못한 임시 선택은 원래 카드 상태로 되돌린다.
        temporarySelectedNumber = -1;
        localSubmissionPending = false;
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

    public string GetHoverSkillNameText(int cardValue)
    {
        if (!TryGetLocalPlayerState(out NetworkPlayerState localPlayer) || localPlayer == null)
            return string.Empty;

        if (!IsValidCardValue(cardValue))
            return string.Empty;

        return localPlayer.Job.Value switch
        {
            JobType.Hero => GetKnightHoverSkillName(cardValue, localPlayer),
            _ => string.Empty
        };
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
        ApplyFanLayout();
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
                if (localSubmissionPending && PlayerMaskContainsCard(localPlayer, temporarySelectedNumber))
                {
                    submittedCard = temporarySelectedNumber;
                    localPlayer.RequestCancelSubmittedCardRpc();
                    locallyCanceledSubmittedCard = submittedCard;
                    ClearLocalSubmittedCardState(clearCanceledMarker: false);
                    currentRemainingCardMask = ReturnCardToMask(currentRemainingCardMask, submittedCard);
                    RefreshCardInteractableState();
                }
                else
                {
                    ClearLocalSubmittedCardState(clearCanceledMarker: true);
                    SyncLocalPlayerCardState();
                    ResetAllCardsImmediate();
                    RefreshCardInteractableState();
                }

                return;
            }

            SyncLocalPlayerCardState();
            RefreshCardInteractableState();
            return;
        }

        if (localPlayer.IsSubmittedCardLocked.Value)
        {
            GameStatusMessageRouter.Show("계시로 제출한 카드는 취소할 수 없습니다.");
            SyncLocalPlayerCardState();
            RefreshCardInteractableState();
            return;
        }

        // 새로고침은 서버에 제출 취소를 요청하고, 로컬 UI는 즉시 카드 반환처럼 보이게 맞춘다.
        localPlayer.RequestCancelSubmittedCardRpc();
        locallyCanceledSubmittedCard = submittedCard;
        ClearLocalSubmittedCardState(clearCanceledMarker: false);
        currentRemainingCardMask = ReturnCardToMask(currentRemainingCardMask, submittedCard);
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
        localSubmissionPending = true;
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

            bool usable = IsCardUsable(cards[i].CardValue);
            cards[i].SetVisible(usable);

            if (!usable)
                continue;

            cards[i].SetInteractable(true);
            cards[i].SetDragLocked(dragLockedBySubmittedCard);
        }

        ApplyFanLayout();
        RefreshRevelationButtonVisibility();
    }

    private void ResetAllCardsImmediate()
    {
        AutoCollectCardsIfNeeded();
        ApplyFanLayout();

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
            cards[i].ApplyArtwork(cardFrontSprite, cardOutlineSprite);
        }
    }

    private void ApplyFanLayout()
    {
        if (!useFanLayout || cards == null || cards.Length == 0)
            return;

        Array.Sort(cards, CompareCardsByValue);

        layoutCards.Clear();
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] != null && cards[i].gameObject.activeSelf)
                layoutCards.Add(cards[i]);
        }

        int count = layoutCards.Count;
        if (count == 0)
            return;

        float half = (count - 1) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            NumberCardDragUI card = layoutCards[i];
            if (card == null)
                continue;

            RectTransform cardRect = card.transform as RectTransform;
            if (cardRect == null)
                continue;

            float offsetIndex = i - half;
            float normalized = half > 0f ? offsetIndex / half : 0f;
            float sideAmount = Mathf.Abs(normalized);

            cardRect.sizeDelta = fanCardSize;
            cardRect.SetSiblingIndex(i + 1);
            card.AcceptCurrentSiblingIndex();

            Vector2 position = new Vector2(
                fanCenter.x + offsetIndex * fanCardSpacing,
                fanCenter.y + fanCenterLift * (1f - sideAmount) - fanEdgeDrop * sideAmount);

            float rotation = -normalized * fanMaxRotation;
            if (count > 1 && Mathf.Abs(rotation) < fanMinRotation)
            {
                float direction = Mathf.Approximately(normalized, 0f)
                    ? (i <= half ? 1f : -1f)
                    : Mathf.Sign(rotation);

                rotation = direction * fanMinRotation;
            }

            card.SetHomePose(position, rotation);
        }
    }

    private int CompareCardsByValue(NumberCardDragUI a, NumberCardDragUI b)
    {
        if (a == b)
            return 0;

        if (a == null)
            return 1;

        if (b == null)
            return -1;

        return a.CardValue.CompareTo(b.CardValue);
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

    private void EnsureRevelationButton()
    {
        if (revelationButton != null || !autoCreateRevelationButton || popupRoot == null)
            return;

        GameObject buttonObject = new GameObject("Saint Revelation Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(popupRoot.transform, false);

        RectTransform buttonRect = (RectTransform)buttonObject.transform;
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = revelationButtonSize;
        buttonRect.anchoredPosition = revelationButtonAnchoredPosition;

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(1f, 1f, 1f, 0.94f);

        revelationButton = buttonObject.GetComponent<Button>();

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = revelationButtonLabel;
        label.fontSize = 26f;
        label.fontStyle = FontStyles.Bold;
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

    private void BindRevelationButton()
    {
        if (revelationButton == null)
            return;

        revelationButton.onClick.RemoveListener(OnClickRevelationButton);
        revelationButton.onClick.AddListener(OnClickRevelationButton);
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

    private void OnClickRevelationButton()
    {
        if (!CanUseActiveSkillButton(out NetworkPlayerState localPlayer))
        {
            GameStatusMessageRouter.Show("Active skill cannot be used.");
            RefreshRevelationButtonVisibility();
            return;
        }

        if (localPlayer.Job.Value == JobType.Saint &&
            BattleCurrencyUtility.GetTotalCrystals(localPlayer) < 1)
        {
            GameStatusMessageRouter.Show("보석이 하나 필요합니다!");
            RefreshRevelationButtonVisibility();
            return;
        }

        dragLockedBySubmittedCard = true;
        RefreshCardInteractableState();
        RefreshRevelationButtonVisibility();

        if (localPlayer.Job.Value == JobType.Saint)
            localPlayer.RequestUseSaintRevelationRpc();
        else if (localPlayer.Job.Value == JobType.Defender)
            localPlayer.RequestUseDefenderLoanRpc();
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

        if (IsValidCardValue(submittedCard))
        {
            temporarySelectedNumber = submittedCard;
            localSubmissionPending = false;
            dragLockedBySubmittedCard = true;
            currentRemainingCardMask = ConsumeCardFromMask(currentRemainingCardMask, submittedCard);
            return;
        }

        if (HasTemporarySelection)
        {
            bool keepPendingSelection = localSubmissionPending
                && PlayerMaskContainsCard(localPlayer, temporarySelectedNumber)
                && !IsRoundLocked();

            if (keepPendingSelection)
            {
                dragLockedBySubmittedCard = true;
                currentRemainingCardMask = ConsumeCardFromMask(currentRemainingCardMask, temporarySelectedNumber);
                return;
            }

            ClearLocalSubmittedCardState(clearCanceledMarker: true);
        }

        dragLockedBySubmittedCard = IsRoundLocked();
    }

    private void BindObservedLocalPlayerIfNeeded()
    {
        if (observedLocalPlayer != null && !observedLocalPlayer.IsSpawned)
            UnbindObservedLocalPlayer();

        if (!TryGetLocalPlayerState(out NetworkPlayerState localPlayer))
            return;

        if (observedLocalPlayer == localPlayer)
            return;

        UnbindObservedLocalPlayer();
        observedLocalPlayer = localPlayer;
        observedLocalPlayer.SubmittedCard.OnValueChanged += HandleLocalSubmittedCardChanged;
        observedLocalPlayer.RemainingNumberCardsMask.OnValueChanged += HandleLocalRemainingCardMaskChanged;
    }

    private void BindObservedSessionIfNeeded()
    {
        NetworkSessionState session = NetworkSessionState.Instance;
        if (session == null || observedSession == session)
            return;

        UnbindObservedSession();
        observedSession = session;
        roundLockChangedHandler = HandleRoundLockChanged;
        observedSession.IsRoundLocked.OnValueChanged += roundLockChangedHandler;
    }

    private void UnbindObservedLocalPlayer()
    {
        if (observedLocalPlayer == null)
            return;

        observedLocalPlayer.SubmittedCard.OnValueChanged -= HandleLocalSubmittedCardChanged;
        observedLocalPlayer.RemainingNumberCardsMask.OnValueChanged -= HandleLocalRemainingCardMaskChanged;
        observedLocalPlayer = null;
    }

    private void UnbindObservedSession()
    {
        if (observedSession == null || roundLockChangedHandler == null)
        {
            observedSession = null;
            roundLockChangedHandler = null;
            return;
        }

        observedSession.IsRoundLocked.OnValueChanged -= roundLockChangedHandler;
        observedSession = null;
        roundLockChangedHandler = null;
    }

    private void HandleLocalSubmittedCardChanged(int previousValue, int newValue)
    {
        if (IsValidCardValue(newValue))
        {
            temporarySelectedNumber = newValue;
            localSubmissionPending = false;
            locallyCanceledSubmittedCard = -1;
        }
        else if (IsValidCardValue(previousValue))
        {
            ClearLocalSubmittedCardState(clearCanceledMarker: previousValue != locallyCanceledSubmittedCard);
        }

        SyncLocalPlayerCardState();
        ResetAllCardsImmediate();
        RefreshCardInteractableState();
        RefreshCardPanelForCurrentState(previousValue, newValue);
        RefreshRevelationButtonVisibility();
    }

    private void HandleLocalRemainingCardMaskChanged(byte previousValue, byte newValue)
    {
        currentRemainingCardMask = (byte)(newValue & 0b0011_1111);
        SyncLocalPlayerCardState();
        RefreshCardInteractableState();
        RefreshOpenButtonVisibility();
        RefreshRevelationButtonVisibility();
    }

    private void HandleRoundLockChanged(bool previousValue, bool newValue)
    {
        if (newValue)
        {
            if (popupRoot != null)
                popupRoot.SetActive(false);

            SetOpenButtonInteractable(false);
            RefreshRevelationButtonVisibility();
            return;
        }

        RefreshOpenButtonVisibility();
        RefreshRevelationButtonVisibility();

        if (ShouldAutoOpenCards())
            OpenPopup();
    }

    private void RefreshCardPanelForCurrentState(int previousSubmittedCard, int currentSubmittedCard)
    {
        if (focusHidden)
            return;

        if (IsRoundLocked())
        {
            if (popupRoot != null)
                popupRoot.SetActive(false);

            SetOpenButtonInteractable(false);
            return;
        }

        if (!IsValidCardValue(currentSubmittedCard))
        {
            SetOpenButtonInteractable(false);

            if (IsValidCardValue(previousSubmittedCard) || ShouldAutoOpenCards())
                OpenPopup();

            return;
        }

        if (popupRoot != null)
            popupRoot.SetActive(false);

        SetOpenButtonInteractable(true);
    }

    private void RefreshOpenButtonVisibility()
    {
        if (openPopupButton == null || focusHidden)
            return;

        bool visible = CanShowOpenPopupButton();
        openPopupButton.interactable = visible;
        openPopupButton.gameObject.SetActive(visible);
    }

    private void RefreshRevelationButtonVisibility()
    {
        if (revelationButton == null)
            return;

        NetworkPlayerState localPlayer = null;
        bool visible = IsOpen && CanUseActiveSkillButton(out localPlayer);
        if (visible && localPlayer != null)
            SetActiveSkillButtonLabel(localPlayer.Job.Value == JobType.Defender ? "\uB300\uBD80" : revelationButtonLabel);

        revelationButton.interactable = visible;
        revelationButton.gameObject.SetActive(visible);
    }

    private bool CanShowOpenPopupButton()
    {
        if (focusHidden || IsRoundLocked())
            return false;

        if (observedLocalPlayer != null &&
            IsValidCardValue(observedLocalPlayer.SubmittedCard.Value) &&
            !observedLocalPlayer.IsSubmittedCardLocked.Value)
        {
            return true;
        }

        return localSubmissionPending && HasTemporarySelection;
    }

    private bool CanUseActiveSkillButton(out NetworkPlayerState localPlayer)
    {
        localPlayer = null;

        if (focusHidden || IsRoundLocked())
            return false;

        if (dragLockedBySubmittedCard || localSubmissionPending || HasTemporarySelection)
            return false;

        if (!TryGetLocalPlayerState(out localPlayer) || localPlayer == null)
            return false;

        if (localPlayer.SubmittedCard.Value >= 1 && localPlayer.SubmittedCard.Value <= 6)
            return false;

        if (localPlayer.Job.Value == JobType.Saint)
        {
            if (!BattleGimmickDebugSettings.SaintRevelationEnabled)
                return false;
        }
        else if (localPlayer.Job.Value == JobType.Defender)
        {
            if (!BattleGimmickDebugSettings.DefenderLoanEnabled)
                return false;
        }
        else
        {
            return false;
        }

        return (localPlayer.RemainingNumberCardsMask.Value & 0b0011_1111) != 0;
    }

    private void SetActiveSkillButtonLabel(string label)
    {
        if (revelationButton == null)
            return;

        TMP_Text text = revelationButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = string.IsNullOrWhiteSpace(label) ? revelationButtonLabel : label;
    }

    private bool ShouldAutoOpenCards()
    {
        if (focusHidden || IsRoundLocked() || IsOpen)
            return false;

        if (observedLocalPlayer != null && IsValidCardValue(observedLocalPlayer.SubmittedCard.Value))
            return false;

        if (localSubmissionPending && HasTemporarySelection)
            return false;

        return true;
    }

    private void ClearLocalSubmittedCardState(bool clearCanceledMarker)
    {
        temporarySelectedNumber = -1;
        localSubmissionPending = false;
        dragLockedBySubmittedCard = false;

        if (clearCanceledMarker)
            locallyCanceledSubmittedCard = -1;
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

    private string GetKnightHoverSkillName(int cardValue, NetworkPlayerState localPlayer)
    {
        bool duplicatesSubmittedNumber = false;
        int[] submittedCounts = new int[7];

        foreach (NetworkPlayerState player in NetworkPlayerRegistry.GetSpawnedPlayers())
        {
            if (player == null || player == localPlayer || player.Job.Value == JobType.None)
                continue;

            int submittedCard = player.SubmittedCard.Value;
            if (!IsValidCardValue(submittedCard))
                continue;

            submittedCounts[submittedCard]++;
            if (submittedCard == cardValue)
                duplicatesSubmittedNumber = true;
        }

        for (int i = 1; i < submittedCounts.Length; i++)
        {
            if (submittedCounts[i] > 1)
                duplicatesSubmittedNumber = true;
        }

        return duplicatesSubmittedNumber ? "망한 작전" : "계산된 승리";
    }

    private bool PlayerMaskContainsCard(NetworkPlayerState player, int cardValue)
    {
        if (player == null || !IsValidCardValue(cardValue))
            return false;

        int bit = 1 << (cardValue - 1);
        return (player.RemainingNumberCardsMask.Value & bit) != 0;
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
