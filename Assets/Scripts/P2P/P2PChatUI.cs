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

    [Header("채팅 입력")]
    [SerializeField] private GameObject RobbyThingsRoot;
    [SerializeField] private TMP_InputField chatInputField;
    [SerializeField] private Button chatSendButton;
    [SerializeField] private TMP_Text chatCountText;
    [SerializeField] private int chatCharacterLimit = 100;

    private Coroutine statusClearCoroutine;
    private Coroutine scrollCoroutine;

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

        chatSendButton.onClick.AddListener(TrySendChatFromInput);

        // 채팅 입력창
        chatInputField.lineType = TMP_InputField.LineType.SingleLine;
        chatInputField.characterLimit = chatCharacterLimit;
        chatInputField.onValueChanged.AddListener(_ => RefreshChatCountText());
        chatInputField.onSubmit.AddListener(_ => TrySendChatFromInput());

        // 닉네임 입력창
        nicknameInput.lineType = TMP_InputField.LineType.SingleLine;
        nicknameInput.characterLimit = nicknameCharacterLimit;
        nicknameInput.onValueChanged.AddListener(OnNicknameValueChanged);

        ResetToInitialState();

        RefreshChatCountText();
        RefreshNicknameCountText();

        SetStatus(initialStatusText, false);
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
        RobbyThingsRoot.SetActive(false);
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
        RobbyThingsRoot.SetActive(true);
        chatPopupRoot.SetActive(false);
    }

    public void EnterRoomAsClient()
    {
        ipInputRoot.SetActive(false);
        joinButtonRoot.SetActive(false);

        chatToggleButtonRoot.SetActive(true);
        RobbyThingsRoot.SetActive(true);
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
        if (historyContent == null)
            return;

        for (int i = historyContent.childCount - 1; i >= 0; i--)
            Destroy(historyContent.GetChild(i).gameObject);

        foreach (P2PChatEntry entry in entries)
        {
            switch (entry.EntryType)
            {
                case P2PChatEntryType.Normal:
                    {
                        ChatNormalBlockView block = Instantiate(normalBlockPrefab, historyContent);
                        block.Bind(entry.Sender.ToString(), entry.Message.ToString());
                        break;
                    }

                case P2PChatEntryType.Narration:
                    {
                        ChatNarrationBlockView block = Instantiate(narrationBlockPrefab, historyContent);
                        block.Bind(entry.Message.ToString());
                        break;
                    }

                case P2PChatEntryType.Divider:
                    {
                        Instantiate(dividerBlockPrefab, historyContent);
                        break;
                    }
            }
        }

        ScrollToBottomDeferred();
    }

    private void ScrollToBottomDeferred()
    {
        if (scrollCoroutine != null)
            StopCoroutine(scrollCoroutine);

        scrollCoroutine = StartCoroutine(CoScrollToBottomNextFrame());
    }

    private IEnumerator CoScrollToBottomNextFrame()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(historyContent);

        if (historyScrollRect != null)
            historyScrollRect.verticalNormalizedPosition = 0f;

        scrollCoroutine = null;
    }
}