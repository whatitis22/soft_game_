using UnityEngine;
using static JobDefinition;

public class BattlePlayerStanding : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Header("Animator Trigger Names")]
    [SerializeField] private string idleTrigger = "Idle";
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private string victoryTrigger = "Victory";
    [SerializeField] private string defeatTrigger = "Defeat";

    public int PlayerIndex { get; private set; } = -1;
    public string PlayerKey { get; private set; }

    private bool initialized = false;

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

            case BattleStandingAction.Attack:
                SetTrigger(attackTrigger);
                break;

            case BattleStandingAction.Victory:
                SetTrigger(victoryTrigger);
                break;

            case BattleStandingAction.Defeat:
                SetTrigger(defeatTrigger);
                break;
        }
    }

    public void PlayCustom(string triggerName)
    {
        SetTrigger(triggerName);
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