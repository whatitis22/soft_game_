using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sirenix.OdinInspector;
using DG.Tweening;

// ==============================================================================
//  ■ UIAnimationStep
// ------------------------------------------------------------------------------
//  파츠
// ==============================================================================

[Serializable]
public class UIAnimationStep : MonoBehaviour
{
    // --------------------------------------------------------------------------
    //  ● Private Fields
    // --------------------------------------------------------------------------
    [SerializeField] private string _label; // 스텝의 제목 또는 이름

    [TitleGroup("Runtime")]
    [SerializeField] private UIAnimationReferenceHub _target;        // 스텝의 대상
    [SerializeField] private TypeUIAnimationSequence _sequenceMode;  // 스텝이 전환되는 방식

    [TitleGroup("Content")]
    [HorizontalGroup("Content/Row1"), ToggleLeft]
    [SerializeField] private bool _useMove = false;      // 움직임 연출 사용
    [HorizontalGroup("Content/Row1"), ToggleLeft]
    [SerializeField] private bool _useFade = false;      // 페이드 연출 사용
    [HorizontalGroup("Content/Row2"), ToggleLeft]
    [SerializeField] private bool _useRotation = false;  // 회전 연출 사용
    [HorizontalGroup("Content/Row2"), ToggleLeft]
    [SerializeField] private bool _useImage = false;     // 이미지 및 색 변경 사용
    [HorizontalGroup("Content/Row3"), ToggleLeft]
    [SerializeField] private bool _useScale = false;     // 크기 조절 연출 사용
    [HorizontalGroup("Content/Row3"), ToggleLeft]
    [SerializeField] private bool _useText = false;      // 텍스트 연출 사용
    [Space(5f)]

    // * 움직임 연출 관련 필드
    [BoxGroup("Move Value"), LabelText("Value Mode"), ShowIf("_useMove", true)]
    [SerializeField] private TypeUIAnimationValue _moveValueMode;
    [Space(5f)]
    [BoxGroup("Move Value"), LabelText("Apply Preset Start Position"), ToggleLeft, ShowIf("@_useMove && _moveValueMode == TypeUIAnimationValue.ABSOLUTE", true)]
    [SerializeField] private bool _moveApplyPresetStartPosition = false;
    [Space(5f)]
    [BoxGroup("Move Value"), LabelText("Start Position"), ShowIf("@_useMove && _moveValueMode == TypeUIAnimationValue.ABSOLUTE && _moveApplyPresetStartPosition", true)]
    [SerializeField] private Vector3 _moveStartPosition;
    [BoxGroup("Move Value"), LabelText("End Position"), ShowIf("@_useMove && _moveValueMode == TypeUIAnimationValue.ABSOLUTE", true)]
    [SerializeField] private Vector3 _moveEndPosition;
    [BoxGroup("Move Value"), LabelText("Offset"), ShowIf("@_useMove && _moveValueMode == TypeUIAnimationValue.RELATIVE", true)]
    [SerializeField] private Vector3 _moveOffset;
    [Space(5f)]
    [BoxGroup("Move Value"), LabelText("Ease"), ShowIf("_useMove", true)]
    [SerializeField] private Ease _moveEase = Ease.Linear;
    [BoxGroup("Move Value"), LabelText("Duration"), ShowIf("_useMove", true)]
    [SerializeField] private float _moveDuration = 0f;
    [Space(5f)]
    [BoxGroup("Move Value"), LabelText("Pre Delay"), ShowIf("_useMove", true)]
    [SerializeField] private float _movePreDelay = 0f;
    [BoxGroup("Move Value"), LabelText("Post Delay"), ShowIf("_useMove", true)]
    [SerializeField] private float _movePostDelay = 0f;

