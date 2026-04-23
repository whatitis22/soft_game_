using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LobbyReadySlotsManager : MonoBehaviour
{
    [SerializeField] private RectTransform[] readySlots;
    [SerializeField] private GameObject readyIconPrefab;
    [SerializeField] private LobbyStandingManager standingManager;

    private readonly Dictionary<int, GameObject> readyIconsBySlot = new();
    private float nextRefreshTime;

    [SerializeField] private float refreshInterval = 0.2f;

    private void Start()
    {
        CreateReadyIcons();
        RefreshReadyIcons();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;

        nextRefreshTime = Time.unscaledTime + refreshInterval;
        RefreshReadyIcons();
    }

    private void CreateReadyIcons()
    {
        ClearReadyIcons();

        if (readySlots == null || readyIconPrefab == null)
            return;

        for (int i = 0; i < readySlots.Length; i++)
        {
            RectTransform slot = readySlots[i];
            if (slot == null)
                continue;

            GameObject icon = Instantiate(readyIconPrefab, slot);
            RectTransform iconRect = icon.transform as RectTransform;

            if (iconRect != null)
            {
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = Vector2.zero;
                iconRect.localScale = Vector3.one;
            }

            icon.SetActive(false);
            readyIconsBySlot[i] = icon;
        }
    }

    private void RefreshReadyIcons()
    {
        if (standingManager == null)
            return;

        foreach (KeyValuePair<int, GameObject> pair in readyIconsBySlot)
        {
            if (pair.Value != null)
                pair.Value.SetActive(false);
        }

        ulong hostClientId = ulong.MaxValue;
        if (NetworkSessionState.Instance != null)
            hostClientId = NetworkSessionState.Instance.HostClientId.Value;

        foreach (NetworkPlayerState player in NetworkPlayerRegistry.GetSpawnedPlayers())
        {
            // 호스트는 준비 아이콘 표시 안 함
            if (player.OwnerClientId == hostClientId)
                continue;

            if (!player.IsReady.Value)
                continue;

            if (!standingManager.TryGetSlotIndexForPlayer(player.OwnerClientId, out int slotIndex))
                continue;

            if (readyIconsBySlot.TryGetValue(slotIndex, out GameObject icon) && icon != null)
                icon.SetActive(true);
        }
    }

    private void ClearReadyIcons()
    {
        foreach (KeyValuePair<int, GameObject> pair in readyIconsBySlot)
        {
            if (pair.Value != null)
                Destroy(pair.Value);
        }

        readyIconsBySlot.Clear();
    }
}
