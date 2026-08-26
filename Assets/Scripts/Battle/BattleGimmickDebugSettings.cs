using UnityEngine;

[AddComponentMenu("전투/기믹 디버그 설정")]
[DisallowMultipleComponent]
public class BattleGimmickDebugSettings : MonoBehaviour
{
    private static BattleGimmickDebugSettings instance;

    [Header("Knight")]
    [SerializeField] private bool enableKnightGimmicks = true;
    [SerializeField] private bool enableKnightCalculatedVictory = true;
    [SerializeField] private bool enableKnightFailedPlan = true;
    [SerializeField] private bool enableKnightShiftBlame = true;

    [Header("Saint")]
    [SerializeField] private bool enableSaintGimmicks = true;
    [SerializeField] private bool enableSaintRevelation = true;
    [SerializeField] private bool enableSaintAmen = true;
    [SerializeField] private bool enableSaintDamnGod = true;

    [Header("Mage")]
    [SerializeField] private bool enableMageGimmicks = true;
    [SerializeField] private bool enableMageMediocreSpell = true;
    [SerializeField] private bool enableMageDestructionSpell = true;
    [SerializeField] private bool enableMageWitchHunt = true;

    [Header("Archer")]
    [SerializeField] private bool enableArcherGimmicks = true;
    [SerializeField] private bool enableArcherSupportAttack = true;
    [SerializeField] private bool enableArcherScream = true;
    [SerializeField] private bool enableArcherContributionReward = true;

    [Header("Fighter")]
    [SerializeField] private bool enableFighterGimmicks = true;
    [SerializeField] private bool enableFighterTopCommand = true;
    [SerializeField] private bool enableFighterHarmony = true;
    [SerializeField] private bool enableFighterConflict = true;
    [SerializeField] private bool enableFighterLuckyReward = true;

    [Header("Defender")]
    [SerializeField] private bool enableDefenderGimmicks = true;
    [SerializeField] private bool enableDefenderLoan = true;
    [SerializeField] private bool enableDefenderRewardDebt = true;
    [SerializeField] private bool enableDefenderSettlement = true;
    [SerializeField] private bool enableDefenderBadDebt = true;

    [Header("Berserker")]
    [SerializeField] private bool enableBerserkerGimmicks = true;
    [SerializeField] private bool enableBerserkerOneMore = true;
    [SerializeField] private bool enableBerserkerOverexcited = true;

    public static bool KnightCalculatedVictoryEnabled => Current == null || (Current.enableKnightGimmicks && Current.enableKnightCalculatedVictory);
    public static bool KnightFailedPlanEnabled => Current == null || (Current.enableKnightGimmicks && Current.enableKnightFailedPlan);
    public static bool KnightShiftBlameEnabled => Current == null || (Current.enableKnightGimmicks && Current.enableKnightShiftBlame);

    public static bool SaintRevelationEnabled => Current == null || (Current.enableSaintGimmicks && Current.enableSaintRevelation);
    public static bool SaintAmenEnabled => Current == null || (Current.enableSaintGimmicks && Current.enableSaintAmen);
    public static bool SaintDamnGodEnabled => Current == null || (Current.enableSaintGimmicks && Current.enableSaintDamnGod);

    public static bool MageMediocreSpellEnabled => Current == null || (Current.enableMageGimmicks && Current.enableMageMediocreSpell);
    public static bool MageDestructionSpellEnabled => Current == null || (Current.enableMageGimmicks && Current.enableMageDestructionSpell);
    public static bool MageWitchHuntEnabled => Current == null || (Current.enableMageGimmicks && Current.enableMageWitchHunt);

    public static bool ArcherSupportAttackEnabled => Current == null || (Current.enableArcherGimmicks && Current.enableArcherSupportAttack);
    public static bool ArcherScreamEnabled => Current == null || (Current.enableArcherGimmicks && Current.enableArcherScream);
    public static bool ArcherContributionRewardEnabled => Current == null || (Current.enableArcherGimmicks && Current.enableArcherContributionReward);

    public static bool FighterTopCommandEnabled => Current == null || (Current.enableFighterGimmicks && Current.enableFighterTopCommand);
    public static bool FighterHarmonyEnabled => Current == null || (Current.enableFighterGimmicks && Current.enableFighterHarmony);
    public static bool FighterConflictEnabled => Current == null || (Current.enableFighterGimmicks && Current.enableFighterConflict);
    public static bool FighterLuckyRewardEnabled => Current == null || (Current.enableFighterGimmicks && Current.enableFighterLuckyReward);

    public static bool DefenderLoanEnabled => Current == null || (Current.enableDefenderGimmicks && Current.enableDefenderLoan);
    public static bool DefenderRewardDebtEnabled => Current == null || (Current.enableDefenderGimmicks && Current.enableDefenderRewardDebt);
    public static bool DefenderSettlementEnabled => Current == null || (Current.enableDefenderGimmicks && Current.enableDefenderSettlement);
    public static bool DefenderBadDebtEnabled => Current == null || (Current.enableDefenderGimmicks && Current.enableDefenderBadDebt);

    public static bool BerserkerOneMoreEnabled => Current == null || (Current.enableBerserkerGimmicks && Current.enableBerserkerOneMore);
    public static bool BerserkerOverexcitedEnabled => Current == null || (Current.enableBerserkerGimmicks && Current.enableBerserkerOverexcited);

    private static BattleGimmickDebugSettings Current
    {
        get
        {
            if (instance == null)
                instance = FindAnyObjectByType<BattleGimmickDebugSettings>(FindObjectsInactive.Include);

            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("BattleGimmickDebugSettings duplicate instance detected.", this);
            return;
        }

        instance = this;
    }

    private void OnEnable()
    {
        if (instance == null)
            instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
