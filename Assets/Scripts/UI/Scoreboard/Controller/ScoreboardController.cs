using UnityEngine;
using UnityEngine.UI;
using TMPro;

public partial class ScoreboardController : MonoBehaviour
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

    private void Awake()
    {
        RefreshAllViews();
    }

    private void RefreshAllViews()
    {
        RefreshGemView(TypeGem.Red);
        RefreshGemView(TypeGem.Green);
        RefreshGemView(TypeGem.Blue);
        RefreshScoreView();
        RefreshRankView();
        RefreshPlayerInfoView();
    }

    private void RefreshTextView(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private void PlayAnimation(UIAnimationHandler animation)
    {
        animation?.Play();
    }
}
