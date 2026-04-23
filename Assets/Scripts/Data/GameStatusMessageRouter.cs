using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class GameStatusMessageRouter
{
    private const float DefaultAutoClearDelay = 3f;

    private static event Action<string, bool> Receivers;
    private static int receiverCount;
    private static FallbackOverlay fallbackOverlay;

    public static void RegisterReceiver(Action<string, bool> receiver)
    {
        if (receiver == null)
            return;

        Receivers += receiver;
        receiverCount++;
    }

    public static void UnregisterReceiver(Action<string, bool> receiver)
    {
        if (receiver == null)
            return;

        Receivers -= receiver;
        receiverCount = Mathf.Max(0, receiverCount - 1);
    }

    public static void Show(string message, bool autoClear = true)
    {
        if (receiverCount > 0)
        {
            Receivers?.Invoke(message ?? string.Empty, autoClear);
            return;
        }

        EnsureFallbackOverlay().Show(message ?? string.Empty, autoClear);
    }

    private static FallbackOverlay EnsureFallbackOverlay()
    {
        if (fallbackOverlay != null)
            return fallbackOverlay;

        GameObject root = new GameObject("Game Status Message Router");
        UnityEngine.Object.DontDestroyOnLoad(root);

        fallbackOverlay = root.AddComponent<FallbackOverlay>();
        return fallbackOverlay;
    }

    private sealed class FallbackOverlay : MonoBehaviour
    {
        private CanvasGroup canvasGroup;
        private TMP_Text messageText;
        private Coroutine clearRoutine;

        private void Awake()
        {
            BuildOverlay();
            SetVisible(false);
        }

        public void Show(string message, bool autoClear)
        {
            if (messageText == null)
                BuildOverlay();

            messageText.text = message ?? string.Empty;
            SetVisible(!string.IsNullOrEmpty(message));

            if (clearRoutine != null)
            {
                StopCoroutine(clearRoutine);
                clearRoutine = null;
            }

            if (autoClear && !string.IsNullOrEmpty(message))
                clearRoutine = StartCoroutine(CoClearAfterDelay());
        }

        private IEnumerator CoClearAfterDelay()
        {
            yield return new WaitForSecondsRealtime(DefaultAutoClearDelay);
            Show(string.Empty, false);
        }

        private void BuildOverlay()
        {
            if (messageText != null)
                return;

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            GameObject panelObject = new GameObject("Status Panel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(transform, false);

            RectTransform panelRect = (RectTransform)panelObject.transform;
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -48f);
            panelRect.sizeDelta = new Vector2(760f, 72f);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.72f);

            GameObject textObject = new GameObject("Status Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelObject.transform, false);

            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24f, 10f);
            textRect.offsetMax = new Vector2(-24f, -10f);

            messageText = textObject.GetComponent<TextMeshProUGUI>();
            messageText.alignment = TextAlignmentOptions.Center;
            messageText.color = Color.white;
            messageText.fontSize = 26f;
            messageText.textWrappingMode = TextWrappingModes.Normal;
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
        }
    }
}
