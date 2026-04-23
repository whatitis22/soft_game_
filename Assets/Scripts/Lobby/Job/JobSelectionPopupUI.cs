using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
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
    private readonly Dictionary<NetworkPlayerState, NetworkVariable<JobType>.OnValueChangedDelegate> jobChangeHandlers
        = new Dictionary<NetworkPlayerState, NetworkVariable<JobType>.OnValueChangedDelegate>();

    private bool IsPopupOpen => popupRoot != null && popupRoot.activeSelf;

    private void OnEnable()
    {
        NetworkPlayerRegistry.PlayersChanged += OnRegisteredPlayersChanged;
    }

    private void OnDisable()
    {
        NetworkPlayerRegistry.PlayersChanged -= OnRegisteredPlayersChanged;
        UnsubscribeAllJobChanges();
    }

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
        RefreshJobChangeSubscriptions();
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

    private void OnRegisteredPlayersChanged()
    {
        RefreshJobChangeSubscriptions();

        if (IsPopupOpen)
            RefreshFromNetworkState();
    }

    private void RefreshFromNetworkState()
    {
        ResolveLocalPlayerState();
        BuildList();
        RefreshSelectButtonState();
    }

    private void RefreshJobChangeSubscriptions()
    {
        List<NetworkPlayerState> players = NetworkPlayerRegistry.GetSpawnedPlayers();
        List<NetworkPlayerState> subscribedPlayers = new List<NetworkPlayerState>(jobChangeHandlers.Keys);

        for (int i = 0; i < subscribedPlayers.Count; i++)
        {
            NetworkPlayerState player = subscribedPlayers[i];
            if (player == null || !players.Contains(player))
                UnsubscribeJobChange(player);
        }

        for (int i = 0; i < players.Count; i++)
        {
            NetworkPlayerState player = players[i];
            if (player == null || jobChangeHandlers.ContainsKey(player))
                continue;

            NetworkVariable<JobType>.OnValueChangedDelegate handler = (previousValue, newValue) =>
            {
                if (IsPopupOpen)
                    RefreshFromNetworkState();
            };

            player.Job.OnValueChanged += handler;
            jobChangeHandlers[player] = handler;
        }
    }

    private void UnsubscribeJobChange(NetworkPlayerState player)
    {
        if (player == null)
        {
            if (!ReferenceEquals(player, null))
                jobChangeHandlers.Remove(player);

            return;
        }

        if (!jobChangeHandlers.TryGetValue(player, out NetworkVariable<JobType>.OnValueChangedDelegate handler))
            return;

        player.Job.OnValueChanged -= handler;
        jobChangeHandlers.Remove(player);
    }

    private void UnsubscribeAllJobChanges()
    {
        foreach (KeyValuePair<NetworkPlayerState, NetworkVariable<JobType>.OnValueChangedDelegate> pair in jobChangeHandlers)
        {
            if (pair.Key != null)
                pair.Key.Job.OnValueChanged -= pair.Value;
        }

        jobChangeHandlers.Clear();
    }

    private void ResolveLocalPlayerState()
    {
        if (NetworkPlayerRegistry.TryGetLocalPlayer(out NetworkPlayerState player))
            localPlayerState = player;
        else
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
    }
}
