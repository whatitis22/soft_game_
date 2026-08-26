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

        // 씬 UI가 살아 있으면 전역 오버레이 대신 해당 UI가 메시지를 받는다.
        Receivers += receiver;
        receiverCount++;
    }

    public static void UnregisterReceiver(Action<string, bool> receiver)
    {
        if (receiver == null)
            return;

        // 수신자가 모두 사라지면 이후 메시지는 fallback overlay로 표시된다.
        Receivers -= receiver;
        receiverCount = Mathf.Max(0, receiverCount - 1);
    }

    public static void Show(string message, bool autoClear = true)
    {
        // 현재 씬에 상태 UI가 있으면 그쪽으로 보내고, 없으면 런타임 오버레이를 사용한다.
        if (receiverCount > 0)
        {
            Receivers?.Invoke(message ?? string.Empty, autoClear);
            return;
        }

        EnsureFallbackOverlay().Show(message ?? string.Empty, autoClear);
    }

    public static void RefreshFallbackFont()
    {
        if (fallbackOverlay != null)
            fallbackOverlay.ApplyConfiguredFont();
    }

    private static FallbackOverlay EnsureFallbackOverlay()
    {
        if (fallbackOverlay != null)
            return fallbackOverlay;

        // 전투 씬처럼 P2PChatUI가 없는 경우에도 실패 사유를 볼 수 있게 만든다.
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

            // 빈 메시지는 화면을 숨기는 신호로 사용한다.
            ApplyConfiguredFont();
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
            // Time.timeScale이 멈춰도 안내 메시지는 사라져야 하므로 realtime을 쓴다.
            yield return new WaitForSecondsRealtime(DefaultAutoClearDelay);
            Show(string.Empty, false);
        }

        private void BuildOverlay()
        {
            if (messageText != null)
                return;

            // 별도 프리팹 없이도 모든 씬에서 최소한의 상태 표시가 가능하도록 UI를 런타임 생성한다.
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
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
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
            ApplyConfiguredFont();
        }

        public void ApplyConfiguredFont()
        {
            if (messageText == null)
                return;

            TMP_FontAsset configuredFont = GameStatusMessageSettings.Instance != null
                ? GameStatusMessageSettings.Instance.StatusFont
                : null;

            if (configuredFont != null)
                messageText.font = configuredFont;
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
        }
    }
}
