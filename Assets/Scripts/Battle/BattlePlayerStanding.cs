using System.Collections.Generic;
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
    [SerializeField] private Animator animator;

    [Header("Animator Trigger Names")]
    [SerializeField] private string idleTrigger = "Idle";
    [SerializeField] private string hitTrigger = "Hit";
    [SerializeField] private string victoryTrigger = "Victory";
    [SerializeField] private string defeatTrigger = "Defeat";

    [Header("추가 테스트용 Trigger들")]
    [SerializeField] private string[] extraTestTriggers;

    public int PlayerIndex { get; private set; } = -1;
    public string PlayerKey { get; private set; } = string.Empty;

    private bool initialized;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
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
        PlayerIndex = playerIndex;
        PlayerKey = BattlePlayerStandingRegistry.ToKey(playerIndex);
        initialized = true;

        BattlePlayerStandingRegistry.Register(this);
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
}