    // * 회전 연출 관련 필드
    [BoxGroup("Rotation Value"), LabelText("Value Mode"), ShowIf("_useRotation", true)]
    [SerializeField] private TypeUIAnimationValue _rotationValueMode;
    [Space(5f)]
    [BoxGroup("Rotation Value"), LabelText("Apply Preset Start Rotation"), ToggleLeft, ShowIf("@_useRotation && _rotationValueMode == TypeUIAnimationValue.ABSOLUTE", true)]
    [SerializeField] private bool _rotationApplyPresetStartRotation = false;
    [Space(5f)]
    [BoxGroup("Rotation Value"), LabelText("Start Rotation"), ShowIf("@_useRotation && _rotationValueMode == TypeUIAnimationValue.ABSOLUTE && _rotationApplyPresetStartRotation", true)]
    [SerializeField] private Vector3 _rotationStartRotation;
    [BoxGroup("Rotation Value"), LabelText("End Rotation"), ShowIf("@_useRotation && _rotationValueMode == TypeUIAnimationValue.ABSOLUTE", true)]
    [SerializeField] private Vector3 _rotationEndRotation;
    [BoxGroup("Rotation Value"), LabelText("Offset"), ShowIf("@_useRotation && _rotationValueMode == TypeUIAnimationValue.RELATIVE", true)]
    [SerializeField] private Vector3 _rotationOffset;
    [Space(5f)]
    [BoxGroup("Rotation Value"), LabelText("Rotate Mode"), ShowIf("_useRotation", true)]
    [SerializeField] private RotateMode _rotationMode = RotateMode.Fast;
    [BoxGroup("Rotation Value"), LabelText("Ease"), ShowIf("_useRotation", true)]
    [SerializeField] private Ease _rotationEase = Ease.Linear;
    [BoxGroup("Rotation Value"), LabelText("Duration"), ShowIf("_useRotation", true)]
    [SerializeField] private float _rotationDuration = 0f;
    [Space(5f)]
    [BoxGroup("Rotation Value"), LabelText("Pre Delay"), ShowIf("_useRotation", true)]
    [SerializeField] private float _rotationPreDelay = 0f;
    [BoxGroup("Rotation Value"), LabelText("Post Delay"), ShowIf("_useRotation", true)]
    [SerializeField] private float _rotationPostDelay = 0f;

    // * 스케일 연출 관련 필드
    [BoxGroup("Scale Value"), LabelText("Value Mode"), ShowIf("_useScale", true)]
    [SerializeField] private TypeUIAnimationValue _scaleValueMode;
    [Space(5f)]
    [BoxGroup("Scale Value"), LabelText("Apply Preset Start Scale"), ToggleLeft, ShowIf("@_useScale && _scaleValueMode == TypeUIAnimationValue.ABSOLUTE", true)]
    [SerializeField] private bool _scaleApplyPresetStartScale = false;
    [Space(5f)]
    [BoxGroup("Scale Value"), LabelText("Start Scale"), ShowIf("@_useScale && _scaleValueMode == TypeUIAnimationValue.ABSOLUTE && _scaleApplyPresetStartScale", true)]
    [SerializeField] private Vector3 _scaleStartScale;
    [BoxGroup("Scale Value"), LabelText("End Scale"), ShowIf("@_useScale && _scaleValueMode == TypeUIAnimationValue.ABSOLUTE", true)]
    [SerializeField] private Vector3 _scaleEndScale;
    [BoxGroup("Scale Value"), LabelText("Offset"), ShowIf("@_useScale && _scaleValueMode == TypeUIAnimationValue.RELATIVE", true)]
    [SerializeField] private Vector3 _scaleOffset;
    [Space(5f)]
    [BoxGroup("Scale Value"), LabelText("Ease"), ShowIf("_useScale", true)]
    [SerializeField] private Ease _scaleEase = Ease.Linear;
    [BoxGroup("Scale Value"), LabelText("Duration"), ShowIf("_useScale", true)]
    [SerializeField] private float _scaleDuration = 0f;
    [Space(5f)]
    [BoxGroup("Scale Value"), LabelText("Pre Delay"), ShowIf("_useScale", true)]
    [SerializeField] private float _scalePreDelay = 0f;
    [BoxGroup("Scale Value"), LabelText("Post Delay"), ShowIf("_useScale", true)]
    [SerializeField] private float _scalePostDelay = 0f;

