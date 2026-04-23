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
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private TMP_InputField ipInput;

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

    private void Start()
    {
        hostButton.onClick.AddListener(OnClickHost);
        clientButton.onClick.AddListener(OnClickClient);
        joinButton.onClick.AddListener(OnClickJoin);

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

        SetStatus(initialStatusText, false);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (loadOlderHistoryButton != null)
            loadOlderHistoryButton.onClick.RemoveListener(LoadOlderHistoryPage);

        if (historyScrollRect != null)
            historyScrollRect.onValueChanged.RemoveListener(OnHistoryScrollValueChanged);
    }

    public void ResetToInitialState()
    {
        hostButtonRoot.SetActive(true);
        clientButtonRoot.SetActive(true);
        nicknameInputRoot.SetActive(true);

        ipInputRoot.SetActive(false);
        joinButtonRoot.SetActive(false);

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
            SetStatus("닉네임은 공백만 입력할 수 없습니다.");
            return false;
        }

        return true;
    }

    public void EnterRoomAsHost()
    {
        chatToggleButtonRoot.SetActive(true);
        lobbyThingsRoot.SetActive(true);
        chatPopupRoot.SetActive(false);
    }

    public void EnterRoomAsClient()
    {
        ipInputRoot.SetActive(false);
        joinButtonRoot.SetActive(false);

        chatToggleButtonRoot.SetActive(true);
        lobbyThingsRoot.SetActive(true);
        chatPopupRoot.SetActive(false);
    }

    public void SetStatus(string message, bool autoClear = true)
    {
        if (statusText == null)
            return;

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

        HideFirstStepUI();
        ipInputRoot.SetActive(true);
        joinButtonRoot.SetActive(true);

        P2PChatBootstrap.Instance.PrepareClient(nickname);
        SetStatus("접속할 IPv4를 입력한 뒤 입장 버튼을 누르세요.");
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

        return historyScrollRect.verticalNormalizedPosition <= 0.05f;
    }

    private void OnHistoryScrollValueChanged(Vector2 normalizedPosition)
    {
        if (suppressHistoryScrollEvent || !loadOlderOnScrollTop)
            return;

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
