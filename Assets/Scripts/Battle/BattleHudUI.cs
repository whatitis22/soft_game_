using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
public class BattleHudUI : MonoBehaviour
{
    public static BattleHudUI Instance { get; private set; }

    private const string TargetSceneName = "GameScene";
    private const int StageEncounterCount = 4;

    [Header("Layout")]
    [SerializeField] private Vector2 playerPanelSize = new Vector2(348f, 110f);
    [SerializeField] private Vector2 insurancePanelSize = new Vector2(408f, 98f);
    [SerializeField] private Vector2 bagPanelSize = new Vector2(250f, 250f);
    [SerializeField] private Vector2 monsterInfoSize = new Vector2(440f, 440f);

    [Header("Style")]
    [SerializeField] private Color panelColor = new Color(1f, 1f, 1f, 0.86f);
    [SerializeField] private Color textColor = new Color(0.04f, 0.04f, 0.04f, 1f);
    [SerializeField] private Color lineColor = new Color(0.02f, 0.02f, 0.02f, 0.92f);
    [SerializeField] private Sprite insuranceJarSprite;
    [SerializeField] private Sprite itemBagSprite;
    [SerializeField] private Sprite monsterBookSprite;
    [SerializeField] private Sprite stageProgressIndicatorSprite;
    [SerializeField] private Sprite stageProgressBarSprite;
    [SerializeField] private Sprite stageProgressNormalMonsterSprite;
    [SerializeField] private Sprite stageProgressBossMonsterSprite;

    private RectTransform root;
    private TMP_Text nicknameText;
    private TMP_Text pingText;
    private TMP_Text playerRedText;
    private TMP_Text playerGreenText;
    private TMP_Text playerBlueText;
    private TMP_Text playerScoreText;
    private TMP_Text legacyScoreText;
    private RectTransform stageMarker;
    private TMP_Text insuranceRedText;
    private TMP_Text insuranceGreenText;
    private TMP_Text insuranceBlueText;
    private TMP_Text monsterNameText;
    private TMP_Text monsterHpText;
    private TMP_Text monsterDescriptionText;
    private bool focusHidden;
    private bool authoredLayout;

    private static bool sceneHooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!sceneHooked)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            sceneHooked = true;
        }

        EnsureForScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForScene(scene);
    }

    public static void EnsureForScene(Scene scene)
    {
        if (!string.Equals(scene.name, TargetSceneName, System.StringComparison.Ordinal))
            return;

        if (FindAnyObjectByType<BattleHudUI>(FindObjectsInactive.Include) != null)
            return;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
            return;

        GameObject hudObject = new GameObject("Battle HUD");
        hudObject.transform.SetParent(canvas.transform, false);
        hudObject.AddComponent<BattleHudUI>();
    }

#if UNITY_EDITOR
    public void BuildPreviewInEditor()
    {
        EnsureUi();
        SetFocusHidden(false);
    }
#endif

    private void Awake()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EnsureUi();
            return;
        }
#endif

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureUi();
    }

#if UNITY_EDITOR
    private void OnEnable()
    {
        if (Application.isPlaying)
            return;

        EnsureUi();
    }
