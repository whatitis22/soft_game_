using UnityEngine;

public partial class UIAnimationStep
{
    private float CalculateEstimatedDuration()
    {
        float duration = 0f;

        duration = Mathf.Max(duration, CalculateContentDuration(_useMove, _movePreDelay, _moveDuration, _movePostDelay));
        duration = Mathf.Max(duration, CalculateContentDuration(_useRotation, _rotationPreDelay, _rotationDuration, _rotationPostDelay));
        duration = Mathf.Max(duration, CalculateContentDuration(_useScale, _scalePreDelay, _scaleDuration, _scalePostDelay));
        duration = Mathf.Max(duration, CalculateContentDuration(_useFade, _fadePreDelay, _fadeDuration, _fadePostDelay));
        duration = Mathf.Max(duration, CalculateImageDuration());
        duration = Mathf.Max(duration, CalculateTextDuration());

        return duration;
    }

    private float CalculateImageDuration()
    {
        if (!_useImage || (!HasImageSpriteContent() && !_imageChangeDeltaSize && !_imageChangeColor))
        {
            return 0f;
        }

        float colorDuration = _imageChangeColor ? _imageDuration : 0f;
        return CalculateContentDuration(true, _imagePreDelay, colorDuration, _imagePostDelay);
    }

    private float CalculateTextDuration()
    {
        if (!_useText || !HasTextContent())
        {
            return 0f;
        }

        float contentDuration = HasAnimatedTextContent() ? _textDuration : 0f;
        return CalculateContentDuration(true, _textPreDelay, contentDuration, _textPostDelay);
    }

    private bool HasImageSpriteContent()
    {
        return _useImage && _imageChangeSprite && _imageSprite != null;
    }

    private bool HasTextContent()
    {
        return _textChangeColor || _textChangeAlpha;
    }

    private bool HasAnimatedTextContent()
    {
        return _textChangeColor || _textChangeAlpha;
    }

    private float CalculateContentDuration(bool isEnabled, float preDelay, float duration, float postDelay)
    {
        if (!isEnabled)
        {
            return 0f;
        }

        return SafeTime(preDelay) + SafeTime(duration) + SafeTime(postDelay);
    }

    private float SafeTime(float value)
    {
        return Mathf.Max(0f, value);
    }

    private float ClampAlpha(float value)
    {
        return Mathf.Clamp01(value);
    }
}
