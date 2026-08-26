using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum BattleStandingAction
{
    Idle,
    Hit,
    Victory,
    Defeat
}

public class BattlePlayerStanding : MonoBehaviour
{
    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Animator Trigger Names")]
    [SerializeField] private string idleTrigger = "Idle";
    [SerializeField] private string hitTrigger = "Hit";
    [SerializeField] private string victoryTrigger = "Victory";
    [SerializeField] private string defeatTrigger = "Defeat";

    [Header("추가 테스트용 Trigger들")]
    [SerializeField] private string[] extraTestTriggers;

    [Header("View - Nickname")]
    [SerializeField] private TMP_Text nicknameText;

    [Header("View - Current Card")]
    [SerializeField] private SpriteRenderer currentCardRenderer;
    [SerializeField] private Sprite[] numberCardSprites = new Sprite[6];
    [SerializeField] private Sprite cardFrontSprite;
    [SerializeField] private Sprite cardBackSprite;
    [SerializeField] private Sprite cardOutlineSprite;
    [SerializeField] private SpriteRenderer currentCardOutlineRenderer;
    [SerializeField] private TMP_Text currentCardValueLabel;

    [Header("View - Self Indicator")]
    [SerializeField] private GameObject selfIndicatorObject;
    [SerializeField] private SpriteRenderer selfIndicatorRenderer;

    [Header("Focus Click")]
    [SerializeField] private bool enableFocusClick = true;
    [SerializeField] private bool autoAddClickCollider = true;

    public int PlayerIndex { get; private set; } = -1;
    public string PlayerKey { get; private set; } = string.Empty;
    public string RawNickname { get; private set; } = string.Empty;
    public bool IsHostPlayer { get; private set; }
    public bool IsLocalPlayer { get; private set; }
    public int CurrentSubmittedCardValue { get; private set; } = -1;

    private bool initialized;
    private bool selfIndicatorForceHidden;
    private bool currentSubmittedCardFaceDown;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (selfIndicatorObject == null && selfIndicatorRenderer != null)
            selfIndicatorObject = selfIndicatorRenderer.gameObject;

