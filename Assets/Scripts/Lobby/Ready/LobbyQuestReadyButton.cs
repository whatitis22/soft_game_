using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

[DisallowMultipleComponent]
public class LobbyQuestReadyButton : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text buttonLabelText;

    [Header("Scene")]
    [SerializeField] private string targetSceneName;

    [Header("문구")]
    [SerializeField] private string hostLabel = "퀘스트 선택";
    [SerializeField] private string clientReadyOffLabel = "퀘스트 준비";
    [SerializeField] private string clientReadyOnLabel = "취소";

    [Header("갱신 주기")]
    [SerializeField] private float refreshInterval = 0.2f;

    private float nextRefreshTime;
    private NetworkPlayerState cachedLocalPlayer;

    private void Update()
    {
        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + refreshInterval;
            RefreshLabel();
        }
    }

    public void OnClickButton()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return;

        NetworkPlayerState localPlayer = FindLocalPlayer();
        if (localPlayer == null)
            return;

        if (NetworkManager.Singleton.IsServer)
        {
            TryHostMoveScene();
        }
        else
        {
            bool nextReady = !localPlayer.IsReady.Value;
            localPlayer.RequestSetReadyRpc(nextReady);
        }

        RefreshLabel();
    }

    private void RefreshLabel()
    {
        if (buttonLabelText == null)
            return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            buttonLabelText.text = hostLabel;
            return;
        }

        if (NetworkManager.Singleton.IsServer)
        {
            buttonLabelText.text = hostLabel;
            return;
        }

        NetworkPlayerState localPlayer = FindLocalPlayer();
        if (localPlayer == null)
        {
            buttonLabelText.text = clientReadyOffLabel;
            return;
        }

        buttonLabelText.text = localPlayer.IsReady.Value
            ? clientReadyOnLabel
            : clientReadyOffLabel;
    }

    private void TryHostMoveScene()
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            GameStatusMessageRouter.Show("이동할 씬 이름이 비어 있습니다.");
            return;
        }

        if (!NetworkManager.Singleton.NetworkConfig.EnableSceneManagement)
        {
            GameStatusMessageRouter.Show("Network Scene Management가 비활성화되어 있습니다.");
            return;
        }

        int playerCount = CountSpawnedPlayers();
        if (playerCount <= 1)
        {
            StartSceneLoad();
            return;
        }

        if (!AreAllClientsReady())
        {
            GameStatusMessageRouter.Show("모든 인원이 준비되지 않았습니다.");
            return;
        }

        StartSceneLoad();
    }

    private void StartSceneLoad()
    {
        var status = NetworkManager.Singleton.SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);

        if (status != SceneEventProgressStatus.Started)
        {
            GameStatusMessageRouter.Show($"씬 이동에 실패했습니다. ({status})");
            return;
        }

        ResetAllPlayersReadyOnServer();
    }

    private bool AreAllClientsReady()
    {
        ulong hostClientId = ulong.MaxValue;
        if (NetworkSessionState.Instance != null)
            hostClientId = NetworkSessionState.Instance.HostClientId.Value;

        bool foundAnyClient = false;

        foreach (NetworkPlayerState player in NetworkPlayerRegistry.GetSpawnedPlayers())
        {
            if (player.OwnerClientId == hostClientId)
                continue;

            foundAnyClient = true;

            if (!player.IsReady.Value)
                return false;
        }

        return foundAnyClient;
    }

    private void ResetAllPlayersReadyOnServer()
    {
        if (!NetworkManager.Singleton.IsServer)
            return;

        foreach (NetworkPlayerState player in NetworkPlayerRegistry.GetSpawnedPlayers())
            player.ServerSetReady(false);
    }

    private int CountSpawnedPlayers()
    {
        return NetworkPlayerRegistry.CountSpawnedPlayers();
    }

    private NetworkPlayerState FindLocalPlayer()
    {
        if (cachedLocalPlayer != null && cachedLocalPlayer.IsSpawned && cachedLocalPlayer.IsOwner)
            return cachedLocalPlayer;

        if (NetworkPlayerRegistry.TryGetLocalPlayer(out NetworkPlayerState player))
        {
            cachedLocalPlayer = player;
            return player;
        }

        cachedLocalPlayer = null;
        return null;
    }
}