#endif

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (focusHidden)
            return;

        Refresh();
    }

    public void SetFocusHidden(bool hidden)
    {
        focusHidden = hidden;

        if (root != null)
            root.gameObject.SetActive(!hidden);
    }

    private void EnsureUi()
    {
        if (root != null)
            return;

        root = GetComponent<RectTransform>();
        if (root == null)
            root = gameObject.AddComponent<RectTransform>();

        if (root.childCount > 0)
        {
            authoredLayout = true;
            CacheAuthoredUi();
            return;
        }

        authoredLayout = false;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        root.SetAsFirstSibling();
        CleanupGeneratedDuplicates(root);

        BuildPlayerPanel();
        BuildStagePanel();
        BuildInsurancePanel();
        BuildBagPanel();
        BuildMonsterInfoPanel();
    }

    private void BuildPlayerPanel()
    {
        RectTransform panel = GetOrCreatePanel("Player Status", root);
        PlaceTopLeft(panel, new Vector2(12f, -34f), playerPanelSize);
        CleanupGeneratedDuplicates(panel);

        nicknameText = GetOrCreateText("Nickname", panel, "Player", 22f, TextAlignmentOptions.Center);
        SetCell(nicknameText.rectTransform, 0f, 0.5f, 0.68f, 1f, 0f, 0f, -2f, -2f);

        pingText = GetOrCreateText("Ping", panel, "-ms", 20f, TextAlignmentOptions.Center);
        SetCell(pingText.rectTransform, 0.68f, 0.5f, 1f, 1f, 2f, 0f, 0f, -2f);

        DisableDirectChild(panel, "Crystals");
        DisableDirectChild(panel, "Score");

        playerRedText = GetOrCreateText("Red Crystal Count", panel, "0", 20f, TextAlignmentOptions.Center);
        SetCell(playerRedText.rectTransform, 0f, 0f, 0.22f, 0.5f, 0f, 2f, -2f, 0f);

        playerGreenText = GetOrCreateText("Green Crystal Count", panel, "0", 20f, TextAlignmentOptions.Center);
        SetCell(playerGreenText.rectTransform, 0.22f, 0f, 0.45f, 0.5f, 2f, 2f, -2f, 0f);

        playerBlueText = GetOrCreateText("Blue Crystal Count", panel, "0", 20f, TextAlignmentOptions.Center);
        SetCell(playerBlueText.rectTransform, 0.45f, 0f, 0.68f, 0.5f, 2f, 2f, -2f, 0f);

        playerScoreText = GetOrCreateText("Score Count", panel, "0", 20f, TextAlignmentOptions.Center);
        SetCell(playerScoreText.rectTransform, 0.68f, 0f, 1f, 0.5f, 2f, 2f, 0f, 0f);

        DisableDirectChildren(panel, "Divider");
        CreateDivider(panel, new Vector2(0.68f, 0f), new Vector2(0.68f, 1f), 2f);
        CreateDivider(panel, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), 2f);
    }

    private void BuildStagePanel()
    {
        RectTransform stageRoot = GetOrCreateRect("Stage Route", root);
        stageRoot.anchorMin = new Vector2(0.5f, 1f);
        stageRoot.anchorMax = new Vector2(0.5f, 1f);
        stageRoot.pivot = new Vector2(0.5f, 1f);
        stageRoot.anchoredPosition = new Vector2(0f, -28f);
        stageRoot.sizeDelta = new Vector2(530f, 110f);

        CleanupGeneratedDuplicates(stageRoot);
        DisableDirectChildren(stageRoot, "Divider");
        CreateDivider(stageRoot, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.32f), 2f);

        string[] labels = { "1-1", "1-2", "1-3", "1-Boss" };
        for (int i = 0; i < labels.Length; i++)
        {
            float x = GetStageNodeAnchorX(i);

            RectTransform node = GetOrCreatePanel($"Stage Node {i + 1}", stageRoot);
            node.anchorMin = new Vector2(x, 0.08f);
            node.anchorMax = new Vector2(x, 0.08f);
            node.pivot = new Vector2(0.5f, 0.5f);
            node.anchoredPosition = Vector2.zero;
            node.sizeDelta = new Vector2(34f, 34f);

            RectTransform label = GetOrCreatePanel($"Stage Label {i + 1}", stageRoot);
            label.anchorMin = new Vector2(x, 0.67f);
            label.anchorMax = new Vector2(x, 0.67f);
            label.pivot = new Vector2(0.5f, 0.5f);
            label.anchoredPosition = Vector2.zero;
            label.sizeDelta = new Vector2(i == labels.Length - 1 ? 72f : 58f, 44f);
            ApplyStageLabelSprite(label, i == labels.Length - 1);

            TMP_Text labelText = GetOrCreateText("Text", label, labels[i], 20f, TextAlignmentOptions.Center);
            Stretch(labelText.rectTransform, 0f);
        }

        HideExtraStageRouteEntries(stageRoot, labels.Length);
        stageMarker = CreateStageMarker(stageRoot);
        SetStageMarkerIndex(0);
    }

    private void BuildInsurancePanel()
    {
        RectTransform panel = GetOrCreatePanel("Dropped Crystals", root);
        PlaceTopRight(panel, new Vector2(-34f, -46f), insurancePanelSize);
        ApplyInsuranceJarSprite(panel);
        CleanupGeneratedDuplicates(panel);
        DisableDirectChild(panel, "Insurance");

        insuranceRedText = GetOrCreateText("Red Crystal Count", panel, "0", 30f, TextAlignmentOptions.Center);
        SetInspectorCell(insuranceRedText.rectTransform, 0.12f, 0f, 0.36f, 1f, 0f, 5.2f, 0f, -5.2f);

        insuranceGreenText = GetOrCreateText("Green Crystal Count", panel, "0", 30f, TextAlignmentOptions.Center);
        SetInspectorCell(insuranceGreenText.rectTransform, 0.38f, 0f, 0.62f, 1f, 0f, 5.2f, 0f, -5.2f);

        insuranceBlueText = GetOrCreateText("Blue Crystal Count", panel, "0", 30f, TextAlignmentOptions.Center);
        SetInspectorCell(insuranceBlueText.rectTransform, 0.64f, 0f, 0.88f, 1f, 0f, 5.2f, 0f, -5.2f);
    }

    private void BuildBagPanel()
    {
        RectTransform panel = GetOrCreatePanel("Bag Placeholder", root);
        PlaceBottomLeft(panel, new Vector2(22f, 20f), bagPanelSize);
        ApplyItemBagSprite(panel);
    }

    private void BuildMonsterInfoPanel()
    {
        RectTransform panel = GetOrCreatePanel("Monster Info", root);
        PlaceBottomRight(panel, new Vector2(0f, 0.00006103516f), monsterInfoSize);
        ApplyMonsterBookSprite(panel);
        CleanupGeneratedDuplicates(panel);

        RectTransform portrait = GetOrCreatePanel("Portrait", panel);
        SetInspectorCell(portrait, 0.06f, 0.7f, 0.32f, 0.94f, 67.92637f, -45.06004f, -68.25246f, 67.23775f);
        SetRectTransformExtras(portrait, -0.131f, 0.123f, 16.912f, 0.0117f);

        monsterNameText = GetOrCreateText("Name", panel, "Monster", 21f, TextAlignmentOptions.Center);
        SetInspectorCell(monsterNameText.rectTransform, 0.38f, 0.82f, 0.94f, 0.94f, -9f, -30.00002f, 9f, 29.99998f);
        SetRectTransformExtras(monsterNameText.rectTransform, -0.046f, 0.052f, 6.508f, 0f);

        monsterHpText = GetOrCreateText("HP", panel, "     -", 20f, TextAlignmentOptions.Center);
        SetInspectorCell(monsterHpText.rectTransform, 0.38f, 0.7f, 0.94f, 0.82f, -31f, -38f, 31f, 38f);
        SetRectTransformExtras(monsterHpText.rectTransform, -0.103f, 0.103f, 13.669f, 0f);

        for (int i = 0; i < 3; i++)
        {
            RectTransform slot = GetOrCreatePanel($"Monster Slot {i + 1}", panel);
            if (i == 0)
            {
                SetInspectorCell(slot, 0.12f, 0.46f, 0.34f, 0.64f, 49.58157f, -28.557f, -41.90757f, 47.375f);
                SetRectTransformExtras(slot, -0.136f, 0.127f, 17.511f, 0.0105f);
            }
            else if (i == 1)
            {
                SetInspectorCell(slot, 0.41f, 0.46f, 0.63f, 0.64f, 22.95735f, -2.8043f, -13.73685f, 22.4037f);
                SetRectTransformExtras(slot, -0.097f, 0.098f, 12.939f, -0.2461f);
            }
            else
            {
                SetInspectorCell(slot, 0.7f, 0.46f, 0.92f, 0.64f, -5.258658f, 13.04385f, 25.77296f, 8.117455f);
                SetRectTransformExtras(slot, -0.049f, 0.055f, 6.863f, -0.9884f);
            }
        }

        monsterDescriptionText = GetOrCreateText("Description", panel, string.Empty, 17f, TextAlignmentOptions.TopLeft);
        SetInspectorCell(monsterDescriptionText.rectTransform, 0.12f, 0.08f, 0.88f, 0.38f, 70.2116f, 6.4796f, -25.4238f, -6.4796f);
        SetRectTransformExtras(monsterDescriptionText.rectTransform, -0.095f, -3.983f, 9.266f, 3.7792f);
    }

    private void Refresh()
    {
        if (authoredLayout)
            CacheAuthoredUi();

        NetworkPlayerState player = null;
        NetworkPlayerRegistry.TryGetLocalPlayer(out player);

        string nickname = GetNickname(player);
        int red = player != null ? player.CrystalRed.Value : 0;
        int blue = player != null ? player.CrystalBlue.Value : 0;
        int green = player != null ? player.CrystalGreen.Value : 0;
        int score = red + blue + green;

        SetText(nicknameText, nickname);
        SetText(pingText, GetPingText());
        SetText(playerRedText, red.ToString());
        SetText(playerGreenText, green.ToString());
        SetText(playerBlueText, blue.ToString());
        SetText(playerScoreText != null ? playerScoreText : legacyScoreText, score.ToString());

        NetworkSessionState session = NetworkSessionState.Instance;
        int insuranceRed = session != null ? session.InsuranceRed.Value : 0;
        int insuranceBlue = session != null ? session.InsuranceBlue.Value : 0;
        int insuranceGreen = session != null ? session.InsuranceGreen.Value : 0;

        SetStageMarkerIndex(GetCurrentStageIndex(session));
        SetText(insuranceRedText, insuranceRed.ToString());
        SetText(insuranceGreenText, insuranceGreen.ToString());
        SetText(insuranceBlueText, insuranceBlue.ToString());

        MonsterData monster = BattleFlowController.Instance != null ? BattleFlowController.Instance.CurrentMonsterData : null;
        SetText(
            monsterNameText,
            monster != null && !string.IsNullOrWhiteSpace(monster.monster_name)
                ? monster.monster_name
                : "Monster");

        SetText(monsterHpText, GetMonsterHpText());
        SetText(monsterDescriptionText, string.Empty);
    }

    private void CacheAuthoredUi()
    {
        nicknameText = FindText("Nickname");
        pingText = FindText("Ping");
        playerRedText = FindText("Red Crystal Count");
        playerGreenText = FindText("Green Crystal Count");
        playerBlueText = FindText("Blue Crystal Count");
        playerScoreText = FindText("Score Count");
        legacyScoreText = FindText("Score");

        Transform droppedCrystals = FindChildRecursive(root, "Dropped Crystals");
        insuranceRedText = FindText("Red Crystal Count", droppedCrystals);
        insuranceGreenText = FindText("Green Crystal Count", droppedCrystals);
        insuranceBlueText = FindText("Blue Crystal Count", droppedCrystals);

        stageMarker = FindRect("Current Stage Marker");
        ApplyStageProgressIndicatorSprite(stageMarker);
        ApplyStageProgressBarSprite(FindRect("Stage Route Line"));
        ConfigureAuthoredStageRoute();
        monsterNameText = FindText("Name");
        monsterHpText = FindText("HP");
        monsterDescriptionText = FindText("Description");
    }

    private TMP_Text FindText(string objectName)
    {
        return FindText(objectName, root);
    }

    private TMP_Text FindText(string objectName, Transform searchRoot)
    {
        Transform child = FindChildRecursive(searchRoot, objectName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private RectTransform FindRect(string objectName)
    {
        Transform child = FindChildRecursive(root, objectName);
        return child != null ? child.GetComponent<RectTransform>() : null;
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

    private void SetText(TMP_Text text, string value)
    {
        if (text != null)
            text.text = value;
    }

    private string GetNickname(NetworkPlayerState player)
    {
        if (player != null && !string.IsNullOrWhiteSpace(player.DisplayNickname.Value.ToString()))
            return player.DisplayNickname.Value.ToString();

        if (GameLocalSession.Instance != null && !string.IsNullOrWhiteSpace(GameLocalSession.Instance.LocalNickname))
            return GameLocalSession.Instance.LocalNickname;

        return "Akaneru";
    }

    private string GetPingText()
    {
        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsListening)
            return "-ms";

        return manager.IsServer ? "0ms" : "-ms";
    }

    private string GetMonsterHpText()
    {
        MonsterSpawner spawner = FindAnyObjectByType<MonsterSpawner>();
        if (spawner == null || !spawner.TryGetCurrentMonsterState(out MonsterBattleState state))
            return "     -";

        return $"     {state.CurrentHp}";
    }

    private RectTransform CreateStageMarker(RectTransform parent)
    {
        RectTransform marker = CreateRect("Current Stage Marker", parent);
        marker.pivot = new Vector2(0.5f, 0f);
        marker.sizeDelta = new Vector2(38f, 34f);
        ApplyStageProgressIndicatorSprite(marker);
        marker.SetAsLastSibling();

        return marker;
    }

    private void SetStageMarkerIndex(int stageIndex)
    {
        if (stageMarker == null)
            return;

        int clampedIndex = Mathf.Clamp(stageIndex, 0, StageEncounterCount - 1);
        RectTransform stageLabel = FindRect($"Stage Label {clampedIndex + 1}");
        if (stageLabel != null && stageLabel.parent == stageMarker.parent)
        {
            Vector2 markerPosition = stageMarker.anchoredPosition;
            markerPosition.x = stageLabel.anchoredPosition.x;
            stageMarker.anchoredPosition = markerPosition;
        }
        else
        {
            float x = GetStageNodeAnchorX(clampedIndex);
            stageMarker.anchorMin = new Vector2(x, stageMarker.anchorMin.y);
            stageMarker.anchorMax = new Vector2(x, stageMarker.anchorMax.y);
            stageMarker.anchoredPosition = new Vector2(0f, stageMarker.anchoredPosition.y);
        }

        stageMarker.SetAsLastSibling();
    }

    private int GetCurrentStageIndex(NetworkSessionState session)
    {
        int currentStage = session != null ? session.CurrentStage.Value : 0;
        if (currentStage <= 0 && BattleFlowController.Instance != null)
            currentStage = BattleFlowController.Instance.CurrentEncounterNumber;

        if (currentStage <= 0)
            return 0;

        return Mathf.Clamp(currentStage - 1, 0, StageEncounterCount - 1);
    }

    private float GetStageNodeAnchorX(int stageIndex)
    {
        return Mathf.Lerp(0.08f, 0.92f, stageIndex / (float)(StageEncounterCount - 1));
    }

    private void ConfigureAuthoredStageRoute()
    {
        Transform stageRoot = FindChildRecursive(root, "Stage Route");
        if (stageRoot == null)
            return;

        string[] labels = { "1-1", "1-2", "1-3", "1-Boss" };
        for (int i = 0; i < labels.Length; i++)
        {
            ConfigureAuthoredStageLabel(stageRoot, $"Stage Label {i + 1}", labels[i], i == labels.Length - 1);
            SetStageRouteEntryActive(stageRoot, $"Stage Node {i + 1}", true);
        }

        HideExtraStageRouteEntries(stageRoot, labels.Length);
    }

    private void ConfigureAuthoredStageLabel(Transform stageRoot, string entryName, string labelText, bool isBossLabel)
    {
        Transform entry = FindChildRecursive(stageRoot, entryName);
        if (entry == null)
            return;

        entry.gameObject.SetActive(true);
        RectTransform rect = entry.GetComponent<RectTransform>();
        ApplyStageLabelSprite(rect, isBossLabel);
        SetText(entry.GetComponentInChildren<TMP_Text>(true), labelText);
    }

    private void HideExtraStageRouteEntries(Transform stageRoot, int visibleEntryCount)
    {
        for (int i = visibleEntryCount + 1; i <= 5; i++)
        {
            SetStageRouteEntryActive(stageRoot, $"Stage Node {i}", false);
            SetStageRouteEntryActive(stageRoot, $"Stage Label {i}", false);
        }
    }

    private void SetStageRouteEntryActive(Transform stageRoot, string entryName, bool active)
    {
        Transform entry = FindChildRecursive(stageRoot, entryName);
        if (entry != null)
            entry.gameObject.SetActive(active);
    }

    private RectTransform CreatePanel(string name, Transform parent)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = panelColor;
        image.raycastTarget = false;

        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = lineColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return rect;
    }

    private RectTransform GetOrCreatePanel(string name, Transform parent)
    {
        RectTransform rect = GetOrCreateRect(name, parent);
        Image image = rect.GetComponent<Image>();
        if (image == null)
            image = rect.gameObject.AddComponent<Image>();

        image.color = panelColor;
        image.raycastTarget = false;

        Outline outline = rect.GetComponent<Outline>();
        if (outline == null)
            outline = rect.gameObject.AddComponent<Outline>();

        outline.effectColor = lineColor;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return rect;
    }

    private TMP_Text CreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = textColor;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    private TMP_Text GetOrCreateText(string name, Transform parent, string text, float fontSize, TextAlignmentOptions alignment)
    {
        RectTransform rect = GetOrCreateRect(name, parent);
        TMP_Text tmp = rect.GetComponent<TMP_Text>();
        if (tmp == null)
            tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = textColor;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        rect.gameObject.SetActive(true);
        return tmp;
    }

    private RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    private RectTransform GetOrCreateRect(string name, Transform parent)
    {
        Transform existing = FindDirectChild(parent, name);
        if (existing != null)
            return existing.GetComponent<RectTransform>() ?? existing.gameObject.AddComponent<RectTransform>();

        return CreateRect(name, parent);
    }

    private Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name == childName)
                return child;
        }

        return null;
    }

    private void DisableDirectChild(Transform parent, string childName)
    {
        Transform child = FindDirectChild(parent, childName);
        if (child != null)
            child.gameObject.SetActive(false);
    }

    private void DisableDirectChildren(Transform parent, string childName)
    {
        if (parent == null)
            return;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child != null && child.name == childName)
                child.gameObject.SetActive(false);
        }
    }

    private void CleanupGeneratedDuplicates(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
                continue;

            bool duplicate = false;
            for (int j = 0; j < i; j++)
            {
                Transform earlier = parent.GetChild(j);
                if (earlier != null && earlier.name == child.name)
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
                continue;

            DestroyGeneratedObject(child.gameObject);
        }
    }

    private void DestroyGeneratedObject(GameObject go)
    {
        if (go == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(go);
        else
#endif
            Destroy(go);
    }

    private void CreateDivider(RectTransform parent, Vector2 anchorStart, Vector2 anchorEnd, float thickness)
    {
        string dividerName = Mathf.Approximately(anchorStart.x, anchorEnd.x)
            ? "Vertical Divider"
            : "Horizontal Divider";

        bool isStageRouteLine = parent != null && string.Equals(parent.name, "Stage Route", System.StringComparison.Ordinal);
        if (isStageRouteLine)
            dividerName = "Stage Route Line";

        RectTransform divider = GetOrCreateRect(dividerName, parent);
        divider.anchorMin = anchorStart;
        divider.anchorMax = anchorEnd;
        divider.offsetMin = Vector2.zero;
        divider.offsetMax = Vector2.zero;

        Image image = divider.GetComponent<Image>();
        if (image == null)
            image = divider.gameObject.AddComponent<Image>();

        if (isStageRouteLine)
        {
            ApplyStageProgressBarSprite(divider);
        }
        else
        {
            image.color = lineColor;
            image.raycastTarget = false;
        }

        if (Mathf.Approximately(anchorStart.x, anchorEnd.x))
            divider.sizeDelta = new Vector2(thickness, 0f);
        else
            divider.sizeDelta = new Vector2(0f, thickness);
    }

    private void PlaceTopLeft(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private void PlaceTopRight(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private void PlaceBottomLeft(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private void PlaceBottomRight(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private void SetCell(RectTransform rect, float minX, float minY, float maxX, float maxY, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }

    private void SetInspectorCell(RectTransform rect, float minX, float minY, float maxX, float maxY, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(minX, minY);
        rect.anchorMax = new Vector2(maxX, maxY);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private void SetRectTransformExtras(RectTransform rect, float rotationX, float rotationY, float rotationZ, float posZ)
    {
        rect.localEulerAngles = new Vector3(rotationX, rotationY, rotationZ);
        Vector3 position = rect.localPosition;
        position.z = posZ;
        rect.localPosition = position;
    }

    private void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private void ApplyInsuranceJarSprite(RectTransform panel)
    {
        Image image = panel.GetComponent<Image>();
        if (image == null)
            return;

        Sprite sprite = GetInsuranceJarSprite();
        if (sprite == null)
            return;

        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;

        Outline outline = panel.GetComponent<Outline>();
        if (outline != null)
            outline.enabled = false;
    }

    private Sprite GetInsuranceJarSprite()
    {
        if (insuranceJarSprite != null)
            return insuranceJarSprite;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/souce/main_sources/main_insuranceJar.png");
#else
        return null;
#endif
    }

    private void ApplyItemBagSprite(RectTransform panel)
    {
        Image image = panel.GetComponent<Image>();
        if (image == null)
            return;

        Sprite sprite = GetItemBagSprite();
        if (sprite == null)
            return;

        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;

        Outline outline = panel.GetComponent<Outline>();
        if (outline != null)
            outline.enabled = false;
    }

    private Sprite GetItemBagSprite()
    {
        if (itemBagSprite != null)
            return itemBagSprite;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/souce/main_sources/main_itemBag.png");
#else
        return null;
#endif
    }

    private void ApplyMonsterBookSprite(RectTransform panel)
    {
        Image image = panel.GetComponent<Image>();
        if (image == null)
            return;

        Sprite sprite = GetMonsterBookSprite();
        if (sprite == null)
            return;

        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;

        Outline outline = panel.GetComponent<Outline>();
        if (outline != null)
            outline.enabled = false;
    }

    private void ApplyStageProgressIndicatorSprite(RectTransform marker)
    {
        if (marker == null)
            return;

        TMP_Text markerText = marker.GetComponent<TMP_Text>();
        if (markerText != null)
            DestroyGeneratedComponent(markerText);

        Image image = marker.GetComponent<Image>();
        if (image == null)
            image = marker.gameObject.AddComponent<Image>();

        Sprite sprite = GetStageProgressIndicatorSprite();
        image.sprite = sprite;
        image.color = sprite != null ? Color.white : Color.clear;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private void ApplyStageProgressBarSprite(RectTransform bar)
    {
        if (bar == null)
            return;

        Image image = bar.GetComponent<Image>();
        if (image == null)
            image = bar.gameObject.AddComponent<Image>();

        Sprite sprite = GetStageProgressBarSprite();
        image.sprite = sprite;
        image.color = sprite != null ? Color.white : lineColor;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;
    }

    private void ApplyStageLabelSprite(RectTransform label, bool isBossLabel)
    {
        if (label == null)
            return;

        Image image = label.GetComponent<Image>();
        if (image == null)
            image = label.gameObject.AddComponent<Image>();

        Sprite sprite = isBossLabel ? GetStageProgressBossMonsterSprite() : GetStageProgressNormalMonsterSprite();
        image.sprite = sprite;
        image.color = sprite != null ? Color.white : panelColor;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.raycastTarget = false;

        Outline outline = label.GetComponent<Outline>();
        if (outline != null)
            outline.enabled = sprite == null;
    }

    private void DestroyGeneratedComponent(Component component)
    {
        if (component == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(component);
        else
#endif
            Destroy(component);
    }

    private Sprite GetMonsterBookSprite()
    {
        if (monsterBookSprite != null)
            return monsterBookSprite;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/souce/main_sources/main_monsterBook.png");
#else
        return null;
#endif
    }

    private Sprite GetStageProgressIndicatorSprite()
    {
        if (stageProgressIndicatorSprite != null)
            return stageProgressIndicatorSprite;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/souce/main_sources/main_stageProgress_indicator.png");
#else
        return null;
#endif
    }

    private Sprite GetStageProgressBarSprite()
    {
        if (stageProgressBarSprite != null)
            return stageProgressBarSprite;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/souce/main_sources/main_stageProgress_bar.png");
#else
        return null;
#endif
    }

    private Sprite GetStageProgressNormalMonsterSprite()
    {
        if (stageProgressNormalMonsterSprite != null)
            return stageProgressNormalMonsterSprite;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/souce/main_sources/main_main_stageProgress_normalMontser.png");
#else
        return null;
#endif
    }

    private Sprite GetStageProgressBossMonsterSprite()
    {
        if (stageProgressBossMonsterSprite != null)
            return stageProgressBossMonsterSprite;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/souce/main_sources/main_main_stageProgress_bossMontser.png");
#else
        return null;
#endif
    }
}