        EnsureCurrentCardArtworkLayers();
        RefreshNicknameView();
        RefreshCurrentCardView();
        RefreshSelfIndicatorVisibility();
        EnsureClickCollider();
    }

    private void OnMouseUpAsButton()
    {
        if (!enableFocusClick)
            return;

        if (BattleFlowController.Instance != null && BattleFlowController.Instance.TryRequestKnightShiftBlameTarget(PlayerIndex))
            return;

        if (BattleFlowController.Instance != null && BattleFlowController.Instance.TryRequestDefenderTarget(PlayerIndex))
            return;

        BattleStandingFocusController.GetOrCreate().ToggleFocus(this);
    }
    private void OnEnable()
    {
        if (initialized)
            BattlePlayerStandingRegistry.Register(this);
    }

    private void OnDisable()
    {
        if (initialized)
            BattlePlayerStandingRegistry.Unregister(this);
    }

    private void OnDestroy()
    {
        if (initialized)
            BattlePlayerStandingRegistry.Unregister(this);
    }

    public void Initialize(int playerIndex)
    {
        Initialize(playerIndex, string.Empty, false, false);
    }

    public void Initialize(int playerIndex, string nickname, bool isHostPlayer, bool isLocalPlayer)
    {
        PlayerIndex = playerIndex;
        PlayerKey = BattlePlayerStandingRegistry.ToKey(playerIndex);
        RawNickname = nickname ?? string.Empty;
        IsHostPlayer = isHostPlayer;
        IsLocalPlayer = isLocalPlayer;

        initialized = true;

        EnsureCurrentCardArtworkLayers();
        RefreshNicknameView();
        RefreshCurrentCardView();
        RefreshSelfIndicatorVisibility();
        EnsureClickCollider();

        BattlePlayerStandingRegistry.Register(this);
    }

    public void SetNickname(string nickname, bool isHostPlayer)
    {
        RawNickname = nickname ?? string.Empty;
        IsHostPlayer = isHostPlayer;
        RefreshNicknameView();
    }

    public void SetLocalPlayer(bool isLocalPlayer)
    {
        IsLocalPlayer = isLocalPlayer;
        RefreshCurrentCardView();
        RefreshSelfIndicatorVisibility();
    }

    public void SetSubmittedCard(int cardValue)
    {
        SetSubmittedCard(cardValue, false);
    }

    public void SetSubmittedCard(int cardValue, bool faceDownForLocalPlayer)
    {
        if (cardValue < 1 || cardValue > 6)
        {
            Debug.LogWarning($"{name}: 잘못된 카드 값입니다. ({cardValue})");
            return;
        }

        CurrentSubmittedCardValue = cardValue;
        currentSubmittedCardFaceDown = faceDownForLocalPlayer;
        RefreshCurrentCardView();
        RefreshSelfIndicatorVisibility();
    }

    public void ClearSubmittedCard()
    {
        CurrentSubmittedCardValue = -1;
        currentSubmittedCardFaceDown = false;
        RefreshCurrentCardView();
        RefreshSelfIndicatorVisibility();
    }

    public void HideSelfIndicator()
    {
        selfIndicatorForceHidden = true;
        RefreshSelfIndicatorVisibility();
    }

    public void ShowSelfIndicator()
    {
        selfIndicatorForceHidden = false;
        RefreshSelfIndicatorVisibility();
    }
    public void SetSelfIndicatorForceHidden(bool hidden)
    {
        selfIndicatorForceHidden = hidden;
        RefreshSelfIndicatorVisibility();
    }

    public Sprite GetNumberCardSprite(int cardValue)
    {
        int spriteIndex = cardValue - 1;
        if (numberCardSprites == null ||
            spriteIndex < 0 ||
            spriteIndex >= numberCardSprites.Length)
        {
            return null;
        }

        Sprite sharedFront = GetSharedCardFrontSprite();
        return sharedFront != null ? sharedFront : numberCardSprites[spriteIndex];
    }

    public void Play(BattleStandingAction action)
    {
        switch (action)
        {
            case BattleStandingAction.Idle:
                SetTrigger(idleTrigger);
                break;

            case BattleStandingAction.Hit:
                SetTrigger(hitTrigger);
                break;

            case BattleStandingAction.Victory:
                SetTrigger(victoryTrigger);
                break;

            case BattleStandingAction.Defeat:
                SetTrigger(defeatTrigger);
                break;
        }
    }

    public void PlayIdle() => SetTrigger(idleTrigger);
    public void PlayHit() => SetTrigger(hitTrigger);
    public void PlayVictory() => SetTrigger(victoryTrigger);
    public void PlayDefeat() => SetTrigger(defeatTrigger);

    public void PlayCustom(string triggerName)
    {
        SetTrigger(triggerName);
    }

    public string[] GetAvailableAnimationNames()
    {
        List<string> result = new List<string>();

        AddIfValid(result, idleTrigger);
        AddIfValid(result, hitTrigger);
        AddIfValid(result, victoryTrigger);
        AddIfValid(result, defeatTrigger);

        if (extraTestTriggers != null)
        {
            for (int i = 0; i < extraTestTriggers.Length; i++)
                AddIfValid(result, extraTestTriggers[i]);
        }

        return result.ToArray();
    }

    public bool TryPlayByName(string animationName)
    {
        if (string.IsNullOrWhiteSpace(animationName))
            return false;

        string[] available = GetAvailableAnimationNames();
        for (int i = 0; i < available.Length; i++)
        {
            if (available[i] == animationName)
            {
                SetTrigger(animationName);
                return true;
            }
        }

        Debug.LogWarning($"{name}: '{animationName}' 은(는) 등록된 테스트용 애니메이션이 아닙니다.");
        return false;
    }

    private void RefreshNicknameView()
    {
        if (nicknameText == null)
            return;

        string visibleName = RawNickname ?? string.Empty;

        if (IsHostPlayer && !string.IsNullOrEmpty(visibleName))
            visibleName = "*" + visibleName;

        nicknameText.text = visibleName;
    }

    private void RefreshCurrentCardView()
    {
        if (currentCardRenderer == null)
            return;

        EnsureCurrentCardArtworkLayers();

        if (CurrentSubmittedCardValue < 1 || CurrentSubmittedCardValue > 6)
        {
            currentCardRenderer.sprite = null;
            SetSpriteRendererAlpha(currentCardRenderer, 0f);
            SetSpriteRendererAlpha(currentCardOutlineRenderer, 0f);
            SetCurrentCardValueLabelVisible(false);
            return;
        }

        bool showFaceValue = IsLocalPlayer && !currentSubmittedCardFaceDown;
        Sprite targetSprite = showFaceValue ? GetSharedCardFrontSprite() : GetSharedCardBackSprite();

        currentCardRenderer.sprite = targetSprite;
        SetSpriteRendererAlpha(currentCardRenderer, targetSprite != null ? 1f : 0f);

        if (currentCardOutlineRenderer != null)
        {
            currentCardOutlineRenderer.sprite = GetSharedCardOutlineSprite();
            SetSpriteRendererAlpha(currentCardOutlineRenderer, targetSprite != null && currentCardOutlineRenderer.sprite != null ? 1f : 0f);
        }

        SetCurrentCardValueLabelVisible(showFaceValue && targetSprite != null);
    }
    private void RefreshSelfIndicatorVisibility()
    {
        GameObject targetObject = selfIndicatorObject;

        if (targetObject == null && selfIndicatorRenderer != null)
            targetObject = selfIndicatorRenderer.gameObject;

        if (targetObject == null)
            return;

        bool hasSubmittedCard = CurrentSubmittedCardValue >= 1 && CurrentSubmittedCardValue <= 6;
        bool visible = IsLocalPlayer && !selfIndicatorForceHidden && !hasSubmittedCard;

        targetObject.SetActive(visible);
    }

    private void AddIfValid(List<string> list, string triggerName)
    {
        if (string.IsNullOrWhiteSpace(triggerName))
            return;

        if (!list.Contains(triggerName))
            list.Add(triggerName);
    }

    private void SetTrigger(string triggerName)
    {
        if (animator == null)
        {
            Debug.LogWarning($"{name}: Animator가 없습니다.");
            return;
        }

        if (string.IsNullOrWhiteSpace(triggerName))
        {
            Debug.LogWarning($"{name}: triggerName이 비어 있습니다.");
            return;
        }

        animator.SetTrigger(triggerName);
    }

    private void SetSpriteRendererAlpha(SpriteRenderer renderer, float alpha)
    {
        if (renderer == null)
            return;

        Color c = renderer.color;
        c.a = alpha;
        renderer.color = c;
    }
    private Sprite GetSharedCardFrontSprite()
    {
        CardDrawPopupController cardPopup = CardDrawPopupController.GetAny();
        if (cardPopup != null && cardPopup.CardFrontSprite != null)
            return cardPopup.CardFrontSprite;

        return cardFrontSprite;
    }

    private Sprite GetSharedCardBackSprite()
    {
        CardDrawPopupController cardPopup = CardDrawPopupController.GetAny();
        if (cardPopup != null && cardPopup.CardBackSprite != null)
            return cardPopup.CardBackSprite;

        return cardBackSprite;
    }

    private Sprite GetSharedCardOutlineSprite()
    {
        CardDrawPopupController cardPopup = CardDrawPopupController.GetAny();
        if (cardPopup != null && cardPopup.CardOutlineSprite != null)
            return cardPopup.CardOutlineSprite;

        return cardOutlineSprite;
    }

    private void EnsureCurrentCardArtworkLayers()
    {
        if (currentCardRenderer == null)
            return;

        if (currentCardOutlineRenderer == null)
        {
            Transform existingOutline = currentCardRenderer.transform.Find("Current Card Outline");
            if (existingOutline != null)
                currentCardOutlineRenderer = existingOutline.GetComponent<SpriteRenderer>();
        }

        if (currentCardOutlineRenderer == null)
        {
            GameObject outlineObject = new GameObject("Current Card Outline");
            outlineObject.transform.SetParent(currentCardRenderer.transform, false);
            outlineObject.transform.localPosition = Vector3.zero;
            currentCardOutlineRenderer = outlineObject.AddComponent<SpriteRenderer>();
        }

        if (currentCardOutlineRenderer != null)
        {
            currentCardOutlineRenderer.sprite = GetSharedCardOutlineSprite();
            currentCardOutlineRenderer.sortingLayerID = currentCardRenderer.sortingLayerID;
            currentCardOutlineRenderer.sortingOrder = currentCardRenderer.sortingOrder + 1;
        }

        if (currentCardValueLabel == null)
        {
            Transform existingLabel = currentCardRenderer.transform.Find("Current Card Value Label");
            if (existingLabel != null)
                currentCardValueLabel = existingLabel.GetComponent<TMP_Text>();
        }

        if (currentCardValueLabel == null)
        {
            GameObject labelObject = new GameObject("Current Card Value Label");
            labelObject.transform.SetParent(currentCardRenderer.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            currentCardValueLabel = labelObject.AddComponent<TextMeshPro>();
        }

        if (currentCardValueLabel != null)
        {
            currentCardValueLabel.alignment = TextAlignmentOptions.Center;
            currentCardValueLabel.color = new Color(0.08f, 0.075f, 0.06f, 1f);
            currentCardValueLabel.fontSize = 3f;
            currentCardValueLabel.fontStyle = FontStyles.Bold;
            currentCardValueLabel.raycastTarget = false;

            MeshRenderer labelRenderer = currentCardValueLabel.GetComponent<MeshRenderer>();
            if (labelRenderer != null)
            {
                labelRenderer.sortingLayerID = currentCardRenderer.sortingLayerID;
                labelRenderer.sortingOrder = currentCardRenderer.sortingOrder + 2;
            }
        }
    }

    private void SetCurrentCardValueLabelVisible(bool visible)
    {
        if (currentCardValueLabel == null)
            return;

        currentCardValueLabel.text = visible ? CurrentSubmittedCardValue.ToString() : string.Empty;
        currentCardValueLabel.gameObject.SetActive(visible);
    }
    private void EnsureClickCollider()
    {
        if (!autoAddClickCollider)
            return;

        if (GetComponent<Collider2D>() != null)
            return;

        SpriteRenderer mainRenderer = GetComponent<SpriteRenderer>();
        if (mainRenderer == null || mainRenderer.sprite == null)
            return;

        BoxCollider2D clickCollider = gameObject.AddComponent<BoxCollider2D>();
        clickCollider.isTrigger = true;
        clickCollider.size = mainRenderer.sprite.bounds.size;
        clickCollider.offset = mainRenderer.sprite.bounds.center;
    }
}