    // * 페이드 연출 관련 필드
    [BoxGroup("Fade Value"), LabelText("Value Mode"), ShowIf("_useFade", true)]
    [SerializeField] private TypeUIAnimationValue _fadeValueMode;
    [Space(5f)]
    [BoxGroup("Fade Value"), LabelText("Apply Preset Start Alpha"), ToggleLeft, ShowIf("@_useFade && _fadeValueMode == TypeUIAnimationValue.ABSOLUTE", true)]
    [SerializeField] private bool _fadeApplyPresetStartAlpha = false;
    [Space(5f)]
    [BoxGroup("Fade Value"), LabelText("Start Alpha"), ShowIf("@_useFade && _fadeValueMode == TypeUIAnimationValue.ABSOLUTE && _fadeApplyPresetStartAlpha", true)]
    [SerializeField] private float _fadeStartAlpha;
    [BoxGroup("Fade Value"), LabelText("End Alpha"), ShowIf("@_useFade && _fadeValueMode == TypeUIAnimationValue.ABSOLUTE", true)]
    [SerializeField] private float _fadeEndAlpha;
    [BoxGroup("Fade Value"), LabelText("Offset"), ShowIf("@_useFade && _fadeValueMode == TypeUIAnimationValue.RELATIVE", true)]
    [SerializeField] private float _fadeOffset;
    [Space(5f)]
    [BoxGroup("Fade Value"), LabelText("Ease"), ShowIf("_useFade", true)]
    [SerializeField] private Ease _fadeEase = Ease.Linear;
    [BoxGroup("Fade Value"), LabelText("Duration"), ShowIf("_useFade", true)]
    [SerializeField] private float _fadeDuration = 0f;
    [Space(5f)]
    [BoxGroup("Fade Value"), LabelText("Pre Delay"), ShowIf("_useFade", true)]
    [SerializeField] private float _fadePreDelay = 0f;
    [BoxGroup("Fade Value"), LabelText("Post Delay"), ShowIf("_useFade", true)]
    [SerializeField] private float _fadePostDelay = 0f;

    // * 이미지 연출 관련 필드
    [BoxGroup("Image Value"), LabelText("Change Sprite"), ToggleLeft, ShowIf("_useImage", true)]
    [SerializeField] private bool _imageChangeSprite = false;
    [BoxGroup("Image Value"), LabelText("Sprite"), ShowIf("@_useImage && _imageChangeSprite", true)]
    [SerializeField] private Sprite _imageSprite;
    [Space(5f)]
    [BoxGroup("Image Value"), LabelText("Change Delta Size"), ToggleLeft, ShowIf("_useImage", true)]
    [SerializeField] private bool _imageChangeDeltaSize = false;
    [BoxGroup("Image Value"), LabelText("Delta Size Mode"), ShowIf("@_useImage && _imageChangeDeltaSize", true)]
    [SerializeField] private TypeUIAnimationImageDeltaSizeMode _imageDeltaSizeMode = TypeUIAnimationImageDeltaSizeMode.SPRITE;
    [BoxGroup("Image Value"), LabelText("Custom Delta Size"), ShowIf("@_useImage && _imageChangeDeltaSize && _imageDeltaSizeMode == TypeUIAnimationImageDeltaSizeMode.CUSTOM", true)]
    [SerializeField] private Vector2 _imageCustomDeltaSize;
    [Space(5f)]
    [BoxGroup("Image Value"), LabelText("Change Color"), ToggleLeft, ShowIf("_useImage", true)]
    [SerializeField] private bool _imageChangeColor = false;
    [BoxGroup("Image Value"), LabelText("Apply Preset Start Color"), ToggleLeft, ShowIf("@_useImage && _imageChangeColor", true)]
    [SerializeField] private bool _imageApplyPresetStartColor = false;
    [BoxGroup("Image Value"), LabelText("Start Color"), ShowIf("@_useImage && _imageChangeColor && _imageApplyPresetStartColor", true)]
    [SerializeField] private Color _imageStartColor = Color.white;
    [BoxGroup("Image Value"), LabelText("End Color"), ShowIf("@_useImage && _imageChangeColor", true)]
    [SerializeField] private Color _imageColor = Color.white;
    [Space(5f)]
    [BoxGroup("Image Value"), LabelText("Ease"), ShowIf("@_useImage && _imageChangeColor", true)]
    [SerializeField] private Ease _imageEase = Ease.Linear;
    [BoxGroup("Image Value"), LabelText("Duration"), ShowIf("@_useImage && _imageChangeColor", true)]
    [SerializeField] private float _imageDuration = 0f;
    [Space(5f)]
    [BoxGroup("Image Value"), LabelText("Pre Delay"), ShowIf("_useImage", true)]
    [SerializeField] private float _imagePreDelay = 0f;
    [BoxGroup("Image Value"), LabelText("Post Delay"), ShowIf("_useImage", true)]
    [SerializeField] private float _imagePostDelay = 0f;

