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
        // 플레이어 입장/퇴장에 따라 직업 점유 상태도 바뀌므로 레지스트리 이벤트를 구독한다.
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
        // 팝업을 열 때 현재 스폰된 플레이어들의 Job 변경 이벤트를 다시 맞춘다.
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
        // 서버가 확정한 NetworkVariable 값을 기준으로 목록과 선택 버튼을 다시 그린다.
        ResolveLocalPlayerState();
        BuildList();
        RefreshSelectButtonState();
    }

    private void RefreshJobChangeSubscriptions()
    {
        // 직업 변경 이벤트는 플레이어별 NetworkVariable에 걸어 실제 변경 시점에만 UI를 갱신한다.
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
        // 현재 직업/점유 상태를 기준으로 좌측 선택 리스트를 새로 구성한다.
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

        // 현재 선택 중이거나 다른 플레이어가 점유한 직업은 선택 버튼을 숨기거나 비활성화한다.
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

        // 즉시 UI를 낙관적으로 바꾸지 않고, 서버 확정 후 OnValueChanged에서 갱신한다.
        localPlayerState.RequestChangeJobRpc(currentPreview.jobType);
    }
}
