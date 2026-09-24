using System;
using UnityEngine;
using Sirenix.OdinInspector;
using DG.Tweening;

[Serializable]
public partial class UIAnimationStep : MonoBehaviour
{
    [TitleGroup("Runtime")]
    [SerializeField] private UIAnimationReferenceHub _target;
    [SerializeField] private TypeUIAnimationSequence _sequenceMode;

    [TitleGroup("Content")]
    [HorizontalGroup("Content/Row1"), ToggleLeft]
    [SerializeField] private bool _useMove = false;
    [HorizontalGroup("Content/Row1"), ToggleLeft]
    [SerializeField] private bool _useFade = false;
    [HorizontalGroup("Content/Row2"), ToggleLeft]
    [SerializeField] private bool _useRotation = false;
    [HorizontalGroup("Content/Row2"), ToggleLeft]
    [SerializeField] private bool _useImage = false;
    [HorizontalGroup("Content/Row3"), ToggleLeft]
    [SerializeField] private bool _useScale = false;
    [HorizontalGroup("Content/Row3"), ToggleLeft]
    [SerializeField] private bool _useText = false;
    [Space(5f)]

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
}
