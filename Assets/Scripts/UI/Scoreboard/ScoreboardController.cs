using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Sirenix.OdinInspector;

public class ScoreboardController : UIAnimationBaseController
{
    [Header("Gems")]
    [SerializeField] private int _redGemAmount = 0;
    [SerializeField] private int _greenGemAmount = 0;
    [SerializeField] private int _blueGemAmount = 0;
    [Space(5f)]
    [SerializeField] private int _score = 0;

    [Header("Animations")]
    [SerializeField] private UIAnimationHandler _redGemGetAnimaion;
    [SerializeField] private UIAnimationHandler _redGemLostAnimaion;
    [SerializeField] private UIAnimationHandler _greenGemGetAnimaion;
    [SerializeField] private UIAnimationHandler _greenGemLostAnimaion;
    [SerializeField] private UIAnimationHandler _blueGemGetAnimaion;
    [SerializeField] private UIAnimationHandler _blueGemLostAnimaion;
    [Space(5f)]
    [SerializeField] private UIAnimationHandler _scoreUpAnimaion;
    [SerializeField] private UIAnimationHandler _scoreDownAnimaion;


    [Header("References")]
    [SerializeField] private UIAnimationReferenceHub _redGemText;
    [SerializeField] private UIAnimationReferenceHub _blueGemText;
    [SerializeField] private UIAnimationReferenceHub _greenGemText;
    [Space(5f)]
    [SerializeField] private UIAnimationReferenceHub _scoreText;

    protected override void Awake()
    {
        base.Awake();
        // RefreshScoreView();
    }

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
        amount = Mathf.Max(0, amount);

        switch (gemType)
        {
            case TypeGem.Red:
                _redGemAmount = amount;
                break;

            case TypeGem.Green:
                _greenGemAmount = amount;
                break;

            case TypeGem.Blue:
                _blueGemAmount = amount;
                break;
        }
    }

    public void SetGemAmount(
        int redGemAmount,
        int greenGemAmount,
        int blueGemAmount)
    {
        SetGemAmount(TypeGem.Red, redGemAmount);
        SetGemAmount(TypeGem.Green, greenGemAmount);
        SetGemAmount(TypeGem.Blue, blueGemAmount);
    }

    public void AddGemAmount(TypeGem gemType, int amount)
    {
        amount = Mathf.Max(0, amount);

        SetGemAmount(
            gemType,
            GetGemAmount(gemType) + amount
        );
    }

    public void AddGemAmount(
        int redGemAmount,
        int greenGemAmount,
        int blueGemAmount)
    {
        AddGemAmount(TypeGem.Red, redGemAmount);
        AddGemAmount(TypeGem.Green, greenGemAmount);
        AddGemAmount(TypeGem.Blue, blueGemAmount);
    }

    public void RemoveGemAmount(TypeGem gemType, int amount)
    {
        amount = Mathf.Max(0, amount);

        SetGemAmount(
            gemType,
            GetGemAmount(gemType) - amount
        );
    }

    public void RemoveGemAmount(
        int redGemAmount,
        int greenGemAmount,
        int blueGemAmount)
    {
        RemoveGemAmount(TypeGem.Red, redGemAmount);
        RemoveGemAmount(TypeGem.Green, greenGemAmount);
        RemoveGemAmount(TypeGem.Blue, blueGemAmount);
    }

    // private void RefreshScoreView()
    // {
    //     if (_scoreText != null)
    //     {
    //         _scoreText.text = _score.ToString();
    //     }
    // }
}