    // * 텍스트 연출 관련 필드
    [BoxGroup("Text Value"), LabelText("Change Text Color"), ToggleLeft, ShowIf("_useText", true)]
    [SerializeField] private bool _textChangeColor = false;
    [BoxGroup("Text Value"), LabelText("Apply Preset Start Text Color"), ToggleLeft, ShowIf("@_useText && _textChangeColor", true)]
    [SerializeField] private bool _textApplyPresetStartColor = false;
    [BoxGroup("Text Value"), LabelText("Start Text Color"), ShowIf("@_useText && _textChangeColor && _textApplyPresetStartColor", true)]
    [SerializeField] private Color _textStartColor = Color.white;
    [BoxGroup("Text Value"), LabelText("End Text Color"), ShowIf("@_useText && _textChangeColor", true)]
    [SerializeField] private Color _textColor = Color.white;
    [Space(5f)]
    [BoxGroup("Text Value"), LabelText("Change Alpha"), ToggleLeft, ShowIf("_useText", true)]
    [SerializeField] private bool _textChangeAlpha = false;
    [BoxGroup("Text Value"), LabelText("Alpha"), ShowIf("@_useText && _textChangeAlpha", true)]
    [SerializeField, Range(0f, 1f)] private float _textAlpha = 1f;
    [Space(5f)]
    [BoxGroup("Text Value"), LabelText("Ease"), ShowIf("_useText", true)]
    [SerializeField] private Ease _textEase = Ease.Linear;
    [BoxGroup("Text Value"), LabelText("Duration"), ShowIf("_useText", true)]
    [SerializeField] private float _textDuration = 0f;
    [Space(5f)]
    [BoxGroup("Text Value"), LabelText("Pre Delay"), ShowIf("_useText", true)]
    [SerializeField] private float _textPreDelay = 0f;
    [BoxGroup("Text Value"), LabelText("Post Delay"), ShowIf("_useText", true)]
    [SerializeField] private float _textPostDelay = 0f;

    // --------------------------------------------------------------------------
    //  ● Read only properties
    // --------------------------------------------------------------------------
    public string Label => string.IsNullOrWhiteSpace(_label) ? name : _label;
    public UIAnimationReferenceHub Target => _target;
    public TypeUIAnimationSequence SequenceMode => _sequenceMode;
    public float EstimatedDuration => CalculateEstimatedDuration();
#if UNITY_EDITOR
    public bool RequiresRectTransformPreviewSnapshot => _useMove || _useRotation || _useScale || (_useImage && _imageChangeDeltaSize);
    public bool RequiresCanvasGroupPreviewSnapshot => _useFade;
    public bool RequiresImageSpritePreviewSnapshot => HasImageSpriteContent();
    public bool RequiresImageColorPreviewSnapshot => _useImage && _imageChangeColor;
    public bool RequiresTextColorPreviewSnapshot => _useText && HasTextContent();
#endif

    // --------------------------------------------------------------------------
    //  ● Public Methods
    // --------------------------------------------------------------------------
    public Tween CreateTween()
    {
        Sequence sequence = DOTween.Sequence();
        sequence.SetTarget(this);
        sequence.Pause();

        bool hasContent = false;

        JoinContent(sequence, CreateMoveTween(), ref hasContent);
        JoinContent(sequence, CreateRotationTween(), ref hasContent);
        JoinContent(sequence, CreateScaleTween(), ref hasContent);
        JoinContent(sequence, CreateFadeTween(), ref hasContent);
        JoinContent(sequence, CreateImageTween(), ref hasContent);
        JoinContent(sequence, CreateTextTween(), ref hasContent);

        if (!hasContent)
        {
            sequence.AppendCallback(() => { });
        }

        return sequence;
    }

    // --------------------------------------------------------------------------
    //  ● Private Methods
    // --------------------------------------------------------------------------
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

    private Tween CreateTextTween()
    {
        if (!_useText || !TryGetTMPText(out TMP_Text tmpText))
        {
            return null;
        }

        if (!HasTextContent())
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

    private bool HasImageSpriteContent()
    {
        return _useImage && _imageChangeSprite && _imageSprite != null;
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
