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
    [SerializeField] private Sprite cardBackSprite;

    [Header("View - Self Indicator")]
    [SerializeField] private GameObject selfIndicatorObject;
    [SerializeField] private SpriteRenderer selfIndicatorRenderer;

    public int PlayerIndex { get; private set; } = -1;
    public string PlayerKey { get; private set; } = string.Empty;
    public string RawNickname { get; private set; } = string.Empty;
    public bool IsHostPlayer { get; private set; }
    public bool IsLocalPlayer { get; private set; }
    public int CurrentSubmittedCardValue { get; private set; } = -1;

    private bool initialized;
    private bool selfIndicatorForceHidden;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (selfIndicatorObject == null && selfIndicatorRenderer != null)
            selfIndicatorObject = selfIndicatorRenderer.gameObject;

        RefreshNicknameView();
        RefreshCurrentCardView();
        RefreshSelfIndicatorVisibility();
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

        RefreshNicknameView();
        RefreshCurrentCardView();
        RefreshSelfIndicatorVisibility();

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
        if (cardValue < 1 || cardValue > 6)
        {
            Debug.LogWarning($"{name}: 잘못된 카드 값입니다. ({cardValue})");
            return;
        }

        CurrentSubmittedCardValue = cardValue;
        RefreshCurrentCardView();
        RefreshSelfIndicatorVisibility();
    }

    public void ClearSubmittedCard()
    {
        CurrentSubmittedCardValue = -1;
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

        if (CurrentSubmittedCardValue < 1 || CurrentSubmittedCardValue > 6)
        {
            currentCardRenderer.sprite = null;
            SetSpriteRendererAlpha(currentCardRenderer, 0f);
            return;
        }

        Sprite targetSprite = null;

        if (IsLocalPlayer)
        {
            int spriteIndex = CurrentSubmittedCardValue - 1;
            if (numberCardSprites != null &&
                spriteIndex >= 0 &&
                spriteIndex < numberCardSprites.Length)
            {
                targetSprite = numberCardSprites[spriteIndex];
            }
        }
        else
        {
            targetSprite = cardBackSprite;
        }

        currentCardRenderer.sprite = targetSprite;
        SetSpriteRendererAlpha(currentCardRenderer, targetSprite != null ? 1f : 0f);
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
}