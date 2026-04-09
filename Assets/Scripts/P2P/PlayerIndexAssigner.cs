using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerIndexAssigner : MonoBehaviour
{
    [SerializeField] private int maxPlayers = 5;
    [SerializeField] private bool reuseReleasedIndex = true;

    private NetworkManager networkManager;

    // clientId -> playerIndex
    private readonly Dictionary<ulong, int> assignedIndices = new();

    // 재사용 가능한 빈 인덱스
    private readonly SortedSet<int> releasedIndices = new();

    private void Awake()
    {
        networkManager = NetworkManager.Singleton;
    }

    private void OnEnable()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton == null)
            return;

        NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        StartCoroutine(CoAssignWhenPlayerObjectReady(clientId));
    }

    private IEnumerator CoAssignWhenPlayerObjectReady(ulong clientId)
    {
        float timeout = 3f;
        float elapsed = 0f;

        NetworkObject playerObject = null;

        while (elapsed < timeout)
        {
            playerObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObject != null)
                break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (playerObject == null)
        {
            Debug.LogError($"[PlayerIndexAssigner] clientId={clientId} 의 PlayerObject를 찾지 못했습니다.");
            yield break;
        }

        NetworkPlayerState playerState = playerObject.GetComponent<NetworkPlayerState>();
        if (playerState == null)
        {
            Debug.LogError($"[PlayerIndexAssigner] PlayerObject에 NetworkPlayerState가 없습니다. clientId={clientId}");
            yield break;
        }

        if (!assignedIndices.ContainsKey(clientId))
        {
            int newIndex = GetNextAvailableIndex();
            assignedIndices.Add(clientId, newIndex);

            JobType autoJob = JobType.None;
            if (JobSelectionManager.Instance != null)
                autoJob = JobSelectionManager.Instance.GetFirstAvailableJob();

            playerState.ServerInitializePlayer(newIndex, autoJob);

            Debug.Log($"[PlayerIndexAssigner] clientId={clientId} -> PlayerIndex={newIndex}, Job={autoJob}");
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        if (assignedIndices.TryGetValue(clientId, out int removedIndex))
        {
            assignedIndices.Remove(clientId);

            if (reuseReleasedIndex)
                releasedIndices.Add(removedIndex);

            Debug.Log($"[PlayerIndexAssigner] clientId={clientId} 연결 해제 / PlayerIndex={removedIndex} 반환");
        }
    }

    private int GetNextAvailableIndex()
    {
        if (reuseReleasedIndex && releasedIndices.Count > 0)
        {
            int reused = GetSmallestReleasedIndex();
            releasedIndices.Remove(reused);
            return reused;
        }

        for (int i = 0; i < maxPlayers; i++)
        {
            if (!assignedIndices.ContainsValue(i))
                return i;
        }

        Debug.LogWarning("[PlayerIndexAssigner] 가용 PlayerIndex가 없습니다. maxPlayers 범위를 확인하세요.");
        return -1;
    }

    private int GetSmallestReleasedIndex()
    {
        foreach (int index in releasedIndices)
            return index;

        return -1;
    }

    public bool TryGetAssignedIndex(ulong clientId, out int playerIndex)
    {
        return assignedIndices.TryGetValue(clientId, out playerIndex);
    }
}