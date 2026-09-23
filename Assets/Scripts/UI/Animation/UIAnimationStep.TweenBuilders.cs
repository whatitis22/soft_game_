using UnityEngine;
using DG.Tweening;

public partial class UIAnimationStep
{
    private Tween CreateMoveTween()
    {
        if (!_useMove || !TryGetRectTransform(out RectTransform rectTransform))
        {
            return null;
        }

        Sequence sequence = CreateContentSequence(_movePreDelay);

        if (_moveValueMode == TypeUIAnimationValue.ABSOLUTE)
        {
            if (_moveApplyPresetStartPosition)
            {
                sequence.AppendCallback(() => rectTransform.anchoredPosition3D = _moveStartPosition);
            }

            sequence.Append(rectTransform.DOAnchorPos3D(_moveEndPosition, SafeTime(_moveDuration)).SetEase(_moveEase));
        }
        else
        {
            sequence.Append(rectTransform.DOAnchorPos3D(_moveOffset, SafeTime(_moveDuration)).SetEase(_moveEase).SetRelative());
        }

        AppendPostDelay(sequence, _movePostDelay);
        return sequence;
    }

    private Tween CreateRotationTween()
    {
        if (!_useRotation || !TryGetRectTransform(out RectTransform rectTransform))
        {
            return null;
        }

        Sequence sequence = CreateContentSequence(_rotationPreDelay);

        if (_rotationValueMode == TypeUIAnimationValue.ABSOLUTE)
        {
            if (_rotationApplyPresetStartRotation)
            {
                sequence.AppendCallback(() => rectTransform.localEulerAngles = _rotationStartRotation);
            }

            sequence.Append(rectTransform.DOLocalRotate(_rotationEndRotation, SafeTime(_rotationDuration), _rotationMode).SetEase(_rotationEase));
        }
        else
        {
            sequence.Append(rectTransform.DOLocalRotate(_rotationOffset, SafeTime(_rotationDuration), _rotationMode).SetEase(_rotationEase).SetRelative());
        }

        AppendPostDelay(sequence, _rotationPostDelay);
        return sequence;
    }

    private Tween CreateScaleTween()
    {
        if (!_useScale || !TryGetRectTransform(out RectTransform rectTransform))
        {
            return null;
        }

        Sequence sequence = CreateContentSequence(_scalePreDelay);

        if (_scaleValueMode == TypeUIAnimationValue.ABSOLUTE)
        {
            if (_scaleApplyPresetStartScale)
            {
                sequence.AppendCallback(() => rectTransform.localScale = _scaleStartScale);
            }

            sequence.Append(rectTransform.DOScale(_scaleEndScale, SafeTime(_scaleDuration)).SetEase(_scaleEase));
        }
        else
        {
            sequence.Append(rectTransform.DOScale(_scaleOffset, SafeTime(_scaleDuration)).SetEase(_scaleEase).SetRelative());
        }

        AppendPostDelay(sequence, _scalePostDelay);
        return sequence;
    }

    private Tween CreateFadeTween()
    {
        if (!_useFade || !TryGetCanvasGroup(out CanvasGroup canvasGroup))
        {
            return null;
        }

        Sequence sequence = CreateContentSequence(_fadePreDelay);

        if (_fadeValueMode == TypeUIAnimationValue.ABSOLUTE)
        {
            if (_fadeApplyPresetStartAlpha)
            {
                sequence.AppendCallback(() => canvasGroup.alpha = ClampAlpha(_fadeStartAlpha));
            }

            sequence.Append(canvasGroup.DOFade(ClampAlpha(_fadeEndAlpha), SafeTime(_fadeDuration)).SetEase(_fadeEase));
        }
        else
        {
            sequence.Append(canvasGroup.DOFade(_fadeOffset, SafeTime(_fadeDuration)).SetEase(_fadeEase).SetRelative());
        }

        AppendPostDelay(sequence, _fadePostDelay);
        return sequence;
    }

    private bool TryGetRectTransform(out RectTransform rectTransform)
    {
        rectTransform = _target != null ? _target.RectTransform : null;
        return rectTransform != null;
    }

    private bool TryGetCanvasGroup(out CanvasGroup canvasGroup)
    {
        canvasGroup = _target != null ? _target.CanvasGroup : null;
        return canvasGroup != null;
    }

    private Sequence CreateContentSequence(float preDelay)
    {
        Sequence sequence = DOTween.Sequence();

        if (preDelay > 0f)
        {
            sequence.AppendInterval(preDelay);
        }

        return sequence;
    }

    private void AppendPostDelay(Sequence sequence, float postDelay)
    {
        if (postDelay > 0f)
        {
            sequence.AppendInterval(postDelay);
        }
    }

    private void JoinContent(Sequence sequence, Tween contentTween, ref bool hasContent)
    {
        if (contentTween == null)
        {
            return;
        }

        if (hasContent)
        {
            sequence.Join(contentTween);
            return;
        }

        sequence.Append(contentTween);
        hasContent = true;
    }

    private void AppendOrJoinTextTween(Sequence sequence, Tween contentTween, ref bool hasAnimatedContent)
    {
        if (contentTween == null)
        {
            return;
        }

        if (hasAnimatedContent)
        {
            sequence.Join(contentTween);
            return;
        }

        sequence.Append(contentTween);
        hasAnimatedContent = true;
    }
}
