using UnityEngine;

public partial class ScoreboardControllerKeyboardTester
{
    private void AddGem(ScoreboardController target, TypeGem gemType, int amount)
    {
        target.SetGemAmount(gemType, target.GetGemAmount(gemType) + Mathf.Max(0, amount));
        LogAction($"+{amount} {gemType} gem => {target.GetGemAmount(gemType)}");
    }

    private void RemoveGem(ScoreboardController target, TypeGem gemType, int amount)
    {
        target.SetGemAmount(gemType, target.GetGemAmount(gemType) - Mathf.Max(0, amount));
        LogAction($"-{amount} {gemType} gem => {target.GetGemAmount(gemType)}");
    }

    private void AddScore(ScoreboardController target, int amount)
    {
        target.SetScore(target.GetScore() + Mathf.Max(0, amount));
        LogAction($"+{amount} score => {target.GetScore()}");
    }

    private void RemoveScore(ScoreboardController target, int amount)
    {
        target.SetScore(target.GetScore() - Mathf.Max(0, amount));
        LogAction($"-{amount} score => {target.GetScore()}");
    }

    private void RankUp(ScoreboardController target, int amount)
    {
        target.SetRank(target.GetRank() - Mathf.Max(0, amount));
        LogAction($"Rank up {amount} => {target.GetRank()}");
    }

    private void RankDown(ScoreboardController target, int amount)
    {
        target.SetRank(target.GetRank() + Mathf.Max(0, amount));
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
        target.SetGemAmount(0, 0, 0);
        target.SetScore(0);
        target.SetRank(1);

        currentClassIndex = 0;
        currentClass = AdventurerCycle[currentClassIndex];
        currentNicknameIndex = 0;
        currentPingIndex = 0;

        target.SetClass(currentClass);
        target.SetNickname(GetCurrentNickname());
        target.SetPingStatus(GetCurrentPing());

        LogAction("Scoreboard reset.");
    }
}
