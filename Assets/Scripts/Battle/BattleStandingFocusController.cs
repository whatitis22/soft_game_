using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
public class BattleStandingFocusController : MonoBehaviour
{
    private const string PreviewRootName = "전투 UI - 확대";
    private const string FocusUiRootName = "Battle Focus UI";
    private const string FocusShadowRootName = "Battle Focus Shadow";
    private const string FocusCloseClickBoxName = "Battle Focus Close Click Box";
    private static readonly string[] StatsTextNames =
    {
        "Stats Text Red",
        "Stats Text Green",
        "Stats Text Blue",
        "Stats Text Score",
    };

    public static BattleStandingFocusController Instance { get; private set; }

    [Header("Camera")]
    [SerializeField] private Camera targetCamera;
    [SerializeField, Min(0.01f)] private float focusedFieldOfView = 20f;
    [SerializeField, Min(1f)] private float focusDistance = 400f;
    [SerializeField] private float focusScreenOffsetRight = 45f;
    [SerializeField] private float focusWorldOffsetY = -20f;
    [SerializeField, Min(0.01f)] private float transitionSeconds = 0.28f;
    [SerializeField] private Vector2 focusCloseClickBoxDefaultSize = new Vector2(3f, 6f);

    [Header("UI")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Color panelColor = new Color(1f, 1f, 1f, 0.94f);
    [SerializeField] private Color accentColor = new Color(0.08f, 0.075f, 0.06f, 0.8f);
    [SerializeField] private Color textColor = new Color(0.08f, 0.075f, 0.06f, 1f);
    [SerializeField] private Color usedCardColor = new Color(0.22f, 0.22f, 0.22f, 0.82f);
    [SerializeField] private Color normalCardColor = Color.white;

    [Header("Inform Artwork")]
    [SerializeField] private Sprite informJobSprite;
    [SerializeField] private Sprite informCardsSprite;
    [SerializeField] private Sprite informScoreboardSprite;
    [SerializeField] private Sprite informGimmickSprite;
    [SerializeField] private Sprite informGimmickTabSprite;
    [SerializeField] private Sprite informShadowSprite;

    private RectTransform shadowRoot;
    private RectTransform root;
    private TMP_Text titleText;
    private TMP_Text statsText;
    private TMP_Text[] statsValueTexts;
    private TMP_Text gimmickText;
    private Button[] skillButtons;
    private TMP_Text[] skillButtonLabels;
    private Image[] cardImages;
    private Image[] cardOutlineImages;
    private TMP_Text[] cardLabels;
    private CardDrawPopupController cardPopupController;
    private BattleHudUI battleHudUi;
    private RectTransform previewContainer;
    private GameObject focusCloseClickBoxObject;
    private BoxCollider focusCloseClickBoxWorldCollider;
    private BoxCollider2D focusCloseClickBoxCollider;
    private bool focusCloseClickBoxIsAuthored;

    private BattlePlayerStanding focusedStanding;
    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private float originalFieldOfView;
    private bool hasCameraSnapshot;
    private Coroutine cameraRoutine;
    private BattleCameraMoveRunner cameraMoveRunner;
    private int selectedSkillIndex;

    public static BattleStandingFocusController GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        BattleStandingFocusController named = FindNamedFocusController();
        if (named != null)
            return named;

        BattleStandingFocusController existing =
            FindAnyObjectByType<BattleStandingFocusController>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        GameObject go = new GameObject("Battle Standing Focus Controller");
        return go.AddComponent<BattleStandingFocusController>();
    }

    private void Awake()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            CacheReferences();
            return;
        }
#endif

        if (!IsPreviewRoot() && GetComponent<Canvas>() != null)
        {
            enabled = false;
            return;
        }

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CacheReferences();
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (Application.isPlaying)
            return;

        if (!IsPreviewRoot())
            return;

        BuildPreviewInEditor(gameObject.activeInHierarchy);
    }
