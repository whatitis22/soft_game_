using System;
using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class MonsterBattleState : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private int maxHp = 1;
    [SerializeField] private int currentHp = 1;
    [SerializeField] private bool showMaxHpInText = false;

    [Header("View")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private string hpTextObjectName = "HP Text";
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private string nameTextObjectName = "Monster Name Text";
    [SerializeField] private Vector3 nameTextLocalPosition = new Vector3(0f, 80f, -20.1f);

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string damagedTriggerName = "Damaged";
    [SerializeField] private string dieBoolName = "Die";
    [SerializeField] private string dieStateName = "Die";
    [SerializeField] private float dieStateEnterTimeout = 1f;

    private MonsterData monsterData;
    private bool deathAnimationStarted;
    private bool deathAnimationPending;
    private bool deathReported;
    private int deathSequenceVersion;

    public event Action<MonsterBattleState, int> Damaged;
    public event Action<MonsterBattleState, int, int> HpChanged;
    public event Action<MonsterBattleState> Died;
    public event Action<MonsterBattleState> DeathAnimationFinished;

    public MonsterData Data => monsterData;
    public int MaxHp => maxHp;
    public int CurrentHp => currentHp;
    public bool IsDead => currentHp <= 0;
    public bool HasPendingDeathAnimation => deathAnimationPending;

    public void Initialize(MonsterData data, string hpTextName)
    {
        monsterData = data;
        hpTextObjectName = string.IsNullOrWhiteSpace(hpTextName) ? "HP Text" : hpTextName;

        // 몬스터 데이터의 HP를 최대 HP와 현재 HP의 기준값으로 사용한다.
        maxHp = Mathf.Max(1, data != null ? data.HP : maxHp);
        currentHp = maxHp;
        deathAnimationStarted = false;
        deathAnimationPending = false;
        deathReported = false;
        deathSequenceVersion++;

        CacheReferences();
        SetAnimatorBoolIfExists(dieBoolName, false);
        ApplyTextPlaceholder(GetDisplayText(data));
        RefreshHpText();
        NotifyHpChanged();
    }

    private void Awake()
    {
        CacheReferences();
        RefreshHpText();
    }

    private void OnValidate()
    {
        maxHp = Mathf.Max(1, maxHp);
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        CacheReferences();
        RefreshHpText();
    }

    public bool ApplyDamage(int damage)
    {
        return ApplyDamage(damage, false);
    }

    public bool ApplyDamage(int damage, bool deferDeathAnimation)
    {
        if (damage <= 0 || IsDead)
            return false;

        int previousHp = currentHp;
        currentHp = Mathf.Max(0, currentHp - damage);
        int appliedDamage = previousHp - currentHp;

        RefreshHpText();
        NotifyHpChanged();
        Damaged?.Invoke(this, appliedDamage);

        if (IsDead)
        {
            HandleDeathReached(deferDeathAnimation);
        }
        else
        {
            PlayDamagedAnimation();
        }

        return appliedDamage > 0;
    }

    public bool Heal(int amount)
    {
        if (amount <= 0 || currentHp >= maxHp)
            return false;

        currentHp = Mathf.Min(maxHp, currentHp + amount);

        if (!IsDead)
        {
            deathAnimationStarted = false;
            deathAnimationPending = false;
            deathReported = false;
            deathSequenceVersion++;
            SetAnimatorBoolIfExists(dieBoolName, false);
        }

        RefreshHpText();
        NotifyHpChanged();
        return true;
    }

    public void SetCurrentHp(int hp)
    {
        int previousHp = currentHp;
        currentHp = Mathf.Clamp(hp, 0, maxHp);

        if (currentHp > 0)
        {
            deathAnimationStarted = false;
            deathAnimationPending = false;
            deathReported = false;
            deathSequenceVersion++;
            SetAnimatorBoolIfExists(dieBoolName, false);
        }
        else if (previousHp > 0)
        {
            HandleDeathReached(false);
        }

        RefreshHpText();
        NotifyHpChanged();
    }

    public void SetMaxHp(int hp, bool refillCurrentHp)
    {
        maxHp = Mathf.Max(1, hp);
        currentHp = refillCurrentHp ? maxHp : Mathf.Clamp(currentHp, 0, maxHp);
        RefreshHpText();
        NotifyHpChanged();
    }

    public void ResetHp()
    {
        currentHp = maxHp;
        deathAnimationStarted = false;
        deathAnimationPending = false;
        deathReported = false;
        deathSequenceVersion++;
        SetAnimatorBoolIfExists(dieBoolName, false);
        RefreshHpText();
        NotifyHpChanged();
    }

    public void ApplyNetworkState(int syncedMaxHp, int syncedCurrentHp, string syncedHpTextName, bool resetDeathState)
    {
        hpTextObjectName = string.IsNullOrWhiteSpace(syncedHpTextName) ? hpTextObjectName : syncedHpTextName;
        maxHp = Mathf.Max(1, syncedMaxHp);
        currentHp = Mathf.Clamp(syncedCurrentHp, 0, maxHp);

        if (resetDeathState && currentHp > 0)
        {
            deathAnimationStarted = false;
            deathAnimationPending = false;
            deathReported = false;
            deathSequenceVersion++;
            SetAnimatorBoolIfExists(dieBoolName, false);
        }

        CacheReferences();
        RefreshHpText();
    }

    public void RefreshHpText()
    {
        if (hpText == null)
            return;

        hpText.text = showMaxHpInText ? $"{currentHp} / {maxHp}" : currentHp.ToString();
    }

    public void SetDisplayName(string displayName)
    {
        ApplyTextPlaceholder(string.IsNullOrWhiteSpace(displayName) ? "Monster" : displayName);
    }

    private void NotifyHpChanged()
    {
        HpChanged?.Invoke(this, currentHp, maxHp);
    }

    private void CacheReferences()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (hpText == null)
            hpText = FindHpText();

        if (nameText == null)
            nameText = FindNameText();
    }

    private TMP_Text FindHpText()
    {
        TMP_Text[] textComponents = GetComponentsInChildren<TMP_Text>(true);
        TMP_Text fallback = null;

        for (int i = 0; i < textComponents.Length; i++)
        {
            TMP_Text textComponent = textComponents[i];
            if (textComponent == null)
                continue;

            if (fallback == null)
                fallback = textComponent;

            if (textComponent.gameObject.name == hpTextObjectName)
                return textComponent;
        }

        return fallback;
    }

    private TMP_Text FindNameText()
    {
        TMP_Text[] textComponents = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < textComponents.Length; i++)
        {
            TMP_Text textComponent = textComponents[i];
            if (textComponent != null && textComponent.gameObject.name == nameTextObjectName)
                return textComponent;
        }

        return null;
    }

    private string GetDisplayText(MonsterData data)
    {
        if (data == null)
            return "Monster";

        if (!string.IsNullOrWhiteSpace(data.preview_text))
            return data.preview_text;

        if (!string.IsNullOrWhiteSpace(data.monster_name))
            return data.monster_name;

        return string.IsNullOrWhiteSpace(data.id) ? "Monster" : data.id;
    }

    private void ApplyTextPlaceholder(string displayText)
    {
        HideSpriteRenderers();

        if (nameText == null)
            nameText = CreateNameText();

        if (nameText == null)
            return;

        nameText.text = displayText;
        nameText.gameObject.SetActive(true);
    }

    private TMP_Text CreateNameText()
    {
        if (hpText != null)
            return CreateNameTextFromHpText();

        GameObject textObject = new GameObject(nameTextObjectName);
        textObject.transform.SetParent(transform, false);
        textObject.transform.localPosition = nameTextLocalPosition;
        textObject.transform.localRotation = Quaternion.identity;
        textObject.transform.localScale = Vector3.one;

        TextMeshPro textMesh = textObject.AddComponent<TextMeshPro>();
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontSize = 80f;
        textMesh.color = Color.white;
        textMesh.textWrappingMode = TextWrappingModes.NoWrap;
        textMesh.sortingOrder = 10;

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        if (rectTransform != null)
            rectTransform.sizeDelta = new Vector2(360f, 60f);

        return textMesh;
    }

    private TMP_Text CreateNameTextFromHpText()
    {
        GameObject textObject = Instantiate(hpText.gameObject, hpText.transform.parent);
        textObject.name = nameTextObjectName;
        textObject.SetActive(true);

        TMP_Text textMesh = textObject.GetComponent<TMP_Text>();
        if (textMesh == null)
            return null;

        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.fontSize = 80f;
        textMesh.textWrappingMode = TextWrappingModes.NoWrap;
        Renderer renderer = textObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sortingOrder = 10;

        RectTransform nameRect = textObject.GetComponent<RectTransform>();
        RectTransform hpRect = hpText.GetComponent<RectTransform>();
        if (nameRect != null)
        {
            nameRect.anchoredPosition = new Vector2(0f, 95f);
            nameRect.sizeDelta = new Vector2(360f, 60f);

            Vector3 localPosition = hpRect != null ? hpRect.localPosition : nameTextLocalPosition;
            localPosition.x = 0f;
            localPosition.y = 0f;
            localPosition.z = hpRect != null ? hpRect.localPosition.z : nameTextLocalPosition.z;
            nameRect.localPosition = localPosition;
        }
        else
        {
            textObject.transform.localPosition = nameTextLocalPosition;
        }

        return textMesh;
    }

    private void HideSpriteRenderers()
    {
        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].enabled = false;
        }
    }

    private void PlayDamagedAnimation()
    {
        SetAnimatorTriggerIfExists(damagedTriggerName);
    }

    private void PlayDeathAnimation()
    {
        if (deathAnimationStarted)
            return;

        deathAnimationStarted = true;
        SetAnimatorBoolIfExists(dieBoolName, true);
    }

    private void BeginDeathSequence()
    {
        if (deathAnimationStarted)
            return;

        PlayDeathAnimation();
        deathAnimationPending = false;

        // Die 상태로 전환된 뒤 실제 클립 재생이 끝나는 순간까지 기다렸다가 제거 요청을 알린다.
        int sequenceVersion = deathSequenceVersion;
        StartCoroutine(WaitForDeathAnimationEnd(sequenceVersion));
    }

    private void HandleDeathReached(bool deferDeathAnimation)
    {
        if (!deathReported)
        {
            deathReported = true;
            Died?.Invoke(this);
        }

        if (deferDeathAnimation)
        {
            deathAnimationPending = true;
            return;
        }

        BeginDeathSequence();
    }

    public void PlayPendingDeathAnimation()
    {
        if (!IsDead)
            return;

        // 전투 연출이 모두 끝난 뒤 미뤄둔 Die 애니메이션을 시작한다.
        HandleDeathReached(false);
    }

    private IEnumerator WaitForDeathAnimationEnd(int sequenceVersion)
    {
        if (animator == null || string.IsNullOrWhiteSpace(dieStateName))
        {
            yield return null;
            NotifyDeathAnimationFinished(sequenceVersion);
            yield break;
        }

        bool enteredDeathState = false;
        float enterTimeoutAt = Time.time + Mathf.Max(0.1f, dieStateEnterTimeout);
        while (IsDeathSequenceCurrent(sequenceVersion) && Time.time <= enterTimeoutAt)
        {
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);

            if (IsAnimatorStateNamed(currentState, dieStateName) || IsAnimatorStateNamed(nextState, dieStateName))
            {
                enteredDeathState = true;
                break;
            }

            yield return null;
        }

        while (enteredDeathState && IsDeathSequenceCurrent(sequenceVersion))
        {
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            bool isDeathState = IsAnimatorStateNamed(currentState, dieStateName);

            if (isDeathState && !animator.IsInTransition(0) && currentState.normalizedTime >= 1f)
                break;

            yield return null;
        }

        NotifyDeathAnimationFinished(sequenceVersion);
    }

    private bool IsDeathSequenceCurrent(int sequenceVersion)
    {
        return IsDead && deathAnimationStarted && deathSequenceVersion == sequenceVersion;
    }

    private bool IsAnimatorStateNamed(AnimatorStateInfo stateInfo, string stateName)
    {
        return stateInfo.IsName(stateName) || stateInfo.shortNameHash == Animator.StringToHash(stateName);
    }

    private void NotifyDeathAnimationFinished(int sequenceVersion)
    {
        if (!IsDeathSequenceCurrent(sequenceVersion))
            return;

        DeathAnimationFinished?.Invoke(this);
    }

    private void SetAnimatorTriggerIfExists(string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
            return;

        if (HasAnimatorParameter(triggerName, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(triggerName);
    }

    private void SetAnimatorBoolIfExists(string boolName, bool value)
    {
        if (animator == null || string.IsNullOrWhiteSpace(boolName))
            return;

        if (HasAnimatorParameter(boolName, AnimatorControllerParameterType.Bool))
            animator.SetBool(boolName, value);
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null || animator.parameters == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == parameterType && parameter.name == parameterName)
                return true;
        }

        return false;
    }
}
