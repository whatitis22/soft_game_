using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ScoreboardControllerKeyboardTester : MonoBehaviour
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
            CacheController();

        currentClassIndex = GetClassIndex(currentClass);
    }

    private void Start()
    {
        if (applyInitialPlayerInfoOnStart)
            ApplyCurrentPlayerInfo();

        if (logHelpOnStart)
            LogHelp();
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        ScoreboardController target = Controller;
        if (target == null)
        {
            WarnMissingController();
            return;
        }

        int multiplier = GetInputMultiplier();

        if (WasPressed(addRedGemKey))
            AddGem(target, TypeGem.Red, gemStep * multiplier);

        if (WasPressed(addGreenGemKey))
            AddGem(target, TypeGem.Green, gemStep * multiplier);

        if (WasPressed(addBlueGemKey))
            AddGem(target, TypeGem.Blue, gemStep * multiplier);

        if (WasPressed(removeRedGemKey))
            RemoveGem(target, TypeGem.Red, gemStep * multiplier);

        if (WasPressed(removeGreenGemKey))
            RemoveGem(target, TypeGem.Green, gemStep * multiplier);

        if (WasPressed(removeBlueGemKey))
            RemoveGem(target, TypeGem.Blue, gemStep * multiplier);

        if (WasPressed(addScoreKey))
            AddScore(target, scoreStep * multiplier);

        if (WasPressed(removeScoreKey))
            RemoveScore(target, scoreStep * multiplier);

        if (WasPressed(rankUpKey))
            RankUp(target, rankStep * multiplier);

        if (WasPressed(rankDownKey))
            RankDown(target, rankStep * multiplier);

        if (WasPressed(nextClassKey))
            NextClass(target);

        if (WasPressed(nextNicknameKey))
            NextNickname(target);

        if (WasPressed(nextPingKey))
            NextPing(target);

        if (WasPressed(resetKey))
            ResetScoreboard(target);

        if (WasPressed(helpKey))
            LogHelp();
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

    private void AddGem(ScoreboardController target, TypeGem gemType, int amount)
    {
        target.AddGemAmount(gemType, amount);
        LogAction($"+{amount} {gemType} gem => {target.GetGemAmount(gemType)}");
    }

    private void RemoveGem(ScoreboardController target, TypeGem gemType, int amount)
    {
        target.RemoveGemAmount(gemType, amount);
        LogAction($"-{amount} {gemType} gem => {target.GetGemAmount(gemType)}");
    }

    private void AddScore(ScoreboardController target, int amount)
    {
        target.AddScore(amount);
        LogAction($"+{amount} score => {target.GetScore()}");
    }

    private void RemoveScore(ScoreboardController target, int amount)
    {
        target.RemoveScore(amount);
        LogAction($"-{amount} score => {target.GetScore()}");
    }

    private void RankUp(ScoreboardController target, int amount)
    {
        target.RankUp(amount);
        LogAction($"Rank up {amount} => {target.GetRank()}");
    }

    private void RankDown(ScoreboardController target, int amount)
    {
        target.SetRank(target.GetRank() + amount);
        LogAction($"Rank down {amount} => {target.GetRank()}");
    }

    private void NextClass(ScoreboardController target)
    {
        currentClassIndex = (currentClassIndex + 1) % AdventurerCycle.Length;
        currentClass = AdventurerCycle[currentClassIndex];

        target.SetClass(currentClass);
        LogAction($"Class => {currentClass}");
    }

    private void NextNickname(ScoreboardController target)
    {
        if (nicknames == null || nicknames.Length == 0)
        {
            Debug.LogWarning("ScoreboardControllerKeyboardTester: No nicknames are configured.");
            return;
        }

        currentNicknameIndex = (currentNicknameIndex + 1) % nicknames.Length;
        target.SetNickname(GetCurrentNickname());
        LogAction($"Nickname => {GetCurrentNickname()}");
    }

    private void NextPing(ScoreboardController target)
    {
        if (pingValues == null || pingValues.Length == 0)
        {
            Debug.LogWarning("ScoreboardControllerKeyboardTester: No ping values are configured.");
            return;
        }

        currentPingIndex = (currentPingIndex + 1) % pingValues.Length;
        target.SetPingStatus(GetCurrentPing());
        LogAction($"Ping => {GetCurrentPing()}ms");
    }

    private void ResetScoreboard(ScoreboardController target)
    {
        target.ResetGemAmount();
        target.ResetScore();
        target.ResetRank();

        currentClassIndex = 0;
        currentClass = AdventurerCycle[currentClassIndex];
        currentNicknameIndex = 0;
        currentPingIndex = 0;

        target.SetClass(currentClass);
        target.SetNickname(GetCurrentNickname());
        target.SetPingStatus(GetCurrentPing());

        LogAction("Scoreboard reset.");
    }

    private void CacheController()
    {
        if (controller != null || !findTargetOnAwake)
            return;

        controller = GetComponent<ScoreboardController>();

        if (controller == null)
            controller = GetComponentInChildren<ScoreboardController>();

        if (controller == null)
            controller = FindAnyObjectByType<ScoreboardController>();
    }

    private bool WasPressed(Key key)
    {
        if (key == Key.None || Keyboard.current == null)
            return false;

        return Keyboard.current[key].wasPressedThisFrame;
    }

    private int GetInputMultiplier()
    {
        if (!useShiftMultiplier || Keyboard.current == null)
            return 1;

        bool shiftPressed =
            Keyboard.current.leftShiftKey.isPressed ||
            Keyboard.current.rightShiftKey.isPressed;

        return shiftPressed ? shiftMultiplier : 1;
    }

    private string GetCurrentNickname()
    {
        if (nicknames == null || nicknames.Length == 0)
            return string.Empty;

        currentNicknameIndex = Mathf.Clamp(currentNicknameIndex, 0, nicknames.Length - 1);
        return nicknames[currentNicknameIndex];
    }

    private float GetCurrentPing()
    {
        if (pingValues == null || pingValues.Length == 0)
            return 0f;

        currentPingIndex = Mathf.Clamp(currentPingIndex, 0, pingValues.Length - 1);
        return Mathf.Max(0f, pingValues[currentPingIndex]);
    }

    private int GetClassIndex(TypeAdventurer adventurer)
    {
        for (int i = 0; i < AdventurerCycle.Length; i++)
        {
            if (AdventurerCycle[i] == adventurer)
                return i;
        }

        currentClass = AdventurerCycle[0];
        return 0;
    }

    private void LogHelp()
    {
        if (!logActions)
            return;

        Debug.Log(
            "ScoreboardControllerKeyboardTester controls\n" +
            $"{addRedGemKey}/{addGreenGemKey}/{addBlueGemKey}: Add RGB gems\n" +
            $"{removeRedGemKey}/{removeGreenGemKey}/{removeBlueGemKey}: Remove RGB gems\n" +
            $"{addScoreKey}/{removeScoreKey}: Add/remove score\n" +
            $"{rankUpKey}/{rankDownKey}: Rank up/down\n" +
            $"{nextClassKey}/{nextNicknameKey}/{nextPingKey}: Next class/nickname/ping\n" +
            $"{resetKey}: Reset scoreboard, {helpKey}: Show controls\n" +
            "Hold Shift to multiply gem/score/rank changes.");
    }

    private void LogAction(string message)
    {
        if (logActions)
            Debug.Log($"ScoreboardControllerKeyboardTester: {message}");
    }

    private void WarnMissingController()
    {
        if (warnedMissingController)
            return;

        warnedMissingController = true;
        Debug.LogWarning("ScoreboardControllerKeyboardTester: ScoreboardController reference is missing.");
    }

    private void Reset()
    {
        findTargetOnAwake = true;
        controller = GetComponent<ScoreboardController>();
    }

    private void OnValidate()
    {
        gemStep = Mathf.Max(1, gemStep);
        scoreStep = Mathf.Max(1, scoreStep);
        rankStep = Mathf.Max(1, rankStep);
        shiftMultiplier = Mathf.Max(1, shiftMultiplier);
        currentClassIndex = GetClassIndex(currentClass);
    }
}
