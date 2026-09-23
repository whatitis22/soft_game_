using UnityEngine;
using UnityEngine.InputSystem;

public partial class ScoreboardControllerKeyboardTester
{
    private void CacheController()
    {
        if (controller != null || !findTargetOnAwake)
        {
            return;
        }

        controller = GetComponent<ScoreboardController>();

        if (controller == null)
        {
            controller = GetComponentInChildren<ScoreboardController>();
        }

        if (controller == null)
        {
            controller = FindAnyObjectByType<ScoreboardController>();
        }
    }

    private bool WasPressed(Key key)
    {
        if (key == Key.None || Keyboard.current == null)
        {
            return false;
        }

        return Keyboard.current[key].wasPressedThisFrame;
    }

    private int GetInputMultiplier()
    {
        if (!useShiftMultiplier || Keyboard.current == null)
        {
            return 1;
        }

        bool shiftPressed =
            Keyboard.current.leftShiftKey.isPressed ||
            Keyboard.current.rightShiftKey.isPressed;

        return shiftPressed ? shiftMultiplier : 1;
    }

    private string GetCurrentNickname()
    {
        if (nicknames == null || nicknames.Length == 0)
        {
            return string.Empty;
        }

        currentNicknameIndex = Mathf.Clamp(currentNicknameIndex, 0, nicknames.Length - 1);
        return nicknames[currentNicknameIndex];
    }

    private float GetCurrentPing()
    {
        if (pingValues == null || pingValues.Length == 0)
        {
            return 0f;
        }

        currentPingIndex = Mathf.Clamp(currentPingIndex, 0, pingValues.Length - 1);
        return Mathf.Max(0f, pingValues[currentPingIndex]);
    }

    private int GetClassIndex(TypeAdventurer adventurer)
    {
        for (int i = 0; i < AdventurerCycle.Length; i++)
        {
            if (AdventurerCycle[i] == adventurer)
            {
                return i;
            }
        }

        currentClass = AdventurerCycle[0];
        return 0;
    }

    private void LogHelp()
    {
        if (!logActions)
        {
            return;
        }

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
        {
            Debug.Log($"ScoreboardControllerKeyboardTester: {message}");
        }
    }

    private void WarnMissingController()
    {
        if (warnedMissingController)
        {
            return;
        }

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
