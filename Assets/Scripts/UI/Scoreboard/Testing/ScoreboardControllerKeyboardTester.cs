using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public partial class ScoreboardControllerKeyboardTester : MonoBehaviour
{
    private static readonly TypeAdventurer[] AdventurerCycle =
    {
        TypeAdventurer.KNIGHT,
        TypeAdventurer.SAINT,
        TypeAdventurer.MAGE,
        TypeAdventurer.ARCHER,
        TypeAdventurer.FIGHTER,
        TypeAdventurer.BERSERKER,
        TypeAdventurer.DEFENDER,
    };

    [Header("Target")]
    [SerializeField] private ScoreboardController controller;
    [SerializeField] private bool findTargetOnAwake = true;
    [SerializeField] private bool applyInitialPlayerInfoOnStart = true;

    [Header("Step Values")]
    [SerializeField, Min(1)] private int gemStep = 1;
    [SerializeField, Min(1)] private int scoreStep = 10;
    [SerializeField, Min(1)] private int rankStep = 1;
    [SerializeField] private bool useShiftMultiplier = true;
    [SerializeField, Min(1)] private int shiftMultiplier = 5;

    [Header("Player Test Values")]
    [SerializeField] private TypeAdventurer currentClass = TypeAdventurer.SAINT;
    [SerializeField] private string[] nicknames =
    {
        "PlayerOne",
        "NicknameTest",
        "LongName12",
    };
    [SerializeField] private float[] pingValues =
    {
        40f,
        120f,
        220f,
    };

    [Header("Gem Keys")]
    [SerializeField] private Key addRedGemKey = Key.Digit1;
    [SerializeField] private Key addGreenGemKey = Key.Digit2;
    [SerializeField] private Key addBlueGemKey = Key.Digit3;
    [SerializeField] private Key removeRedGemKey = Key.Q;
    [SerializeField] private Key removeGreenGemKey = Key.W;
    [SerializeField] private Key removeBlueGemKey = Key.E;

    [Header("Score / Rank Keys")]
    [SerializeField] private Key addScoreKey = Key.UpArrow;
    [SerializeField] private Key removeScoreKey = Key.DownArrow;
    [SerializeField] private Key rankUpKey = Key.LeftArrow;
    [SerializeField] private Key rankDownKey = Key.RightArrow;

    [Header("Player Info Keys")]
    [SerializeField] private Key nextClassKey = Key.C;
    [SerializeField] private Key nextNicknameKey = Key.N;
    [SerializeField] private Key nextPingKey = Key.P;

    [Header("Utility Keys")]
    [SerializeField] private Key resetKey = Key.R;
    [SerializeField] private Key helpKey = Key.H;
    [SerializeField] private bool logActions = true;
    [SerializeField] private bool logHelpOnStart = true;

    private int currentNicknameIndex;
    private int currentPingIndex;
    private int currentClassIndex;
    private bool warnedMissingController;

    public ScoreboardController Controller
    {
        get
        {
            CacheController();
            return controller;
        }
    }

    private void Awake()
    {
        if (findTargetOnAwake)
        {
            CacheController();
        }

        currentClassIndex = GetClassIndex(currentClass);
    }

    private void Start()
    {
        if (applyInitialPlayerInfoOnStart)
        {
            ApplyCurrentPlayerInfo();
        }

        if (logHelpOnStart)
        {
            LogHelp();
        }
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        ScoreboardController target = Controller;
        if (target == null)
        {
            WarnMissingController();
            return;
        }

        int multiplier = GetInputMultiplier();

        if (WasPressed(addRedGemKey))
        {
            AddGem(target, TypeGem.Red, gemStep * multiplier);
        }

        if (WasPressed(addGreenGemKey))
        {
            AddGem(target, TypeGem.Green, gemStep * multiplier);
        }

        if (WasPressed(addBlueGemKey))
        {
            AddGem(target, TypeGem.Blue, gemStep * multiplier);
        }

        if (WasPressed(removeRedGemKey))
        {
            RemoveGem(target, TypeGem.Red, gemStep * multiplier);
        }

        if (WasPressed(removeGreenGemKey))
        {
            RemoveGem(target, TypeGem.Green, gemStep * multiplier);
        }

        if (WasPressed(removeBlueGemKey))
        {
            RemoveGem(target, TypeGem.Blue, gemStep * multiplier);
        }

        if (WasPressed(addScoreKey))
        {
            AddScore(target, scoreStep * multiplier);
        }

        if (WasPressed(removeScoreKey))
        {
            RemoveScore(target, scoreStep * multiplier);
        }

        if (WasPressed(rankUpKey))
        {
            RankUp(target, rankStep * multiplier);
        }

        if (WasPressed(rankDownKey))
        {
            RankDown(target, rankStep * multiplier);
        }

        if (WasPressed(nextClassKey))
        {
            NextClass(target);
        }

        if (WasPressed(nextNicknameKey))
        {
            NextNickname(target);
        }

        if (WasPressed(nextPingKey))
        {
            NextPing(target);
        }

        if (WasPressed(resetKey))
        {
            ResetScoreboard(target);
        }

        if (WasPressed(helpKey))
        {
            LogHelp();
        }
    }

    [ContextMenu("Apply Current Player Info")]
    public void ApplyCurrentPlayerInfo()
    {
        ScoreboardController target = Controller;
        if (target == null)
        {
            WarnMissingController();
            return;
        }

        target.SetClass(currentClass);
        target.SetNickname(GetCurrentNickname());
        target.SetPingStatus(GetCurrentPing());

        LogAction($"Applied player info: {currentClass}, {GetCurrentNickname()}, {GetCurrentPing()}ms");
    }

    [ContextMenu("Reset Test Scoreboard")]
    public void ResetScoreboard()
    {
        ScoreboardController target = Controller;
        if (target == null)
        {
            WarnMissingController();
            return;
        }

        ResetScoreboard(target);
    }
}
