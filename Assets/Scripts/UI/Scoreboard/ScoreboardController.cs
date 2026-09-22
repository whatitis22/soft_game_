using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScoreboardController : UIAnimationBaseController
{
    [Header("Player Info")]
    [SerializeField] private TypeAdventurer _class = TypeAdventurer.SAINT;
    [SerializeField] private string _nickname = "닉네임은열두글자";
    [SerializeField] private float _ping = 0f;

    [Header("Gems")]
    [SerializeField] private int _redGemAmount = 0;
    [SerializeField] private int _greenGemAmount = 0;
    [SerializeField] private int _blueGemAmount = 0;
    [Space(5f)]
    [SerializeField] private int _score = 0;
    [Space(5f)]
    [SerializeField] private int _playerCount = 1;
    [SerializeField] private int _currentRank = 1;

    [Header("Animations")]
    [SerializeField] private UIAnimationHandler _redGemGetAnimation;
    [SerializeField] private UIAnimationHandler _redGemLostAnimation;
    [SerializeField] private UIAnimationHandler _greenGemGetAnimation;
    [SerializeField] private UIAnimationHandler _greenGemLostAnimation;
    [SerializeField] private UIAnimationHandler _blueGemGetAnimation;
    [SerializeField] private UIAnimationHandler _blueGemLostAnimation;
    [Space(5f)]
    [SerializeField] private UIAnimationHandler _scoreGetAnimation;
    [SerializeField] private UIAnimationHandler _scoreLostAnimation;
    [Space(5f)]
    [SerializeField] private UIAnimationHandler _rankUpAnimation;
    [SerializeField] private UIAnimationHandler _rankDownAnimation;

    [Header("References")]
    [SerializeField] private Image _adventurerClassIconImage;
    [SerializeField] private Sprite[] _adventurerClassIconSprites;
    [Space(5f)]
    [SerializeField] private TMP_Text _nicknameTMP;
    [Space(5f)]
    [SerializeField] private Image _pingStatusImage;
    [SerializeField] private Sprite _goodStatusSprite;
    [SerializeField] private Sprite _normalStatusSprite;
    [SerializeField] private Sprite _badStatusSprite;
    [SerializeField] private TMP_Text _pingStatusTMP;
    [Space(5f)]
    [SerializeField] private TMP_Text _redGemTMP;
    [SerializeField] private TMP_Text _greenGemTMP;
    [SerializeField] private TMP_Text _blueGemTMP;
    [Space(5f)]
    [SerializeField] private TMP_Text _scoreTMP;
    [Space(5f)]
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Sprite _1stBackGroundSprite;
    [SerializeField] private Sprite _2ndBackGroundSprite;
    [SerializeField] private Sprite _3rdBackGroundSprite;
    [SerializeField] private Sprite _defaultBackGroundSprite;

    protected override void Awake()
    {
        base.Awake();
        // RefreshScoreView();
    }

    // --------------------------------------------------------------------------
    //  ● About Gems
    // --------------------------------------------------------------------------

    public int GetGemAmount(TypeGem gemType)
    {
        switch (gemType)
        {
            case TypeGem.Red:
                return _redGemAmount;

            case TypeGem.Green:
                return _greenGemAmount;

            case TypeGem.Blue:
                return _blueGemAmount;

            default:
                return 0;
        }
    }

    public void SetGemAmount(TypeGem gemType, int amount)
    {
        int previousAmount = GetGemAmount(gemType);
        amount = Mathf.Max(0, amount);

        if (amount == previousAmount)
            return;

        switch (gemType)
        {
            case TypeGem.Red:
                _redGemAmount = amount;
                RefreshTextView(_redGemTMP, _redGemAmount.ToString());
                break;

            case TypeGem.Green:
                _greenGemAmount = amount;
                RefreshTextView(_greenGemTMP, _greenGemAmount.ToString());
                break;

            case TypeGem.Blue:
                _blueGemAmount = amount;
                RefreshTextView(_blueGemTMP, _blueGemAmount.ToString());
                break;
        }

        int currentAmount = GetGemAmount(gemType);

        if (currentAmount >= previousAmount)
        {
            PlayGemGetAnimation(gemType);
        }
        else
        {
            PlayGemLostAnimation(gemType);
        }
    }

    public void SetGemAmount(int redGemAmount, int greenGemAmount, int blueGemAmount)
    {
        SetGemAmount(TypeGem.Red, redGemAmount);
        SetGemAmount(TypeGem.Green, greenGemAmount);
        SetGemAmount(TypeGem.Blue, blueGemAmount);
    }

    public void AddGemAmount(TypeGem gemType, int amount = 1)
    {
        amount = Mathf.Max(0, amount);

        SetGemAmount(gemType, GetGemAmount(gemType) + amount);
    }

    public void AddGemAmount(int redGemAmount = 1, int greenGemAmount = 1, int blueGemAmount = 1)
    {
        AddGemAmount(TypeGem.Red, redGemAmount);
        AddGemAmount(TypeGem.Green, greenGemAmount);
        AddGemAmount(TypeGem.Blue, blueGemAmount);
    }

    public void RemoveGemAmount(TypeGem gemType, int amount = 1)
    {
        amount = Mathf.Max(0, amount);

        SetGemAmount(gemType, GetGemAmount(gemType) - amount);
    }

    public void RemoveGemAmount(int redGemAmount = 1, int greenGemAmount = 1, int blueGemAmount = 1)
    {
        RemoveGemAmount(TypeGem.Red, redGemAmount);
        RemoveGemAmount(TypeGem.Green, greenGemAmount);
        RemoveGemAmount(TypeGem.Blue, blueGemAmount);
    }

    public void ResetGemAmount()
    {
        SetGemAmount(TypeGem.Red, 0);
        SetGemAmount(TypeGem.Green, 0);
        SetGemAmount(TypeGem.Blue, 0);
    }

    public void ResetGemAmount(TypeGem gemType)
    {
        SetGemAmount(gemType, 0);
    }

    // --------------------------------------------------------------------------
    //  ● About Gem Animations
    // --------------------------------------------------------------------------

    public void PlayGemGetAnimation(TypeGem gemType)
    {
        switch (gemType)
        {
            case TypeGem.Red:
                _redGemGetAnimation.Play();
                break;

            case TypeGem.Green:
                _greenGemGetAnimation.Play();
                break;

            case TypeGem.Blue:
                _blueGemGetAnimation.Play();
                break;
        }
    }

    public void PlayGemLostAnimation(TypeGem gemType)
    {
        switch (gemType)
        {
            case TypeGem.Red:
                _redGemLostAnimation.Play();
                break;

            case TypeGem.Green:
                _greenGemLostAnimation.Play();
                break;

            case TypeGem.Blue:
                _blueGemLostAnimation.Play();
                break;
        }
    }

    // --------------------------------------------------------------------------
    //  ● About Score
    // --------------------------------------------------------------------------

    public int GetScore()
    {
        return _score;
    }

    public void SetScore(int value)
    {
        int previousScore = GetScore();

        value = Mathf.Max(0, value);

        if (value == previousScore)
            return;

        _score = value;
        RefreshTextView(_scoreTMP, _score.ToString() + "pt");

        int currentScore = GetScore();

        if (currentScore >= previousScore)
        {
            _scoreGetAnimation.Play();
        }
        else
        {
            _scoreLostAnimation.Play();
        }
    }

    public void AddScore(int value = 1)
    {
        value = Mathf.Max(0, value);

        SetScore(GetScore() + value);
    }

    public void RemoveScore(int value = 1)
    {
        value = Mathf.Max(0, value);

        SetScore(GetScore() - value);
    }
    public void ResetScore()
    {
        SetScore(0);
    }

    // --------------------------------------------------------------------------
    //  ● About Rank
    // --------------------------------------------------------------------------

    public int GetRank()
    {
        return _currentRank;
    }

    public void SetRank(int rank)
    {
        int previousRank = _currentRank;

        rank = Mathf.Clamp(rank, 1, _playerCount);

        if (rank == previousRank)
            return;

        _currentRank = rank;
        UpdateRankBackground();

        if (rank < previousRank)
        {
            _rankUpAnimation.Play();
        }
        else
        {
            _rankDownAnimation.Play();
        }
    }

    public void RankUp(int rank = 1)
    {
        int newRank = Mathf.Clamp(_currentRank - rank, 1, _playerCount);

        SetRank(newRank);
    }

    private void RankDown(int rank = 1)
    {
        int newRank = Mathf.Clamp(_currentRank + rank, 1, _playerCount);

        SetRank(newRank);
    }

    public void ResetRank()
    {
        SetRank(1);
    }

    // --------------------------------------------------------------------------
    //  ● About Rank Background
    // --------------------------------------------------------------------------

    public void UpdateRankBackground(int rank)
    {
        switch (rank)
        {
            case 1:
                _backgroundImage.sprite = _1stBackGroundSprite;
                break;

            case 2:
                _backgroundImage.sprite = _2ndBackGroundSprite;
                break;

            case 3:
                _backgroundImage.sprite = _3rdBackGroundSprite;
                break;

            default:
                _backgroundImage.sprite = _defaultBackGroundSprite;
                break;
        }
    }

    public void UpdateRankBackground()
    {
        UpdateRankBackground(_currentRank);
    }

    // --------------------------------------------------------------------------
    //  ● About Refresh UI
    // --------------------------------------------------------------------------

    public void SetClass(TypeAdventurer target)
    {
        if (target == _class)
            return;

        _adventurerClassIconImage.sprite = _adventurerClassIconSprites[(int)target];
    }

    public void SetNickname(string value)
    {
        if (value == _nickname)
            return;

        RefreshTextView(_nicknameTMP, value);
    }

    public void SetPingStatus(float value)
    {
        value = Mathf.Max(0, value);

        if (value == _ping)
            return;

        RefreshTextView(_pingStatusTMP, value.ToString() + "ms");

        switch (value)
        {
            case <= 80:
                _pingStatusImage.sprite = _goodStatusSprite;
                _pingStatusTMP.color = new Color(0.2156863f, 0.654902f, 0.3137255f);
                break;

            case <= 150:
                _pingStatusImage.sprite = _normalStatusSprite;
                _pingStatusTMP.color = new Color(0.9450981f, 0.5019608f, 0.01568628f);
                break;

            default:
                _pingStatusImage.sprite = _badStatusSprite;
                _pingStatusTMP.color = new Color(0.8313726f, 0.01960784f, 0.1215686f);
                break;
        }
    }

    private void RefreshTextView(TMP_Text target, string value)
    {
        if (target == null)
            return;

        target.text = value;
    }
}