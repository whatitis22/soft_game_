using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BattleCrystalChoiceUI : MonoBehaviour
{
    private static BattleCrystalChoiceUI instance;

    private RectTransform root;
    private TMP_Text titleText;
    private Button redButton;
    private Button greenButton;
    private Button blueButton;
    private Action<BattleCrystalColor> onSelected;

    public static void Show(
        string title,
        bool redEnabled,
        bool greenEnabled,
        bool blueEnabled,
        Action<BattleCrystalColor> onSelected)
    {
        BattleCrystalChoiceUI ui = GetOrCreate();
        ui.ShowInternal(title, redEnabled, greenEnabled, blueEnabled, onSelected);
    }

    public static void Hide()
    {
        if (instance != null && instance.root != null)
            instance.root.gameObject.SetActive(false);
    }

    private static BattleCrystalChoiceUI GetOrCreate()
    {
        if (instance != null)
            return instance;

        BattleCrystalChoiceUI existing = FindAnyObjectByType<BattleCrystalChoiceUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            instance = existing;
            return existing;
        }

        GameObject go = new GameObject("Battle Crystal Choice UI");
        instance = go.AddComponent<BattleCrystalChoiceUI>();
        instance.EnsureUi();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        EnsureUi();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void ShowInternal(
        string title,
        bool redEnabled,
        bool greenEnabled,
        bool blueEnabled,
        Action<BattleCrystalColor> selected)
    {
        EnsureUi();
        onSelected = selected;
        titleText.text = string.IsNullOrWhiteSpace(title) ? "재화 선택" : title;

        ConfigureButton(redButton, redEnabled);
        ConfigureButton(greenButton, greenEnabled);
        ConfigureButton(blueButton, blueEnabled);

        root.gameObject.SetActive(true);
        root.SetAsLastSibling();
    }

    private void EnsureUi()
    {
        if (root != null)
            return;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("Battle Choice Canvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        GameObject rootGo = new GameObject("Battle Crystal Choice Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        rootGo.transform.SetParent(canvas.transform, false);
        root = (RectTransform)rootGo.transform;
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = new Vector2(520f, 220f);

        Image panelImage = rootGo.GetComponent<Image>();
        panelImage.color = new Color(1f, 1f, 1f, 0.96f);
        panelImage.raycastTarget = true;

        Outline outline = rootGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
        outline.effectDistance = new Vector2(2f, -2f);

        titleText = CreateText("Title", root, "재화 선택", 30f);
        titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
        titleText.rectTransform.anchoredPosition = new Vector2(0f, -20f);
        titleText.rectTransform.sizeDelta = new Vector2(-40f, 56f);

        redButton = CreateButton("Red", "R", new Vector2(-150f, -42f), new Color(0.85f, 0.18f, 0.16f, 1f), BattleCrystalColor.Red);
        greenButton = CreateButton("Green", "G", new Vector2(0f, -42f), new Color(0.18f, 0.62f, 0.24f, 1f), BattleCrystalColor.Green);
        blueButton = CreateButton("Blue", "B", new Vector2(150f, -42f), new Color(0.18f, 0.35f, 0.86f, 1f), BattleCrystalColor.Blue);

        root.gameObject.SetActive(false);
    }

    private Button CreateButton(string name, string label, Vector2 position, Color color, BattleCrystalColor crystalColor)
    {
        GameObject go = new GameObject($"{name} Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(root, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(112f, 82f);

        Image image = go.GetComponent<Image>();
        image.color = color;

        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(() => Select(crystalColor));

        TMP_Text text = CreateText("Label", rect, label, 34f);
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;

        return button;
    }

    private TMP_Text CreateText(string name, Transform parent, string text, float fontSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TMP_Text tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.black;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    private void ConfigureButton(Button button, bool enabled)
    {
        if (button == null)
            return;

        button.interactable = enabled;
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            Color color = image.color;
            color.a = enabled ? 1f : 0.32f;
            image.color = color;
        }
    }

    private void Select(BattleCrystalColor color)
    {
        Action<BattleCrystalColor> selected = onSelected;
        onSelected = null;
        root.gameObject.SetActive(false);
        selected?.Invoke(color);
    }
}
