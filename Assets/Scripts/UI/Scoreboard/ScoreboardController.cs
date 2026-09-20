using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ScoreboardController : UIAnimationBaseController
{
    [SerializeField]
    private Dictionary<TypeGem, int> _localGemPouch = new()
    {
        {TypeGem.Red, 0},
        {TypeGem.Green, 0},
        {TypeGem.Blue, 0}
    };

    [SerializeField] private UIAnimationReferenceHub _redGemText;
    [SerializeField] private UIAnimationReferenceHub _blueGemText;
    [SerializeField] private UIAnimationReferenceHub _greenGemText;

    private int _score;

    protected override void Awake()
    {
        base.Awake();
        // RefreshScoreView();
    }

    public void SetGemAmount(TypeGem gemType, int amount)
    {
        _localGemPouch[gemType] = Mathf.Max(0, amount);
    }

    public void SetGemAmount(int redGemAmount, int greenGemAmount, int blueGemAmount)
    {
        SetGemAmount(TypeGem.Red, redGemAmount);
        SetGemAmount(TypeGem.Green, greenGemAmount);
        SetGemAmount(TypeGem.Blue, blueGemAmount);
    }

    public void AddGemAmount(TypeGem gemType, int amount)
    {
        _localGemPouch[gemType] += Mathf.Max(0, amount);
    }

    public void AddGemAmount(int redGemAmount, int greenGemAmount, int blueGemAmount)
    {
        AddGemAmount(TypeGem.Red, redGemAmount);
        AddGemAmount(TypeGem.Green, greenGemAmount);
        AddGemAmount(TypeGem.Blue, blueGemAmount);
    }

    public void RemoveGemAmount(TypeGem gemType, int amount)
    {
        _localGemPouch[gemType] =
            Mathf.Max(0, _localGemPouch[gemType] - Mathf.Max(0, amount));
    }

    public void RemoveGemAmount(int redGemAmount, int greenGemAmount, int blueGemAmount)
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