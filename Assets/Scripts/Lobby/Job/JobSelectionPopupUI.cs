using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class JobSelectionPopupUI : MonoBehaviour
{
    [Header("팝업")]
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button selectButton;

    [Header("좌측 리스트")]
    [SerializeField] private RectTransform jobButtonContent;
    [SerializeField] private JobSelectListItemView jobButtonPrefab;

    [Header("우측 정보")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text shortDescriptionText;
    [SerializeField] private TMP_Text detailDescriptionText;

    [Header("직업 데이터")]
    [SerializeField] private JobDefinition[] jobDefinitions;

    private JobDefinition currentPreview;
    private NetworkPlayerState localPlayerState;

    private void Start()
    {
        closeButton.onClick.AddListener(ClosePopup);
        selectButton.onClick.AddListener(OnClickSelect);

        BuildList();
        popupRoot.SetActive(false);
    }

    public void TogglePopup()
    {
        if (popupRoot.activeSelf)
            ClosePopup();
        else
            OpenPopup();
    }

    public void OpenPopup()
    {
        popupRoot.SetActive(true);
        ResolveLocalPlayerState();
        BuildList();

        if (currentPreview == null && jobDefinitions.Length > 0)
            SetPreview(jobDefinitions[0]);
        else
            RefreshSelectButtonState();
    }

    public void ClosePopup()
    {
        popupRoot.SetActive(false);
    }

    private void ResolveLocalPlayerState()
    {
        NetworkPlayerState[] players = FindObjectsByType<NetworkPlayerState>(FindObjectsSortMode.None);

        foreach (NetworkPlayerState player in players)
        {
            if (player != null && player.IsOwner)
            {
                localPlayerState = player;
                return;
            }
        }

        localPlayerState = null;
    }

    private void BuildList()
    {
        for (int i = jobButtonContent.childCount - 1; i >= 0; i--)
            Destroy(jobButtonContent.GetChild(i).gameObject);

        foreach (JobDefinition def in jobDefinitions)
        {
            JobSelectListItemView item = Instantiate(jobButtonPrefab, jobButtonContent);

            bool isCurrent = localPlayerState != null && localPlayerState.Job.Value == def.jobType;
            bool isTakenByOther = false;

            if (JobSelectionManager.Instance != null)
            {
                ulong ignoreId = localPlayerState != null ? localPlayerState.OwnerClientId : ulong.MaxValue;
                isTakenByOther = JobSelectionManager.Instance.IsJobTaken(def.jobType, ignoreId);
            }

            item.Bind(
                def.displayName,
                isCurrent,
                isTakenByOther,
                () => SetPreview(def)
            );
        }
    }

    private void SetPreview(JobDefinition def)
    {
        currentPreview = def;

        if (portraitImage != null)
            portraitImage.sprite = def.portrait;

        if (nameText != null)
            nameText.text = def.displayName;

        if (shortDescriptionText != null)
            shortDescriptionText.text = def.shortDescription;

        if (detailDescriptionText != null)
            detailDescriptionText.text = def.detailDescription;

        RefreshSelectButtonState();
    }

    private void RefreshSelectButtonState()
    {
        if (selectButton == null || currentPreview == null)
            return;

        if (localPlayerState == null)
        {
            selectButton.gameObject.SetActive(false);
            return;
        }

        bool isCurrent = localPlayerState.Job.Value == currentPreview.jobType;

        ulong ignoreId = localPlayerState.OwnerClientId;
        bool isTakenByOther = JobSelectionManager.Instance != null &&
                              JobSelectionManager.Instance.IsJobTaken(currentPreview.jobType, ignoreId);

        if (isCurrent)
        {
            selectButton.gameObject.SetActive(false);
            return;
        }

        selectButton.gameObject.SetActive(true);
        selectButton.interactable = !isTakenByOther;
    }

    private void OnClickSelect()
    {
        if (localPlayerState == null || currentPreview == null)
            return;

        localPlayerState.RequestChangeJobRpc(currentPreview.jobType);

        BuildList();
        RefreshSelectButtonState();
    }
}