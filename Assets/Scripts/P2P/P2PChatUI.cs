using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

[DisallowMultipleComponent]
public class P2PChatUI : MonoBehaviour
{
    public static P2PChatUI Instance { get; private set; }

    [Header("초기 접속 UI 루트")]
    [SerializeField] private GameObject hostButtonRoot;
    [SerializeField] private GameObject clientButtonRoot;
    [SerializeField] private GameObject nicknameInputRoot;
    [SerializeField] private GameObject ipInputRoot;
    [SerializeField] private GameObject joinButtonRoot;

    [Header("초기 접속 UI 참조")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button backToNicknameButton;
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private TMP_InputField ipInput;

    [Header("IP 입력 단계")]
    [SerializeField] private bool autoCreateBackToNicknameButton = true;
    [SerializeField] private string backToNicknameButtonLabel = "닉네임 수정";

    [Header("닉네임 입력")]
    [SerializeField] private TMP_Text nicknameCountText;
    [SerializeField] private int nicknameCharacterLimit = 12;

    [Header("상태 텍스트")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private string initialStatusText = "닉네임을 입력하세요.";
    [SerializeField] private float statusClearDelay = 3f;

    [Header("채팅 팝업")]
    [SerializeField] private GameObject chatToggleButtonRoot;
    [SerializeField] private Button chatToggleButton;
    [SerializeField] private GameObject chatPopupRoot;
    [SerializeField] private Button chatPopupCloseButton;

    [Header("채팅 히스토리")]
    [SerializeField] private ScrollRect historyScrollRect;
    [SerializeField] private RectTransform historyContent;
    [SerializeField] private ChatNormalBlockView normalBlockPrefab;
    [SerializeField] private ChatNarrationBlockView narrationBlockPrefab;
    [SerializeField] private GameObject dividerBlockPrefab;

    [Header("채팅 히스토리 표시")]
    [SerializeField, Min(1)] private int historyPageSize = 30;
    [SerializeField] private bool loadOlderOnScrollTop = true;
    [SerializeField, Range(0.9f, 1f)] private float loadOlderScrollThreshold = 0.98f;
    [SerializeField] private Button loadOlderHistoryButton;
    [SerializeField] private TMP_Text historyRangeText;

    [Header("채팅 입력")]
    [SerializeField] private GameObject lobbyThingsRoot;
    [SerializeField] private Button jobSelectButton;
    [SerializeField] private JobSelectionPopupUI jobSelectionPopupUI;
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private Button chatSendButton;
    [SerializeField] private TMP_Text chatCountText;
    [SerializeField] private int chatCharacterLimit = 100;

    private Coroutine statusClearCoroutine;
    private Coroutine scrollCoroutine;
    private readonly List<GameObject> renderedHistoryBlocks = new List<GameObject>();

    private NetworkList<P2PChatEntry> boundHistory;
    private int displayedHistoryStartIndex;
    private int displayedHistoryCount;
    private bool suppressHistoryScrollEvent;

    private int HistoryPageSize => Mathf.Max(1, historyPageSize);

    private void OnValidate()
    {
        historyPageSize = Mathf.Max(1, historyPageSize);
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        // 로비 UI가 살아 있는 동안에는 전역 메시지를 이 상태 텍스트로 받는다.
        GameStatusMessageRouter.RegisterReceiver(SetStatus);
    }

    private void OnDisable()
    {
        GameStatusMessageRouter.UnregisterReceiver(SetStatus);
    }

    private void Start()
    {
        // 별도 버튼을 씬에 배치하지 않아도 닉네임 수정 버튼을 사용할 수 있게 준비한다.
        EnsureBackToNicknameButton();

        hostButton.onClick.AddListener(OnClickHost);
        clientButton.onClick.AddListener(OnClickClient);
        joinButton.onClick.AddListener(OnClickJoin);
        if (backToNicknameButton != null)
            backToNicknameButton.onClick.AddListener(OnClickBackToNickname);

        chatToggleButton.onClick.AddListener(ToggleChatPopup);
        chatPopupCloseButton.onClick.AddListener(CloseChatPopup);
        if (loadOlderHistoryButton != null)
            loadOlderHistoryButton.onClick.AddListener(LoadOlderHistoryPage);
        if (historyScrollRect != null)
            historyScrollRect.onValueChanged.AddListener(OnHistoryScrollValueChanged);

        chatSendButton.onClick.AddListener(TrySendChatFromInput);
        jobSelectButton.onClick.AddListener(OnClickJobSelectButton);

        chatInputField.lineType = TMP_InputField.LineType.SingleLine;
        chatInputField.characterLimit = chatCharacterLimit;
        chatInputField.onValueChanged.AddListener(_ => RefreshChatCountText());
        chatInputField.onSubmit.AddListener(_ => TrySendChatFromInput());

        nicknameInput.lineType = TMP_InputField.LineType.SingleLine;
        nicknameInput.characterLimit = nicknameCharacterLimit;
        nicknameInput.onValueChanged.AddListener(OnNicknameValueChanged);

        ResetToInitialState();

        RefreshChatCountText();
        RefreshNicknameCountText();

        GameStatusMessageRouter.Show(initialStatusText, false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (loadOlderHistoryButton != null)
            loadOlderHistoryButton.onClick.RemoveListener(LoadOlderHistoryPage);

        if (historyScrollRect != null)
            historyScrollRect.onValueChanged.RemoveListener(OnHistoryScrollValueChanged);

        if (backToNicknameButton != null)
            backToNicknameButton.onClick.RemoveListener(OnClickBackToNickname);
    }

    public void ResetToInitialState()
    {
        // 닉네임 입력부터 다시 시작하는 첫 화면 상태로 되돌린다.
        hostButtonRoot.SetActive(true);
        clientButtonRoot.SetActive(true);
        nicknameInputRoot.SetActive(true);

        ipInputRoot.SetActive(false);
        joinButtonRoot.SetActive(false);
        SetBackToNicknameButtonVisible(false);

        chatToggleButtonRoot.SetActive(false);
        chatPopupRoot.SetActive(false);
        lobbyThingsRoot.SetActive(false);
    }

    private void HideFirstStepUI()
    {
        hostButtonRoot.SetActive(false);
        clientButtonRoot.SetActive(false);
        nicknameInputRoot.SetActive(false);
    }

    private void OnNicknameValueChanged(string value)
    {
        string trimmed = value.Trim();

        // 앞뒤 공백 자동 제거
        if (value != trimmed)
        {
            nicknameInput.SetTextWithoutNotify(trimmed);
        }

        RefreshNicknameCountText();
    }

    private void RefreshNicknameCountText()
    {
        if (nicknameCountText == null || nicknameInput == null)
            return;

        int current = string.IsNullOrEmpty(nicknameInput.text) ? 0 : nicknameInput.text.Length;
        nicknameCountText.text = $"{current}/{nicknameCharacterLimit}";
    }
    private bool TryGetValidNickname(out string nickname)
    {
        nickname = nicknameInput.text.Trim();

        if (string.IsNullOrWhiteSpace(nickname))
        {
            GameStatusMessageRouter.Show("닉네임은 공백만 입력할 수 없습니다.");
            return false;
        }

        return true;
    }

    public void EnterRoomAsHost()
    {
        SetBackToNicknameButtonVisible(false);
        chatToggleButtonRoot.SetActive(true);
        lobbyThingsRoot.SetActive(true);
        chatPopupRoot.SetActive(false);
    }

    public void EnterRoomAsClient()
    {
        ipInputRoot.SetActive(false);
        joinButtonRoot.SetActive(false);
        SetBackToNicknameButtonVisible(false);

        chatToggleButtonRoot.SetActive(true);
        lobbyThingsRoot.SetActive(true);
        chatPopupRoot.SetActive(false);
    }

    public void SetStatus(string message, bool autoClear = true)
    {
        if (statusText == null)
            return;

        // GameStatusMessageRouter의 기본 수신자로 사용되며, autoClear가 꺼지면 안내문을 유지한다.
        statusText.text = message ?? string.Empty;

        if (statusClearCoroutine != null)
        {
            StopCoroutine(statusClearCoroutine);
            statusClearCoroutine = null;
        }

        if (autoClear && !string.IsNullOrEmpty(message))
            statusClearCoroutine = StartCoroutine(CoClearStatusAfterDelay());
    }

    private IEnumerator CoClearStatusAfterDelay()
    {
        yield return new WaitForSeconds(statusClearDelay);

        if (statusText != null)
            statusText.text = string.Empty;

        statusClearCoroutine = null;
    }

    private void OnClickHost()
    {
        if (!TryGetValidNickname(out string nickname))
            return;

        HideFirstStepUI();

        bool success = P2PChatBootstrap.Instance.StartHost(nickname);
        if (!success)
            ResetToInitialState();
    }

    private void OnClickClient()
    {
        if (!TryGetValidNickname(out string nickname))
            return;

        // 클라이언트는 닉네임을 먼저 확정한 뒤 IP 입력 단계로 넘어간다.
        HideFirstStepUI();
        ipInputRoot.SetActive(true);
        joinButtonRoot.SetActive(true);
        SetBackToNicknameButtonVisible(true);

        P2PChatBootstrap.Instance.PrepareClient(nickname);
        GameStatusMessageRouter.Show("접속할 IPv4를 입력한 뒤 입장 버튼을 누르세요.");
    }

    private void OnClickJoin()
    {
        string ip = ipInput.text.Trim();

        if (string.IsNullOrWhiteSpace(ip))
        {
            ip = "127.0.0.1";
            ipInput.text = ip;
        }

        P2PChatBootstrap.Instance.StartClient(ip);
    }

    private void OnClickBackToNickname()
    {
        // 접속 시도 전 단계로 되돌아가 닉네임을 다시 수정할 수 있게 한다.
        if (P2PChatBootstrap.Instance != null)
            P2PChatBootstrap.Instance.CancelClientJoinPreparation();

        hostButtonRoot.SetActive(true);
        clientButtonRoot.SetActive(true);
        nicknameInputRoot.SetActive(true);

        ipInputRoot.SetActive(false);
        joinButtonRoot.SetActive(false);
        SetBackToNicknameButtonVisible(false);

        if (nicknameInput != null)
            nicknameInput.ActivateInputField();

        GameStatusMessageRouter.Show(initialStatusText, false);
    }

    private void EnsureBackToNicknameButton()
    {
        if (backToNicknameButton != null || !autoCreateBackToNicknameButton || joinButton == null)
            return;

        // 기존 Join Button의 스타일을 복제해 씬 수정 없이 되돌아가기 버튼을 제공한다.
        Button createdButton = Instantiate(joinButton, joinButton.transform.parent);
        createdButton.name = "Back To Nickname Button";
        createdButton.onClick.RemoveAllListeners();

        TMP_Text label = createdButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = backToNicknameButtonLabel;

        RectTransform createdRect = createdButton.transform as RectTransform;
        RectTransform joinRect = joinButton.transform as RectTransform;
        if (createdRect != null && joinRect != null)
        {
            float verticalOffset = joinRect.rect.height + 12f;
            if (verticalOffset <= 12f)
                verticalOffset = 60f;

            createdRect.anchoredPosition = joinRect.anchoredPosition + new Vector2(0f, -verticalOffset);
        }

        backToNicknameButton = createdButton;
        SetBackToNicknameButtonVisible(false);
    }

    private void SetBackToNicknameButtonVisible(bool visible)
    {
        if (backToNicknameButton != null)
            backToNicknameButton.gameObject.SetActive(visible);
    }

    private void ToggleChatPopup()
    {
        chatPopupRoot.SetActive(!chatPopupRoot.activeSelf);

        if (chatPopupRoot.activeSelf)
            ScrollToBottomDeferred();
    }

    private void CloseChatPopup()
    {
        chatPopupRoot.SetActive(false);
    }

    private void TrySendChatFromInput()
    {
        if (P2PChatSession.Instance == null)
            return;

        string text = chatInputField.text;
        if (string.IsNullOrWhiteSpace(text))
            return;

        bool sent = P2PChatSession.Instance.TrySendLocalChat(text);
        if (!sent)
            return;

        chatInputField.text = string.Empty;
        RefreshChatCountText();
        chatInputField.ActivateInputField();
    }

    private void RefreshChatCountText()
    {
        if (chatCountText == null || chatInputField == null)
            return;

        int current = string.IsNullOrEmpty(chatInputField.text) ? 0 : chatInputField.text.Length;
        chatCountText.text = $"{current}/{chatCharacterLimit}";
    }

    public void RefreshHistory(NetworkList<P2PChatEntry> entries)
    {
        boundHistory = entries;
        RebuildLatestHistoryPage();
    }

    public void ApplyHistoryChange(NetworkList<P2PChatEntry> entries, NetworkListEvent<P2PChatEntry> changeEvent)
    {
        if (entries == null)
        {
            // 세션이 사라진 경우 화면도 빈 히스토리 상태로 맞춘다.
            boundHistory = null;
            ClearRenderedHistory();
            RefreshHistoryPagingState();
            return;
        }

        if (boundHistory != entries)
        {
            RefreshHistory(entries);
            return;
        }

        switch (changeEvent.Type)
        {
            case NetworkListEvent<P2PChatEntry>.EventType.Add:
                // 일반적인 새 메시지는 블록 하나만 추가해 전체 재생성을 피한다.
                ApplyHistoryAdd(entries, changeEvent);
                break;

            case NetworkListEvent<P2PChatEntry>.EventType.Remove:
            case NetworkListEvent<P2PChatEntry>.EventType.RemoveAt:
                ApplyHistoryRemove(entries, changeEvent.Index);
                break;

            case NetworkListEvent<P2PChatEntry>.EventType.Value:
                ApplyHistoryValueChange(entries, changeEvent.Index);
                break;

            case NetworkListEvent<P2PChatEntry>.EventType.Clear:
                ClearRenderedHistory();
                displayedHistoryStartIndex = 0;
                displayedHistoryCount = 0;
                RefreshHistoryPagingState();
                break;

            case NetworkListEvent<P2PChatEntry>.EventType.Insert:
            case NetworkListEvent<P2PChatEntry>.EventType.Full:
            default:
                RebuildLatestHistoryPage();
                break;
        }
    }

    public void LoadOlderHistoryPage()
    {
        if (boundHistory == null || historyContent == null)
            return;

        // 최신 30개에서 시작해 위로 스크롤할 때 이전 30개를 앞쪽에 붙인다.
        int newStartIndex = Mathf.Max(0, displayedHistoryStartIndex - HistoryPageSize);
        int prependCount = displayedHistoryStartIndex - newStartIndex;
        if (prependCount <= 0)
            return;

        for (int i = displayedHistoryStartIndex - 1; i >= newStartIndex; i--)
        {
            GameObject block = CreateHistoryBlock(boundHistory[i], historyContent);
            if (block == null)
                continue;

            block.transform.SetAsFirstSibling();
            renderedHistoryBlocks.Insert(0, block);
        }

        displayedHistoryStartIndex = newStartIndex;
        displayedHistoryCount = renderedHistoryBlocks.Count;
        RefreshHistoryPagingState();
        ScrollToTopDeferred();
    }

    private void RebuildLatestHistoryPage()
    {
        // 최초 입장/전체 갱신 시에는 최신 페이지 하나만 렌더링한다.
        ClearRenderedHistory();

        if (boundHistory == null || historyContent == null)
        {
            displayedHistoryStartIndex = 0;
            displayedHistoryCount = 0;
            RefreshHistoryPagingState();
            return;
        }

        displayedHistoryStartIndex = Mathf.Max(0, boundHistory.Count - HistoryPageSize);

        for (int i = displayedHistoryStartIndex; i < boundHistory.Count; i++)
        {
            GameObject block = CreateHistoryBlock(boundHistory[i], historyContent);
            if (block != null)
                renderedHistoryBlocks.Add(block);
        }

        displayedHistoryCount = renderedHistoryBlocks.Count;
        RefreshHistoryPagingState();
        ScrollToBottomDeferred();
    }

    private void ApplyHistoryAdd(NetworkList<P2PChatEntry> entries, NetworkListEvent<P2PChatEntry> changeEvent)
    {
        if (historyContent == null)
            return;

        // 사용자가 최신 페이지를 보고 있을 때만 30개 창을 유지하며 맨 앞 항목을 밀어낸다.
        int previousCount = Mathf.Max(0, entries.Count - 1);
        bool wasSingleLatestPage =
            displayedHistoryCount <= HistoryPageSize &&
            displayedHistoryStartIndex == Mathf.Max(0, previousCount - HistoryPageSize);
        bool shouldScrollToBottom = ShouldAutoScrollToBottom();

        if (displayedHistoryCount == 0)
        {
            RebuildLatestHistoryPage();
            return;
        }

        int visibleEndIndex = displayedHistoryStartIndex + displayedHistoryCount;
        if (changeEvent.Index != visibleEndIndex)
        {
            RebuildLatestHistoryPage();
            return;
        }

        GameObject block = CreateHistoryBlock(changeEvent.Value, historyContent);
        if (block != null)
        {
            renderedHistoryBlocks.Add(block);
            displayedHistoryCount = renderedHistoryBlocks.Count;
        }

        if (wasSingleLatestPage && displayedHistoryCount > HistoryPageSize)
            RemoveFirstRenderedHistoryBlock();

        RefreshHistoryPagingState();

        if (shouldScrollToBottom)
            ScrollToBottomDeferred();
    }

    private void ApplyHistoryRemove(NetworkList<P2PChatEntry> entries, int removedIndex)
    {
        if (entries.Count == 0)
        {
            // 서버 히스토리가 비면 표시 중인 블록도 모두 지운다.
            ClearRenderedHistory();
            displayedHistoryStartIndex = 0;
            displayedHistoryCount = 0;
            RefreshHistoryPagingState();
            return;
        }

        if (removedIndex < displayedHistoryStartIndex)
        {
            displayedHistoryStartIndex = Mathf.Max(0, displayedHistoryStartIndex - 1);
        }
        else if (removedIndex < displayedHistoryStartIndex + displayedHistoryCount)
        {
            int localIndex = removedIndex - displayedHistoryStartIndex;
            RemoveRenderedHistoryBlockAt(localIndex);
        }

        displayedHistoryCount = renderedHistoryBlocks.Count;
        RefreshHistoryPagingState();
    }

    private void ApplyHistoryValueChange(NetworkList<P2PChatEntry> entries, int changedIndex)
    {
        if (historyContent == null)
            return;

        if (changedIndex < displayedHistoryStartIndex ||
            changedIndex >= displayedHistoryStartIndex + displayedHistoryCount)
        {
            return;
        }

        int localIndex = changedIndex - displayedHistoryStartIndex;
        if (localIndex < 0 || localIndex >= renderedHistoryBlocks.Count)
            return;

        if (renderedHistoryBlocks[localIndex] != null)
            Destroy(renderedHistoryBlocks[localIndex]);

        GameObject block = CreateHistoryBlock(entries[changedIndex], historyContent);
        if (block == null)
        {
            renderedHistoryBlocks.RemoveAt(localIndex);
            displayedHistoryCount = renderedHistoryBlocks.Count;
            RefreshHistoryPagingState();
            return;
        }

        block.transform.SetSiblingIndex(localIndex);
        renderedHistoryBlocks[localIndex] = block;
    }

    private GameObject CreateHistoryBlock(P2PChatEntry entry, Transform parent)
    {
        // 데이터 타입에 맞는 프리팹을 생성해 채팅 히스토리 UI 블록으로 변환한다.
        switch (entry.EntryType)
        {
            case P2PChatEntryType.Normal:
                {
                    if (normalBlockPrefab == null)
                        return null;

                    ChatNormalBlockView block = Instantiate(normalBlockPrefab, parent);
                    block.Bind(entry.Sender.ToString(), entry.Message.ToString());
                    return block.gameObject;
                }

            case P2PChatEntryType.Narration:
                {
                    if (narrationBlockPrefab == null)
                        return null;

                    ChatNarrationBlockView block = Instantiate(narrationBlockPrefab, parent);
                    block.Bind(entry.Message.ToString());
                    return block.gameObject;
                }

            case P2PChatEntryType.Divider:
                {
                    if (dividerBlockPrefab == null)
                        return null;

                    return Instantiate(dividerBlockPrefab, parent);
                }

            default:
                return null;
        }
    }

    private void ClearRenderedHistory()
    {
        renderedHistoryBlocks.Clear();

        if (historyContent == null)
            return;

        for (int i = historyContent.childCount - 1; i >= 0; i--)
            Destroy(historyContent.GetChild(i).gameObject);
    }

    private void RemoveFirstRenderedHistoryBlock()
    {
        RemoveRenderedHistoryBlockAt(0);
        displayedHistoryStartIndex++;
        displayedHistoryCount = renderedHistoryBlocks.Count;
    }

    private void RemoveRenderedHistoryBlockAt(int localIndex)
    {
        if (localIndex < 0 || localIndex >= renderedHistoryBlocks.Count)
            return;

        if (renderedHistoryBlocks[localIndex] != null)
            Destroy(renderedHistoryBlocks[localIndex]);

        renderedHistoryBlocks.RemoveAt(localIndex);
    }

    private void RefreshHistoryPagingState()
    {
        bool canLoadOlder = boundHistory != null && displayedHistoryStartIndex > 0;

        if (loadOlderHistoryButton != null)
            loadOlderHistoryButton.interactable = canLoadOlder;

        if (historyRangeText == null)
            return;

        int totalCount = boundHistory != null ? boundHistory.Count : 0;
        if (totalCount <= 0 || displayedHistoryCount <= 0)
        {
            historyRangeText.text = "0/0";
            return;
        }

        int firstVisibleNumber = displayedHistoryStartIndex + 1;
        int lastVisibleNumber = Mathf.Min(totalCount, displayedHistoryStartIndex + displayedHistoryCount);
        historyRangeText.text = $"{firstVisibleNumber}-{lastVisibleNumber}/{totalCount}";
    }

    private bool ShouldAutoScrollToBottom()
    {
        if (historyScrollRect == null || chatPopupRoot == null || !chatPopupRoot.activeSelf)
            return true;

        // 사용자가 과거 메시지를 보고 있으면 새 메시지가 와도 스크롤 위치를 덜 흔든다.
        return historyScrollRect.verticalNormalizedPosition <= 0.05f;
    }

    private void OnHistoryScrollValueChanged(Vector2 normalizedPosition)
    {
        if (suppressHistoryScrollEvent || !loadOlderOnScrollTop)
            return;

        // ScrollRect 최상단 근처에 도달하면 이전 페이지를 자동 로드한다.
        if (boundHistory == null || displayedHistoryStartIndex <= 0)
            return;

        if (normalizedPosition.y >= loadOlderScrollThreshold)
            LoadOlderHistoryPage();
    }

    private void ScrollToBottomDeferred()
    {
        if (scrollCoroutine != null)
            StopCoroutine(scrollCoroutine);

        scrollCoroutine = StartCoroutine(CoScrollToNextFrame(0f));
    }

    private void ScrollToTopDeferred()
    {
        if (scrollCoroutine != null)
            StopCoroutine(scrollCoroutine);

        scrollCoroutine = StartCoroutine(CoScrollToNextFrame(1f));
    }

    private IEnumerator CoScrollToNextFrame(float verticalNormalizedPosition)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(historyContent);

        if (historyScrollRect != null)
        {
            suppressHistoryScrollEvent = true;
            historyScrollRect.verticalNormalizedPosition = verticalNormalizedPosition;
            suppressHistoryScrollEvent = false;
        }

        scrollCoroutine = null;
    }

    private void OnClickJobSelectButton()
    {
        if (jobSelectionPopupUI != null)
            jobSelectionPopupUI.TogglePopup();
    }
}
