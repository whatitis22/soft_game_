using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class LobbySpeechBubbleManager : MonoBehaviour
{
    [Header("슬롯")]
    [SerializeField] private RectTransform[] bubbleSlots;

    [Header("프리팹")]
    [SerializeField] private LobbySpeechBubbleView speechBubblePrefab;

    [Header("참조")]
    [SerializeField] private LobbyStandingManager standingManager;

    [Header("표시 설정")]
    [SerializeField] private float bubbleLifetime = 2.5f;

    private readonly Dictionary<ulong, LobbySpeechBubbleView> activeBubbles = new();
    private readonly Dictionary<ulong, Coroutine> activeCoroutines = new();

    private NetworkSessionState boundSession;

    private void Update()
    {
        TryBindSession();
    }

    private void OnDisable()
    {
        UnbindSession();
        ClearAllBubbles();
    }

    private void TryBindSession()
    {
        if (boundSession != null)
            return;

        if (NetworkSessionState.Instance == null)
            return;

        boundSession = NetworkSessionState.Instance;
        boundSession.ChatHistory.OnListChanged += OnChatHistoryChanged;
    }

    private void UnbindSession()
    {
        if (boundSession == null)
            return;

        boundSession.ChatHistory.OnListChanged -= OnChatHistoryChanged;
        boundSession = null;
    }

    private void OnChatHistoryChanged(NetworkListEvent<P2PChatEntry> changeEvent)
    {
        if (changeEvent.Type != NetworkListEvent<P2PChatEntry>.EventType.Add)
            return;

        P2PChatEntry entry = changeEvent.Value;

        if (entry.EntryType != P2PChatEntryType.Normal)
            return;

        ShowBubble(entry.SenderClientId, entry.Message.ToString());
    }

    private void ShowBubble(ulong senderClientId, string message)
    {
        if (standingManager == null || speechBubblePrefab == null)
            return;

        if (!standingManager.TryGetSlotIndexForPlayer(senderClientId, out int slotIndex))
            return;

        if (slotIndex < 0 || slotIndex >= bubbleSlots.Length)
            return;

        RectTransform slot = bubbleSlots[slotIndex];
        if (slot == null)
            return;

        RemoveBubble(senderClientId);

        LobbySpeechBubbleView bubble = Instantiate(speechBubblePrefab, slot);
        RectTransform bubbleRect = bubble.transform as RectTransform;

        if (bubbleRect != null)
        {
            bubbleRect.anchorMin = new Vector2(0.5f, 0f);
            bubbleRect.anchorMax = new Vector2(0.5f, 0f);
            bubbleRect.pivot = new Vector2(0.5f, 0f);
            bubbleRect.anchoredPosition = Vector2.zero;
            bubbleRect.localScale = Vector3.one;
        }

        bubble.Bind(message);

        activeBubbles[senderClientId] = bubble;
        activeCoroutines[senderClientId] = StartCoroutine(CoRemoveBubbleAfterDelay(senderClientId));
    }

    private IEnumerator CoRemoveBubbleAfterDelay(ulong senderClientId)
    {
        yield return new WaitForSecondsRealtime(bubbleLifetime);
        RemoveBubble(senderClientId);
    }

    private void RemoveBubble(ulong senderClientId)
    {
        if (activeCoroutines.TryGetValue(senderClientId, out Coroutine coroutine))
        {
            if (coroutine != null)
                StopCoroutine(coroutine);

            activeCoroutines.Remove(senderClientId);
        }

        if (activeBubbles.TryGetValue(senderClientId, out LobbySpeechBubbleView bubble))
        {
            if (bubble != null)
                Destroy(bubble.gameObject);

            activeBubbles.Remove(senderClientId);
        }
    }

    private void ClearAllBubbles()
    {
        foreach (KeyValuePair<ulong, Coroutine> pair in activeCoroutines)
        {
            if (pair.Value != null)
                StopCoroutine(pair.Value);
        }
        activeCoroutines.Clear();

        foreach (KeyValuePair<ulong, LobbySpeechBubbleView> pair in activeBubbles)
        {
            if (pair.Value != null)
                Destroy(pair.Value.gameObject);
        }
        activeBubbles.Clear();
    }
}