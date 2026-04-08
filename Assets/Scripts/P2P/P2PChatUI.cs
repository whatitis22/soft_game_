using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class P2PChatUI : MonoBehaviour
{
    public static P2PChatUI Instance { get; private set; }

    [Header("SetActive 제어용 루트")]
    [SerializeField] private GameObject hostButtonRoot;
    [SerializeField] private GameObject clientButtonRoot;
    [SerializeField] private GameObject nicknameInputRoot;
    [SerializeField] private GameObject ipInputRoot;
    [SerializeField] private GameObject joinButtonRoot;

    [Header("실제 UI 참조")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private TMP_InputField ipInput;
    [SerializeField] private TMP_Text statusText;

    private void Awake()
    {
        Instance = this;

        hostButton.onClick.AddListener(OnClickHost);
        clientButton.onClick.AddListener(OnClickClient);
        joinButton.onClick.AddListener(OnClickJoin);
    }

    private void Start()
    {
        ResetToInitialState();
        SetStatus("닉네임을 입력하세요.");
    }

    public void ResetToInitialState()
    {
        hostButtonRoot.SetActive(true);
        clientButtonRoot.SetActive(true);
        nicknameInputRoot.SetActive(true);

        ipInputRoot.SetActive(false);
        joinButtonRoot.SetActive(false);
    }

    private void HideFirstStepUI()
    {
        hostButtonRoot.SetActive(false);
        clientButtonRoot.SetActive(false);
        nicknameInputRoot.SetActive(false);
    }

    private string GetNickname()
    {
        return nicknameInput.text.Trim();
    }

    private void OnClickHost()
    {
        string nickname = GetNickname();

        if (string.IsNullOrWhiteSpace(nickname))
        {
            SetStatus("닉네임을 입력해야 합니다.");
            return;
        }

        HideFirstStepUI();
        ipInputRoot.SetActive(false);
        joinButtonRoot.SetActive(false);

        P2PChatBootstrap.Instance.StartHost(nickname);
    }

    private void OnClickClient()
    {
        string nickname = GetNickname();

        if (string.IsNullOrWhiteSpace(nickname))
        {
            SetStatus("닉네임을 입력해야 합니다.");
            return;
        }

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

    public void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}