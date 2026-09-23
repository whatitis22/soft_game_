using UnityEngine;

public partial class ScoreboardController
{
    public int GetScore()
    {
        return _score;
    }

    public void SetScore(int value)
    {
        int previousScore = _score;
        value = Mathf.Max(0, value);

        if (value == previousScore)
        {
            return;
        }

        _score = value;
        RefreshScoreView();

        PlayAnimation(_score > previousScore ? _scoreGetAnimation : _scoreLostAnimation);
    }

    private void RefreshScoreView()
    {
        RefreshTextView(_scoreTMP, _score + "pt");
    }
}