#endif

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (focusedStanding != null)
            RefreshFocusedUi();
    }

    public void ToggleFocus(BattlePlayerStanding standing)
    {
        if (standing == null)
            return;

        if (focusedStanding == standing)
            return;

        Focus(standing);
    }

    public void Focus(BattlePlayerStanding standing)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        CacheReferences();
        EnsureUi();

        if (targetCamera == null)
        {
            Debug.LogWarning("BattleStandingFocusController: targetCamera not found.");
            return;
        }

        if (!hasCameraSnapshot)
        {
            originalCameraPosition = targetCamera.transform.position;
            originalCameraRotation = targetCamera.transform.rotation;
            originalFieldOfView = targetCamera.fieldOfView;
            hasCameraSnapshot = true;
        }

        focusedStanding = standing;
        selectedSkillIndex = 0;
        EnsureFocusCloseClickBox();
        PositionFocusCloseClickBox(standing);
        SetFocusCloseClickBoxActive(true);
        SetLocalCardHandHidden(true);
        SetBattleHudHidden(true);
        shadowRoot.gameObject.SetActive(true);
        root.gameObject.SetActive(true);
        shadowRoot.SetAsLastSibling();
        root.SetAsLastSibling();
        focusCloseClickBoxObject.transform.SetAsLastSibling();
        RefreshFocusedUi();

        Vector3 focusPoint = standing.transform.position + Vector3.up * focusWorldOffsetY;
        Vector3 targetPosition =
            focusPoint -
            targetCamera.transform.forward * focusDistance +
            targetCamera.transform.right * focusScreenOffsetRight;

        StartCameraMove(targetPosition, targetCamera.transform.rotation, focusedFieldOfView);
    }

    public void ClearFocus()
    {
        SetLocalCardHandHidden(false);
        SetBattleHudHidden(false);
        focusedStanding = null;
        SetFocusCloseClickBoxActive(false);

        if (shadowRoot != null)
            shadowRoot.gameObject.SetActive(false);

        if (root != null)
            root.gameObject.SetActive(false);

        if (targetCamera != null && hasCameraSnapshot)
        {
            bool usesExternalRunner = StartCameraMove(
                originalCameraPosition,
                originalCameraRotation,
                originalFieldOfView,
                clearSnapshotOnComplete: true,
                deactivatePreviewOnComplete: IsPreviewRoot());

            if (usesExternalRunner && IsPreviewRoot())
                gameObject.SetActive(false);

            return;
        }

        hasCameraSnapshot = false;

        if (IsPreviewRoot())
            gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    public void BuildPreviewInEditor(bool visible)
    {
        CacheReferences();
        EnsureUi();
        selectedSkillIndex = 0;
        focusedStanding = null;

        if (shadowRoot != null)
            shadowRoot.gameObject.SetActive(visible);

        if (root != null)
            root.gameObject.SetActive(visible);

        if (root.childCount == 0)
            RefreshPreviewUi();
    }
#endif

    private void CacheReferences()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCanvas == null)
            targetCanvas = FindAnyObjectByType<Canvas>();

        if (cardPopupController == null)
            cardPopupController = CardDrawPopupController.GetAny();

        if (battleHudUi == null)
            battleHudUi = FindAnyObjectByType<BattleHudUI>(FindObjectsInactive.Include);
    }

    private void DisableShadowCloseButton()
    {
        if (shadowRoot == null)
            return;

        Button closeButton = shadowRoot.GetComponent<Button>();
        if (closeButton != null)
            closeButton.enabled = false;
    }

    private void EnsureFocusCloseClickBox()
    {
        if (focusCloseClickBoxObject == null)
        {
            focusCloseClickBoxObject = FindFocusCloseClickBoxObject();
            focusCloseClickBoxIsAuthored = focusCloseClickBoxObject != null;
            if (focusCloseClickBoxObject == null)
            {
                focusCloseClickBoxObject = new GameObject(FocusCloseClickBoxName);
                focusCloseClickBoxIsAuthored = false;
            }
        }

        if (focusCloseClickBoxWorldCollider == null)
            focusCloseClickBoxWorldCollider = focusCloseClickBoxObject.GetComponent<BoxCollider>();

        if (focusCloseClickBoxCollider == null)
            focusCloseClickBoxCollider = focusCloseClickBoxObject.GetComponent<BoxCollider2D>();

        if (focusCloseClickBoxWorldCollider == null && focusCloseClickBoxCollider == null)
        {
            focusCloseClickBoxWorldCollider = focusCloseClickBoxObject.AddComponent<BoxCollider>();
            focusCloseClickBoxWorldCollider.size = new Vector3(focusCloseClickBoxDefaultSize.x, focusCloseClickBoxDefaultSize.y, 0.2f);
        }

        Image clickBoxImage = focusCloseClickBoxObject.GetComponent<Image>();
        if (focusCloseClickBoxObject.GetComponent<RectTransform>() != null)
        {
            if (clickBoxImage == null)
                clickBoxImage = focusCloseClickBoxObject.AddComponent<Image>();

            clickBoxImage.color = Color.clear;
            clickBoxImage.raycastTarget = true;
        }

        if (focusCloseClickBoxWorldCollider != null)
            focusCloseClickBoxWorldCollider.isTrigger = true;

        if (focusCloseClickBoxCollider != null)
            focusCloseClickBoxCollider.isTrigger = true;

        BattleFocusCloseClickBox clickBox = focusCloseClickBoxObject.GetComponent<BattleFocusCloseClickBox>();
        if (clickBox == null)
            clickBox = focusCloseClickBoxObject.AddComponent<BattleFocusCloseClickBox>();

        clickBox.Bind(this);
        focusCloseClickBoxObject.SetActive(false);
    }

    private GameObject FindFocusCloseClickBoxObject()
    {
        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == FocusCloseClickBoxName)
                return candidate.gameObject;
        }

        return null;
    }

    private void PositionFocusCloseClickBox(BattlePlayerStanding standing)
    {
        if (focusCloseClickBoxObject == null || standing == null)
            return;

        if (focusCloseClickBoxIsAuthored)
            return;

        Vector3 position = standing.transform.position;
        if (targetCamera != null)
            position -= targetCamera.transform.forward * 0.1f;

        focusCloseClickBoxObject.transform.position = position;
        focusCloseClickBoxObject.transform.rotation = Quaternion.identity;
    }

    private void SetFocusCloseClickBoxActive(bool active)
    {
        if (focusCloseClickBoxObject != null)
            focusCloseClickBoxObject.SetActive(active);
    }

    private void SetLocalCardHandHidden(bool hidden)
    {
        if (cardPopupController == null)
            cardPopupController = CardDrawPopupController.GetAny();

        if (cardPopupController != null)
            cardPopupController.SetFocusHidden(hidden);
    }

    private void SetBattleHudHidden(bool hidden)
    {
        if (battleHudUi == null)
            battleHudUi = FindAnyObjectByType<BattleHudUI>(FindObjectsInactive.Include);

        if (battleHudUi != null)
            battleHudUi.SetFocusHidden(hidden);
    }

    private void EnsureUi()
    {
        if (root != null)
            return;

        if (transform.childCount > 0)
        {
            shadowRoot = FindDirectRect(FocusShadowRootName);
            root = FindDirectRect(FocusUiRootName);
            if (root != null)
            {
                CacheAuthoredUi();
                DisableShadowCloseButton();
                return;
            }
        }

        if (TryUseExternalAuthoredUi())
            return;

        if (targetCanvas == null)
        {
            GameObject canvasGo = new GameObject("Battle Focus Canvas");
            targetCanvas = canvasGo.AddComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        Transform uiParent = GetUiParent();

        shadowRoot = CreateRect(FocusShadowRootName, uiParent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        shadowRoot.gameObject.SetActive(false);

        Image dim = shadowRoot.gameObject.AddComponent<Image>();
        ApplyPanelSprite(dim, GetInformShadowSprite(), new Color(0f, 0f, 0f, 0.28f));
        dim.color = new Color(0f, 0f, 0f, 1f);
        dim.raycastTarget = true;

        root = CreateRect(FocusUiRootName, uiParent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        root.gameObject.SetActive(false);

        CreatePanel("Job Inform", root, new Vector2(0f, 0.818f), new Vector2(0.21f, 1f), Vector2.zero, Vector2.zero, GetInformJobSprite());

        RectTransform cardPanel = CreatePanel("Card Status", root, new Vector2(0.07f, 0.05f), new Vector2(0.56f, 0.41f), new Vector2(0f, -1f), new Vector2(0f, -70f), GetInformCardsSprite());

        RectTransform cardTitleTab = CreateRect("Card Title Tab", root, new Vector2(0.085f, 0.41f), new Vector2(0.33f, 0.455f), new Vector2(10f, -149f), new Vector2(10f, -150f));
        titleText = CreateText("Title", cardTitleTab, "숫자 상황", 26, TextAlignmentOptions.Center);
        Stretch(titleText.rectTransform, 0f, 0f, 0f, 0f);

        cardImages = new Image[6];
        cardOutlineImages = new Image[6];
        cardLabels = new TMP_Text[6];
        for (int i = 0; i < cardImages.Length; i++)
        {
            RectTransform slot = CreateRect($"Card {i + 1}", cardPanel, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(148f, 198f));
            slot.anchorMin = new Vector2(0.5f, 0.5f);
            slot.anchorMax = new Vector2(0.5f, 0.5f);
            float centeredIndex = i - (cardImages.Length - 1) * 0.5f;
            slot.anchoredPosition = new Vector2(centeredIndex * 145f, -20f);
            slot.sizeDelta = new Vector2(131f, 180f);

            cardImages[i] = slot.gameObject.AddComponent<Image>();
            cardImages[i].preserveAspect = true;
            cardImages[i].raycastTarget = false;

            cardLabels[i] = CreateText("Label", slot, (i + 1).ToString(), 30, TextAlignmentOptions.Center);
            Stretch(cardLabels[i].rectTransform, 0f, 0f, 0f, 0f);

            RectTransform outline = CreateRect("Outline", slot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            cardOutlineImages[i] = outline.gameObject.AddComponent<Image>();
            cardOutlineImages[i].preserveAspect = false;
            cardOutlineImages[i].raycastTarget = false;
            outline.SetAsLastSibling();
        }

        RectTransform statsTitleTab = CreateRect("Adventurer Info Tab", root, new Vector2(0.63f, 0.9f), new Vector2(0.74f, 0.965f), new Vector2(-40f, -26.5f), new Vector2(-40f, -61.5f));
        TMP_Text statsTitle = CreateText("Stats Title", statsTitleTab, "모험가 정보", 24, TextAlignmentOptions.Center);
        Stretch(statsTitle.rectTransform, 0f, 0f, 0f, 0f);

        RectTransform statsPanel = CreatePanel("Stats", root, new Vector2(0.60f, 0.79f), new Vector2(0.98f, 0.915f), new Vector2(21.99995f, -1f), new Vector2(-201.6001f, -1f), GetInformScoreboardSprite());
        CreateStatsTexts(statsPanel);
        statsTitleTab.SetAsLastSibling();

        skillButtons = new Button[3];
        skillButtonLabels = new TMP_Text[3];
        CreateSkillButton(0, "Skill Tab 1", new Vector2(0.63f, 0.67f), new Vector2(0.705f, 0.715f));
        CreateSkillButton(1, "Skill Tab 2", new Vector2(0.715f, 0.67f), new Vector2(0.79f, 0.715f));
        CreateSkillButton(2, "Skill Tab 3", new Vector2(0.80f, 0.67f), new Vector2(0.875f, 0.715f));

        RectTransform gimmickPanel = CreatePanel("Gimmick", root, new Vector2(0.60f, 0.065f), new Vector2(0.98f, 0.675f), Vector2.zero, Vector2.zero, GetInformGimmickSprite());
        gimmickText = CreateText("Gimmick Text", gimmickPanel, string.Empty, 23, TextAlignmentOptions.TopLeft);
        Stretch(gimmickText.rectTransform, 36f, 28f, 30f, 22f);
    }

    private bool TryUseExternalAuthoredUi()
    {
        RectTransform externalRoot = FindBestExternalFocusUi();
        if (externalRoot == null)
            return false;

        root = externalRoot;
        shadowRoot = FindSiblingRect(root, FocusShadowRootName);
        if (shadowRoot == null)
            shadowRoot = FindFirstExternalRect(FocusShadowRootName);

        CacheAuthoredUi();
        DisableShadowCloseButton();

        return true;
    }

    private RectTransform FindBestExternalFocusUi()
    {
        RectTransform[] rects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
        RectTransform best = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform candidate = rects[i];
            if (candidate == null || candidate.name != FocusUiRootName || candidate.transform.IsChildOf(transform))
                continue;

            int score = ScoreExternalFocusUi(candidate);
            if (score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    private int ScoreExternalFocusUi(RectTransform candidate)
    {
        int score = candidate.childCount;

        if (targetCanvas != null && candidate.transform.parent == targetCanvas.transform)
            score += 100;

        if (FindSiblingRect(candidate, FocusShadowRootName) != null)
            score += 20;

        if (FindChildRecursive(candidate, "Job Inform") != null)
            score += 10;

        if (FindChildRecursive(candidate, "Card Status") != null)
            score += 10;

        if (FindChildRecursive(candidate, "Stats") != null)
            score += 10;

        if (FindChildRecursive(candidate, "Gimmick") != null)
            score += 10;

        return score;
    }

    private void CacheAuthoredUi()
    {
        titleText = FindText("Title");
        statsText = FindText("Stats Text");
        CacheStatsTextBoxes();
        gimmickText = FindText("Gimmick Text");

        skillButtons = new Button[3];
        skillButtonLabels = new TMP_Text[3];
        if (skillButtons == null)
            skillButtons = new Button[0];

        for (int i = 0; i < skillButtons.Length; i++)
        {
            Transform skillTab = FindChildRecursive(root, $"Skill Tab {i + 1}");
            if (skillTab == null)
                continue;

            skillButtons[i] = skillTab.GetComponent<Button>();
            skillButtonLabels[i] = skillTab.GetComponentInChildren<TMP_Text>(true);
        }

        cardImages = new Image[6];
        cardOutlineImages = new Image[6];
        cardLabels = new TMP_Text[6];
        for (int i = 0; i < cardImages.Length; i++)
        {
            Transform card = FindChildRecursive(root, $"Card {i + 1}");
            if (card == null)
                continue;

            cardImages[i] = card.GetComponent<Image>();
            Transform outline = FindChildRecursive(card, "Outline");
            cardOutlineImages[i] = outline != null ? outline.GetComponent<Image>() : null;
            Transform label = FindChildRecursive(card, "Label");
            cardLabels[i] = label != null ? label.GetComponent<TMP_Text>() : null;
        }
    }

    private RectTransform FindDirectRect(string childName)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name == childName)
                return child.GetComponent<RectTransform>();
        }

        return null;
    }

    private RectTransform FindSiblingRect(RectTransform siblingOf, string siblingName)
    {
        if (siblingOf == null || siblingOf.parent == null)
            return null;

        Transform parent = siblingOf.parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name == siblingName)
                return child.GetComponent<RectTransform>();
        }

        return null;
    }

    private RectTransform FindFirstExternalRect(string objectName)
    {
        RectTransform[] rects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform candidate = rects[i];
            if (candidate != null && candidate.name == objectName && !candidate.transform.IsChildOf(transform))
                return candidate;
        }

        return null;
    }

    private TMP_Text FindText(string objectName)
    {
        Transform child = FindChildRecursive(root, objectName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private Transform FindChildRecursive(Transform searchRoot, string childName)
    {
        if (searchRoot == null)
            return null;

        for (int i = 0; i < searchRoot.childCount; i++)
        {
            Transform child = searchRoot.GetChild(i);
            if (child == null)
                continue;

            if (child.name == childName)
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private Transform GetUiParent()
    {
        RectTransform ownRect = transform as RectTransform;
        if (ownRect != null && targetCanvas != null && transform.IsChildOf(targetCanvas.transform))
        {
            Stretch(ownRect, 0f, 0f, 0f, 0f);
            return ownRect;
        }

        previewContainer = CreateRect("Battle Focus Preview Root", targetCanvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return previewContainer;
    }

    private static BattleStandingFocusController FindNamedFocusController()
    {
        BattleStandingFocusController[] controllers =
            FindObjectsByType<BattleStandingFocusController>(FindObjectsInactive.Include);

        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i] != null && controllers[i].IsPreviewRoot())
                return controllers[i];
        }

        return null;
    }

    private bool IsPreviewRoot()
    {
        return string.Equals(gameObject.name, PreviewRootName, System.StringComparison.Ordinal);
    }

    private void RefreshFocusedUi()
    {
        if (focusedStanding == null)
            return;

        NetworkPlayerRegistry.TryGetByPlayerIndex(focusedStanding.PlayerIndex, out NetworkPlayerState player);
        string nickname = string.IsNullOrWhiteSpace(focusedStanding.RawNickname) ? $"P{focusedStanding.PlayerIndex + 1}" : focusedStanding.RawNickname;
        SetText(titleText, $"{nickname} 의 숫자 상황");

        int red = player != null ? player.CrystalRed.Value : 0;
        int blue = player != null ? player.CrystalBlue.Value : 0;
        int green = player != null ? player.CrystalGreen.Value : 0;
        int score = red + blue + green;

        SetStatsTexts(red, green, blue, score, player);
        RefreshSkillPanel(player);

        for (int i = 0; i < cardImages.Length; i++)
        {
            int cardValue = i + 1;
            bool hasCard = player == null || player.HasNumberCard(cardValue);

            if (cardImages[i] != null)
            {
                cardImages[i].sprite = GetSharedCardFrontSprite(focusedStanding, cardValue);
                cardImages[i].color = hasCard ? normalCardColor : usedCardColor;
            }

            if (cardLabels[i] != null)
            {
                cardLabels[i].enabled = true;
                cardLabels[i].text = cardValue.ToString();
            }

            if (cardOutlineImages != null && i < cardOutlineImages.Length && cardOutlineImages[i] != null)
            {
                cardOutlineImages[i].sprite = GetSharedCardOutlineSprite();
                cardOutlineImages[i].color = cardOutlineImages[i].sprite != null ? Color.white : Color.clear;
            }
        }
    }

    private void RefreshPreviewUi()
    {
        if (titleText != null)
            titleText.text = "Akaneru 의 숫자 상황";

        SetStatsTexts(0, 0, 0, 0, null);

        RefreshSkillPanel(null);

        for (int i = 0; cardImages != null && i < cardImages.Length; i++)
        {
            if (cardImages[i] != null)
            {
                cardImages[i].sprite = GetSharedCardFrontSprite(null, i + 1);
                cardImages[i].color = normalCardColor;
            }

            if (cardLabels != null && i < cardLabels.Length && cardLabels[i] != null)
            {
                cardLabels[i].enabled = true;
                cardLabels[i].text = (i + 1).ToString();
            }

            if (cardOutlineImages != null && i < cardOutlineImages.Length && cardOutlineImages[i] != null)
            {
                cardOutlineImages[i].sprite = GetSharedCardOutlineSprite();
                cardOutlineImages[i].color = cardOutlineImages[i].sprite != null ? Color.white : Color.clear;
            }
        }
    }

    private void CreateStatsTexts(RectTransform statsPanel)
    {
        statsValueTexts = new TMP_Text[StatsTextNames.Length];
        for (int i = 0; i < statsValueTexts.Length; i++)
        {
            statsValueTexts[i] = CreateText(StatsTextNames[i], statsPanel, string.Empty, 34, TextAlignmentOptions.Center);
            SetDefaultStatsTextRect(statsValueTexts[i].rectTransform, i);
        }
    }

    private void CacheStatsTextBoxes()
    {
        statsValueTexts = new TMP_Text[StatsTextNames.Length];
        for (int i = 0; i < statsValueTexts.Length; i++)
            statsValueTexts[i] = FindText(StatsTextNames[i]);

        if (statsValueTexts[0] == null)
            statsValueTexts[0] = statsText;
    }

    private void SetStatsTexts(int red, int green, int blue, int score, NetworkPlayerState player)
    {
        if (statsValueTexts == null || statsValueTexts.Length != StatsTextNames.Length)
            CacheStatsTextBoxes();

        if (HasSplitStatsTexts())
        {
            SetText(statsValueTexts[0], red.ToString());
            SetText(statsValueTexts[1], green.ToString());
            SetText(statsValueTexts[2], blue.ToString());

            string scoreText = $"{score}점";
            if (player != null && player.IsDefenderCustomer.Value)
            {
                string blocked = player.DefenderDebtBlockedThisStage.Value ? " / debt blocked" : string.Empty;
                scoreText += $"\nNetra customer / debt {player.DefenderDebt.Value}{blocked}";
            }

            SetText(statsValueTexts[3], scoreText);
            return;
        }

        SetText(statsText, $"{red}          {green}          {blue}      {score}점");
        if (player != null && player.IsDefenderCustomer.Value && statsText != null)
        {
            string blocked = player.DefenderDebtBlockedThisStage.Value ? " / debt blocked" : string.Empty;
            statsText.text += $"\nNetra customer / debt {player.DefenderDebt.Value}{blocked}";
        }
    }

    private bool HasSplitStatsTexts()
    {
        if (statsValueTexts == null || statsValueTexts.Length != StatsTextNames.Length)
            return false;

        for (int i = 0; i < statsValueTexts.Length; i++)
        {
            if (statsValueTexts[i] == null)
                return false;
        }

        return true;
    }

    private Sprite GetSharedCardFrontSprite(BattlePlayerStanding standing, int cardValue)
    {
        CardDrawPopupController cardPopup = CardDrawPopupController.GetAny();
        if (cardPopup != null && cardPopup.CardFrontSprite != null)
            return cardPopup.CardFrontSprite;

        return standing != null ? standing.GetNumberCardSprite(cardValue) : null;
    }

    private Sprite GetSharedCardOutlineSprite()
    {
        CardDrawPopupController cardPopup = CardDrawPopupController.GetAny();
        return cardPopup != null ? cardPopup.CardOutlineSprite : null;
    }

    private void RefreshSkillPanel(NetworkPlayerState player)
    {
        SkillInfo[] skills = GetSkillInfos(player != null ? player.Job.Value : JobType.None);
        int skillCount = skills != null ? skills.Length : 0;

        if (selectedSkillIndex >= skillCount)
            selectedSkillIndex = 0;

        for (int i = 0; i < skillButtons.Length; i++)
        {
            if (skillButtons[i] == null)
                continue;

            bool visible = i < skillCount;
            skillButtons[i].gameObject.SetActive(visible);
            if (!visible)
                continue;

            Image image = skillButtons[i].GetComponent<Image>();
            if (image != null)
                image.color = i == selectedSkillIndex ? Color.white : new Color(1f, 1f, 1f, 0.72f);

            if (skillButtonLabels[i] != null)
                skillButtonLabels[i].text = $"스킬 {i + 1}";
        }

        SetText(gimmickText, skillCount > 0 ? skills[selectedSkillIndex].Description : "직업 정보가 아직 없습니다.");
    }

    private void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private SkillInfo[] GetSkillInfos(JobType job)
    {
        if (job == JobType.Hero)
        {
            return new[]
            {
                new SkillInfo(
                    "계산된 승리",
                    "계산된 승리\n패시브\n\n발동 조건\n- 이번 라운드에서 플레이어 전원의 숫자가 겹치지 않고 모든 공격이 명중했을 때.\n- 몬스터 토벌을 성공했을 때.\n\n효과\n- 나이트는 무조건 가장 낮은 숫자를 낸 것으로 취급됩니다.\n\n결과\n- 나이트가 확정적으로 1등 전리품을 획득합니다.\n\n기획 의도\n- 나이트가 파티원들을 설득하며, 완벽하게 임무를 수행하는 모습을 구현."),
                new SkillInfo(
                    "망한 작전",
                    "망한 작전\n패시브\n\n발동 조건\n- 이번 라운드에서 누군가의 숫자가 겹쳐서 플레이어의 공격이 하나라도 취소되었을 때.\n- 몬스터 토벌을 실패했을 때.\n\n효과\n- 나이트 또한 몬스터의 공격을 함께 받습니다.\n\n결과\n- 몬스터에게 공격받아, 나이트가 자신이 가장 많이 가진 재화 전부를 보험 항아리에 떨어뜨립니다.\n\n기획 의도\n- 나이트가 파티원들의 방해 때문에 작전이 망하고 몬스터에게 멱살을 잡히는 고통을 구현."),
                new SkillInfo(
                    "책임",
                    "책임\n액티브\n\n발동 조건\n- 계산된 승리가 발동했을 때 또는 망한 작전이 발동했을 때.\n- 망한 작전이 발동했을 때에는 나이트가 실제로 자신의 재화를 1개 이상 보험 항아리에 떨어뜨렸을 때 발동합니다.\n- 잃은 재화가 없다면 발동할 수 없습니다.\n\n효과\n- 계산된 승리가 발동했을 시, 가장 적은 수의 재화 1개를 국고에서 우선으로 가져옵니다.\n- 망한 작전이 발동했을 시, 이번 라운드에서 숫자를 겹친 파티원 중 1명을 지목합니다.\n- 지목당한 플레이어의 원하는 재화 1개를 훔쳐 자신의 주머니로 가져옵니다.\n\n결과\n- 훔친 재화는 몬스터의 공격에도 보험 항아리에 떨어뜨리지 않고, 나이트의 안전 자산이 됩니다.")
            };
        }

        string description = job switch
        {
            JobType.Saint => "성녀의 스킬\n\n상세 효과가 정해지면 이 영역에 표시됩니다.",
            JobType.Witch => "마녀의 스킬\n\n상세 효과가 정해지면 이 영역에 표시됩니다.",
            JobType.Archer => "궁수의 스킬\n\n상세 효과가 정해지면 이 영역에 표시됩니다.",
            JobType.Fighter => "격투가의 스킬\n\n상세 효과가 정해지면 이 영역에 표시됩니다.",
            JobType.Butcher => "도살자의 스킬\n\n상세 효과가 정해지면 이 영역에 표시됩니다.",
            JobType.Guardian => "수호자의 스킬\n\n상세 효과가 정해지면 이 영역에 표시됩니다.",
            JobType.Assassin => "암살자의 스킬\n\n상세 효과가 정해지면 이 영역에 표시됩니다.",
            JobType.Defender => "디펜더(네트라)\n\n대부: 파티원 1명을 고객으로 지정하고, 이번 라운드 몬스터 공격을 대신 받습니다.\n\n고객이 보상을 얻으면 빚이 증가하고, 스테이지 종료 시 가장 빚이 큰 고객에게서 정산합니다.",
            _ => "직업 정보가 아직 없습니다.\n\n선택한 모험가의 기믹이 이 영역에 표시됩니다."
        };

        return new[] { new SkillInfo("스킬", description) };
    }

    private void CreateSkillButton(int index, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        RectTransform tab = CreateTab(name, root, anchorMin, anchorMax, $"스킬 {index + 1}", 21, GetInformGimmickTabSprite());
        Button button = tab.gameObject.AddComponent<Button>();
        int capturedIndex = index;
        button.onClick.AddListener(() =>
        {
            selectedSkillIndex = capturedIndex;
            NetworkPlayerRegistry.TryGetByPlayerIndex(focusedStanding != null ? focusedStanding.PlayerIndex : -1, out NetworkPlayerState player);
            RefreshSkillPanel(player);
        });

        skillButtons[index] = button;
        skillButtonLabels[index] = tab.GetComponentInChildren<TMP_Text>(true);
    }

    private bool StartCameraMove(
        Vector3 position,
        Quaternion rotation,
        float fieldOfView,
        bool clearSnapshotOnComplete = false,
        bool deactivatePreviewOnComplete = false)
    {
        BattleCameraMoveRunner runner = GetCameraMoveRunner();
        if (runner != null)
        {
            cameraRoutine = null;
            runner.StartMove(
                this,
                targetCamera,
                position,
                rotation,
                fieldOfView,
                transitionSeconds,
                clearSnapshotOnComplete,
                deactivatePreviewOnComplete);
            return true;
        }

        if (cameraRoutine != null)
            StopCoroutine(cameraRoutine);

        cameraRoutine = StartCoroutine(MoveCamera(position, rotation, fieldOfView, clearSnapshotOnComplete, deactivatePreviewOnComplete));
        return false;
    }

    private BattleCameraMoveRunner GetCameraMoveRunner()
    {
        if (targetCamera == null)
            return null;

        if (cameraMoveRunner == null)
            cameraMoveRunner = targetCamera.GetComponent<BattleCameraMoveRunner>();

        if (cameraMoveRunner == null)
            cameraMoveRunner = targetCamera.gameObject.AddComponent<BattleCameraMoveRunner>();

        return cameraMoveRunner;
    }

    private IEnumerator MoveCamera(
        Vector3 position,
        Quaternion rotation,
        float fieldOfView,
        bool clearSnapshotOnComplete,
        bool deactivatePreviewOnComplete)
    {
        Vector3 startPosition = targetCamera.transform.position;
        Quaternion startRotation = targetCamera.transform.rotation;
        float startFieldOfView = targetCamera.fieldOfView;
        float elapsed = 0f;

        while (elapsed < transitionSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionSeconds);
            targetCamera.transform.position = Vector3.Lerp(startPosition, position, t);
            targetCamera.transform.rotation = Quaternion.Slerp(startRotation, rotation, t);
            targetCamera.fieldOfView = Mathf.Lerp(startFieldOfView, fieldOfView, t);
            yield return null;
        }

        targetCamera.transform.position = position;
        targetCamera.transform.rotation = rotation;
        targetCamera.fieldOfView = fieldOfView;
        cameraRoutine = null;

        if (clearSnapshotOnComplete)
            hasCameraSnapshot = false;

        if (deactivatePreviewOnComplete)
            gameObject.SetActive(false);
    }

    internal void CompleteCameraMove(bool clearSnapshotOnComplete, bool deactivatePreviewOnComplete)
    {
        cameraRoutine = null;

        if (clearSnapshotOnComplete)
            hasCameraSnapshot = false;

        if (deactivatePreviewOnComplete && gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Sprite backgroundSprite = null)
    {
        RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
        Image image = rect.gameObject.AddComponent<Image>();
        ApplyPanelSprite(image, backgroundSprite, panelColor);
        image.raycastTarget = true;

        if (backgroundSprite == null)
        {
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = accentColor;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        return rect;
    }

    private RectTransform CreateTab(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, string label, int fontSize, Sprite backgroundSprite = null)
    {
        RectTransform tab = CreatePanel(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero, backgroundSprite);
        TMP_Text tabText = CreateText("Label", tab, label, fontSize, TextAlignmentOptions.Center);
        Stretch(tabText.rectTransform, 0f, 0f, 0f, 0f);
        return tab;
    }

    private TMP_Text CreateText(string name, Transform parent, string text, int fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = textColor;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private static void SetDefaultStatsTextRect(RectTransform rect, int index)
    {
        if (rect == null)
            return;

        float left = 34f;
        float right = 5.999998f;
        float width = 1f / StatsTextNames.Length;
        rect.anchorMin = new Vector2(index * width, 0f);
        rect.anchorMax = new Vector2((index + 1) * width, 1f);
        rect.offsetMin = new Vector2(index == 0 ? left : 0f, 0f);
        rect.offsetMax = new Vector2(index == StatsTextNames.Length - 1 ? -right : 0f, -20f);
    }

    private RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    private void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private void ApplyPanelSprite(Image image, Sprite sprite, Color fallbackColor)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = sprite != null ? Color.white : fallbackColor;
    }

    private Sprite GetInformJobSprite()
    {
        return informJobSprite != null ? informJobSprite : LoadEditorSprite("Assets/souce/inform_sources/inform_job.png");
    }

    private Sprite GetInformCardsSprite()
    {
        return informCardsSprite != null ? informCardsSprite : LoadEditorSprite("Assets/souce/inform_sources/inform_cards.png");
    }

    private Sprite GetInformScoreboardSprite()
    {
        return informScoreboardSprite != null ? informScoreboardSprite : LoadEditorSprite("Assets/souce/inform_sources/inform_scoreboard.png");
    }

    private Sprite GetInformGimmickSprite()
    {
        return informGimmickSprite != null ? informGimmickSprite : LoadEditorSprite("Assets/souce/inform_sources/inform_gimmick.png");
    }

    private Sprite GetInformGimmickTabSprite()
    {
        return informGimmickTabSprite != null ? informGimmickTabSprite : LoadEditorSprite("Assets/souce/inform_sources/inform_gimmick_tab.png");
    }

    private Sprite GetInformShadowSprite()
    {
        return informShadowSprite != null ? informShadowSprite : LoadEditorSprite("Assets/souce/inform_sources/inform_shadow.png");
    }

    private Sprite LoadEditorSprite(string assetPath)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
#else
        return null;
#endif
    }

    private struct SkillInfo
    {
        public SkillInfo(string title, string description)
        {
            Title = title;
            Description = description;
        }

        public string Title { get; }
        public string Description { get; }
    }
}

public class BattleCameraMoveRunner : MonoBehaviour
{
    private Coroutine moveRoutine;

    public void StartMove(
        BattleStandingFocusController owner,
        Camera targetCamera,
        Vector3 position,
        Quaternion rotation,
        float fieldOfView,
        float transitionSeconds,
        bool clearSnapshotOnComplete,
        bool deactivatePreviewOnComplete)
    {
        if (moveRoutine != null)
            StopCoroutine(moveRoutine);

        moveRoutine = StartCoroutine(MoveCamera(
            owner,
            targetCamera,
            position,
            rotation,
            fieldOfView,
            transitionSeconds,
            clearSnapshotOnComplete,
            deactivatePreviewOnComplete));
    }

    private IEnumerator MoveCamera(
        BattleStandingFocusController owner,
        Camera targetCamera,
        Vector3 position,
        Quaternion rotation,
        float fieldOfView,
        float transitionSeconds,
        bool clearSnapshotOnComplete,
        bool deactivatePreviewOnComplete)
    {
        if (targetCamera == null)
        {
            moveRoutine = null;
            if (owner != null)
                owner.CompleteCameraMove(clearSnapshotOnComplete, deactivatePreviewOnComplete);
            yield break;
        }

        Vector3 startPosition = targetCamera.transform.position;
        Quaternion startRotation = targetCamera.transform.rotation;
        float startFieldOfView = targetCamera.fieldOfView;
        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, transitionSeconds);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            targetCamera.transform.position = Vector3.Lerp(startPosition, position, t);
            targetCamera.transform.rotation = Quaternion.Slerp(startRotation, rotation, t);
            targetCamera.fieldOfView = Mathf.Lerp(startFieldOfView, fieldOfView, t);
            yield return null;
        }

        targetCamera.transform.position = position;
        targetCamera.transform.rotation = rotation;
        targetCamera.fieldOfView = fieldOfView;
        moveRoutine = null;

        if (owner != null)
            owner.CompleteCameraMove(clearSnapshotOnComplete, deactivatePreviewOnComplete);
    }
}
