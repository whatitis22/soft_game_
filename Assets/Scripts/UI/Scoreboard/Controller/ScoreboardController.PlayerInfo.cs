using UnityEngine;

public partial class ScoreboardController
{
    private static readonly Color GoodPingColor = new Color(0.2156863f, 0.654902f, 0.3137255f);
    private static readonly Color NormalPingColor = new Color(0.9450981f, 0.5019608f, 0.01568628f);
    private static readonly Color BadPingColor = new Color(0.8313726f, 0.01960784f, 0.1215686f);

    public void SetClass(TypeAdventurer target)
    {
        if (target == _class)
        {
            return;
        }

        _class = target;
        RefreshClassView();
    }

    public void SetNickname(string value)
    {
        if (value == null)
        {
            value = string.Empty;
        }

        if (value == _nickname)
        {
            return;
        }

        _nickname = value;
        RefreshNicknameView();
    }

    public void SetPingStatus(float value)
    {
        value = Mathf.Max(0f, value);

        if (Mathf.Approximately(value, _ping))
        {
            return;
        }

        _ping = value;
        RefreshPingView();
    }

    private void RefreshPlayerInfoView()
    {
        RefreshClassView();
        RefreshNicknameView();
        RefreshPingView();
    }

    private void RefreshClassView()
    {
        if (_adventurerClassIconImage != null && TryGetAdventurerSprite(_class, out Sprite sprite))
        {
            _adventurerClassIconImage.sprite = sprite;
        }
    }

    private void RefreshNicknameView()
    {
        RefreshTextView(_nicknameTMP, _nickname ?? string.Empty);
    }

    private void RefreshPingView()
    {
        RefreshTextView(_pingStatusTMP, _ping + "ms");

        if (_pingStatusImage != null)
        {
            _pingStatusImage.sprite = GetPingSprite(_ping);
        }

        if (_pingStatusTMP != null)
        {
            _pingStatusTMP.color = GetPingColor(_ping);
        }
    }

    private bool TryGetAdventurerSprite(TypeAdventurer adventurer, out Sprite sprite)
    {
        int index = (int)adventurer;

        if (_adventurerClassIconSprites == null ||
            index < 0 ||
            index >= _adventurerClassIconSprites.Length)
        {
            sprite = null;
            return false;
        }

        sprite = _adventurerClassIconSprites[index];
        return sprite != null;
    }

    private Sprite GetPingSprite(float ping)
    {
        if (ping <= 80f)
        {
            return _goodStatusSprite;
        }

        if (ping <= 150f)
        {
            return _normalStatusSprite;
        }

        return _badStatusSprite;
    }

    private Color GetPingColor(float ping)
    {
        if (ping <= 80f)
        {
            return GoodPingColor;
        }

        if (ping <= 150f)
        {
            return NormalPingColor;
        }

        return BadPingColor;
    }
}
