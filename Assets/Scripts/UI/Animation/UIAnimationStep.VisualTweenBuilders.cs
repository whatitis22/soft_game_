using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public partial class UIAnimationStep
{
    private Tween CreateImageTween()
    {
        if (!_useImage || !TryGetImage(out Image image))
        {
            return null;
        }

        if (!HasImageSpriteContent() && !_imageChangeDeltaSize && !_imageChangeColor)
        {
            return null;
        }

        Sequence sequence = CreateContentSequence(_imagePreDelay);

        if (HasImageSpriteContent())
        {
            sequence.AppendCallback(() => image.sprite = _imageSprite);
        }

        if (_imageChangeDeltaSize)
        {
            sequence.AppendCallback(() => ApplyImageDeltaSize(image));
        }

        if (_imageChangeColor)
        {
            if (_imageApplyPresetStartColor)
            {
                sequence.AppendCallback(() => image.color = _imageStartColor);
            }

            sequence.Append(image.DOColor(_imageColor, SafeTime(_imageDuration)).SetEase(_imageEase));
        }

        AppendPostDelay(sequence, _imagePostDelay);
        return sequence;
    }

    private Tween CreateTextTween()
    {
        if (!_useText || !TryGetTMPText(out TMP_Text tmpText) || !HasTextContent())
        {
            return null;
        }

        Sequence sequence = CreateContentSequence(_textPreDelay);
        AppendTextStartValueCallbacks(sequence, tmpText);

        bool hasAnimatedContent = false;
        AppendOrJoinTextTween(sequence, CreateTextColorTween(tmpText), ref hasAnimatedContent);
        AppendOrJoinTextTween(sequence, CreateTextAlphaTween(tmpText), ref hasAnimatedContent);

        AppendPostDelay(sequence, _textPostDelay);
        return sequence;
    }

    private void ApplyImageDeltaSize(Image image)
    {
        RectTransform rectTransform = image != null ? image.rectTransform : null;

        if (rectTransform == null)
        {
            return;
        }

        if (_imageDeltaSizeMode == TypeUIAnimationImageDeltaSizeMode.CUSTOM)
        {
            rectTransform.sizeDelta = _imageCustomDeltaSize;
            return;
        }

        if (TryGetImageSpriteDeltaSize(image, out Vector2 spriteDeltaSize))
        {
            rectTransform.sizeDelta = spriteDeltaSize;
        }
    }

    private bool TryGetImageSpriteDeltaSize(Image image, out Vector2 deltaSize)
    {
        Sprite sprite = image != null ? image.sprite : null;

        if (sprite == null)
        {
            deltaSize = Vector2.zero;
            return false;
        }

        float pixelsPerUnit = Mathf.Approximately(image.pixelsPerUnit, 0f) ? 1f : image.pixelsPerUnit;
        deltaSize = new Vector2(sprite.rect.width / pixelsPerUnit, sprite.rect.height / pixelsPerUnit);
        return true;
    }

    private void AppendTextStartValueCallbacks(Sequence sequence, TMP_Text tmpText)
    {
        if (_textChangeColor && _textApplyPresetStartColor)
        {
            sequence.AppendCallback(() => tmpText.color = _textStartColor);
        }
    }

    private Tween CreateTextColorTween(TMP_Text tmpText)
    {
        return _textChangeColor ? tmpText.DOColor(_textColor, SafeTime(_textDuration)).SetEase(_textEase) : null;
    }

    private Tween CreateTextAlphaTween(TMP_Text tmpText)
    {
        return _textChangeAlpha ? tmpText.DOFade(ClampAlpha(_textAlpha), SafeTime(_textDuration)).SetEase(_textEase) : null;
    }

    private bool TryGetImage(out Image image)
    {
        image = _target != null ? _target.Image : null;
        return image != null;
    }

    private bool TryGetTMPText(out TMP_Text tmpText)
    {
        tmpText = _target != null ? _target.TMPText : null;
        return tmpText != null;
    }
}
