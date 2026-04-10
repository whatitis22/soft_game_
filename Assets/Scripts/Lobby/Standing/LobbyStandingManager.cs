using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbyStandingManager : MonoBehaviour
{
    [Header("슬롯")]
    [SerializeField] private RectTransform[] standingSlots; // 왼쪽 -> 오른쪽
    [SerializeField] private int centerSlotIndex = 2;

    [Header("프리팹")]
    [SerializeField] private LobbyStandingView standingPrefab;

    [Header("직업 데이터")]
    [SerializeField] private JobDefinition[] jobDefinitions;

    [Header("표시 설정")]
    [SerializeField] private float localPlayerScaleMultiplier = 1.3f;
    [SerializeField] private float refreshInterval = 0.25f;

    private readonly List<LobbyStandingView> activeViews = new();
    private float nextRefreshTime;
    private string lastSignature = string.Empty;

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + refreshInterval;

        string currentSignature = BuildStateSignature();
        if (currentSignature == lastSignature)
            return;

        lastSignature = currentSignature;
        RefreshStandings();
    }

    private string BuildStateSignature()
    {
        NetworkPlayerState[] players = FindObjectsByType<NetworkPlayerState>(FindObjectsSortMode.None);

        StringBuilder sb = new StringBuilder(256);

        ulong hostId = ulong.MaxValue;
        if (NetworkSessionState.Instance != null)
            hostId = NetworkSessionState.Instance.HostClientId.Value;

        sb.Append("host=").Append(hostId).Append(';');

        foreach (NetworkPlayerState player in players)
        {
            if (player == null || !player.IsSpawned)
                continue;

            sb.Append(player.OwnerClientId).Append('|');
            sb.Append(player.PlayerIndex.Value).Append('|');
            sb.Append(player.DisplayNickname.Value.ToString()).Append('|');
            sb.Append((int)player.Job.Value).Append(';');
        }

        return sb.ToString();
    }

    private void RefreshStandings()
    {
        ClearViews();

        NetworkPlayerState localPlayer = FindLocalPlayer();
        if (localPlayer == null)
            return;

        List<NetworkPlayerState> allPlayers = GetSortedPlayers();
        if (allPlayers.Count == 0)
            return;

        // 내 자신 제외 목록
        List<NetworkPlayerState> others = new List<NetworkPlayerState>();
        foreach (NetworkPlayerState player in allPlayers)
        {
            if (player != localPlayer)
                others.Add(player);
        }

        // 내 자신은 가운데
        CreateStanding(localPlayer, centerSlotIndex, true);

        // 나머지는 가운데 제외 슬롯에 왼쪽부터 순서대로
        List<int> otherSlotIndices = GetOtherSlotIndicesLeftToRight();
        for (int i = 0; i < others.Count && i < otherSlotIndices.Count; i++)
        {
            CreateStanding(others[i], otherSlotIndices[i], false);
        }
    }

    private List<int> GetOtherSlotIndicesLeftToRight()
    {
        List<int> result = new List<int>();

        for (int i = 0; i < standingSlots.Length; i++)
        {
            if (i == centerSlotIndex)
                continue;

            result.Add(i);
        }

        return result;
    }

    private NetworkPlayerState FindLocalPlayer()
    {
        NetworkPlayerState[] players = FindObjectsByType<NetworkPlayerState>(FindObjectsSortMode.None);

        foreach (NetworkPlayerState player in players)
        {
            if (player != null && player.IsSpawned && player.IsOwner)
                return player;
        }

        return null;
    }

    private List<NetworkPlayerState> GetSortedPlayers()
    {
        NetworkPlayerState[] players = FindObjectsByType<NetworkPlayerState>(FindObjectsSortMode.None);
        List<NetworkPlayerState> result = new List<NetworkPlayerState>();

        foreach (NetworkPlayerState player in players)
        {
            if (player != null && player.IsSpawned)
                result.Add(player);
        }

        result.Sort((a, b) => a.PlayerIndex.Value.CompareTo(b.PlayerIndex.Value));
        return result;
    }

    private void CreateStanding(NetworkPlayerState player, int slotIndex, bool isLocalPlayer)
    {
        if (slotIndex < 0 || slotIndex >= standingSlots.Length)
            return;

        RectTransform slot = standingSlots[slotIndex];
        if (slot == null || standingPrefab == null)
            return;

        LobbyStandingView view = Instantiate(standingPrefab, slot);
        RectTransform viewRect = view.transform as RectTransform;

        if (viewRect != null)
        {
            viewRect.anchorMin = Vector2.zero;
            viewRect.anchorMax = Vector2.one;
            viewRect.offsetMin = Vector2.zero;
            viewRect.offsetMax = Vector2.zero;
            viewRect.localScale = Vector3.one;
        }

        JobDefinition definition = GetJobDefinition(player.Job.Value);
        Sprite sprite = definition != null ? definition.lobbyStandingImage : null;

        string nickname = player.DisplayNickname.Value.ToString();

        bool isHost = NetworkSessionState.Instance != null &&
                      player.OwnerClientId == NetworkSessionState.Instance.HostClientId.Value;

        string displayedNickname = isHost ? $"*{nickname}" : nickname;

        view.Bind(sprite, displayedNickname, isLocalPlayer, localPlayerScaleMultiplier);
        activeViews.Add(view);
    }

    private JobDefinition GetJobDefinition(JobType job)
    {
        foreach (JobDefinition def in jobDefinitions)
        {
            if (def != null && def.jobType == job)
                return def;
        }

        return null;
    }

    private void ClearViews()
    {
        for (int i = 0; i < activeViews.Count; i++)
        {
            if (activeViews[i] != null)
                Destroy(activeViews[i].gameObject);
        }

        activeViews.Clear();
    }
}