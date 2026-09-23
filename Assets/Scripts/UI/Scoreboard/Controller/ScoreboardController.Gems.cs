using UnityEngine;
using TMPro;

public partial class ScoreboardController
{
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
        {
            return;
        }

        SetGemState(gemType, amount);
        RefreshGemView(gemType);

        if (amount > previousAmount)
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

    public void PlayGemGetAnimation(TypeGem gemType)
    {
        PlayAnimation(GetGemAnimation(gemType, true));
    }

    public void PlayGemLostAnimation(TypeGem gemType)
    {
        PlayAnimation(GetGemAnimation(gemType, false));
    }

    private void SetGemState(TypeGem gemType, int amount)
    {
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

    private void RefreshGemView(TypeGem gemType)
    {
        RefreshTextView(GetGemText(gemType), GetGemAmount(gemType).ToString());
    }

    private TMP_Text GetGemText(TypeGem gemType)
    {
        switch (gemType)
        {
            case TypeGem.Red:
                return _redGemTMP;
            case TypeGem.Green:
                return _greenGemTMP;
            case TypeGem.Blue:
                return _blueGemTMP;
            default:
                return null;
        }
    }

    private UIAnimationHandler GetGemAnimation(TypeGem gemType, bool isGain)
    {
        switch (gemType)
        {
            case TypeGem.Red:
                return isGain ? _redGemGetAnimation : _redGemLostAnimation;
            case TypeGem.Green:
                return isGain ? _greenGemGetAnimation : _greenGemLostAnimation;
            case TypeGem.Blue:
                return isGain ? _blueGemGetAnimation : _blueGemLostAnimation;
            default:
                return null;
        }
    }
}
