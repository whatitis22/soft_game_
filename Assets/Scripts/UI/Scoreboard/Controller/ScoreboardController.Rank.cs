using UnityEngine;

public partial class ScoreboardController
{
    public int GetRank()
    {
        return _currentRank;
    }

    public void SetRank(int rank)
    {
        int previousRank = _currentRank;
        rank = ClampRank(rank);

        if (rank == previousRank)
        {
            return;
        }

        _currentRank = rank;
        RefreshRankView();

        PlayAnimation(_currentRank < previousRank ? _rankUpAnimation : _rankDownAnimation);
    }

    public void SetPlayerCount(int playerCount)
    {
        _playerCount = Mathf.Max(1, playerCount);
        SetRank(_currentRank);
    }

    public void UpdateRankBackground(int rank)
    {
        if (_backgroundImage != null)
        {
            _backgroundImage.sprite = GetRankBackgroundSprite(rank);
        }
    }

    public void UpdateRankBackground()
    {
        UpdateRankBackground(_currentRank);
    }

    private int ClampRank(int rank)
    {
        return Mathf.Clamp(rank, 1, Mathf.Max(1, _playerCount));
    }

    private void RefreshRankView()
    {
        _currentRank = ClampRank(_currentRank);
        UpdateRankBackground();
    }

    private Sprite GetRankBackgroundSprite(int rank)
    {
        switch (rank)
        {
            case 1:
                return _1stBackGroundSprite;
            case 2:
                return _2ndBackGroundSprite;
            case 3:
                return _3rdBackGroundSprite;
            default:
                return _defaultBackGroundSprite;
        }
    }
}
