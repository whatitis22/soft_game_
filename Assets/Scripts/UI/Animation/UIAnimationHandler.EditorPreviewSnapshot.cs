#if UNITY_EDITOR
using UnityEngine;
using TMPro;

public partial class UIAnimationHandler
{
    private sealed class EditorPreviewSnapshot
    {
        private readonly RectTransform _rectTransform;
        private readonly Vector3 _anchoredPosition;
        private readonly Vector2 _sizeDelta;
        private readonly Vector3 _localEulerAngles;
        private readonly Vector3 _localScale;

        private readonly CanvasGroup _canvasGroup;
        private readonly float _alpha;

        private readonly UnityEngine.UI.Image _image;
        private readonly Sprite _sprite;
        private readonly Color _color;

        private readonly TMP_Text _tmpText;
        private readonly Color _textColor;

        private bool _captureRectTransform;
        private bool _captureCanvasGroup;
        private bool _captureImageSprite;
        private bool _captureImageColor;
        private bool _captureTextColor;

        public EditorPreviewSnapshot(
            RectTransform rectTransform,
            CanvasGroup canvasGroup,
            UnityEngine.UI.Image image,
            TMP_Text tmpText,
            bool captureRectTransform,
            bool captureCanvasGroup,
            bool captureImageSprite,
            bool captureImageColor,
            bool captureTextColor)
        {
            _rectTransform = rectTransform;
            _anchoredPosition = rectTransform != null ? rectTransform.anchoredPosition3D : Vector3.zero;
            _sizeDelta = rectTransform != null ? rectTransform.sizeDelta : Vector2.zero;
            _localEulerAngles = rectTransform != null ? rectTransform.localEulerAngles : Vector3.zero;
            _localScale = rectTransform != null ? rectTransform.localScale : Vector3.one;

            _canvasGroup = canvasGroup;
            _alpha = canvasGroup != null ? canvasGroup.alpha : 1f;

            _image = image;
            _sprite = image != null ? image.sprite : null;
            _color = image != null ? image.color : Color.white;

            _tmpText = tmpText;
            _textColor = tmpText != null ? tmpText.color : Color.white;

            _captureRectTransform = captureRectTransform;
            _captureCanvasGroup = captureCanvasGroup;
            _captureImageSprite = captureImageSprite;
            _captureImageColor = captureImageColor;
            _captureTextColor = captureTextColor;
        }

        public bool Matches(RectTransform rectTransform, CanvasGroup canvasGroup, UnityEngine.UI.Image image, TMP_Text tmpText)
        {
            return _rectTransform == rectTransform && _canvasGroup == canvasGroup && _image == image && _tmpText == tmpText;
        }

        public bool HasMissingCapturedTarget()
        {
            return (_captureRectTransform && _rectTransform == null)
                || (_captureCanvasGroup && _canvasGroup == null)
                || ((_captureImageSprite || _captureImageColor) && _image == null)
                || (_captureTextColor && _tmpText == null);
        }

        public void Include(
            bool captureRectTransform,
            bool captureCanvasGroup,
            bool captureImageSprite,
            bool captureImageColor,
            bool captureTextColor)
        {
            _captureRectTransform |= captureRectTransform;
            _captureCanvasGroup |= captureCanvasGroup;
            _captureImageSprite |= captureImageSprite;
            _captureImageColor |= captureImageColor;
            _captureTextColor |= captureTextColor;
        }

        public void Restore()
        {
            if (_captureRectTransform && _rectTransform != null)
            {
                _rectTransform.anchoredPosition3D = _anchoredPosition;
                _rectTransform.sizeDelta = _sizeDelta;
                _rectTransform.localEulerAngles = _localEulerAngles;
                _rectTransform.localScale = _localScale;
            }

            if (_captureCanvasGroup && _canvasGroup != null)
            {
                _canvasGroup.alpha = _alpha;
            }

            if (_image != null)
            {
                if (_captureImageSprite)
                {
                    _image.sprite = _sprite;
                }

                if (_captureImageColor)
                {
                    _image.color = _color;
                }
            }

            if (_captureTextColor && _tmpText != null)
            {
                _tmpText.color = _textColor;
                _tmpText.SetVerticesDirty();
            }
        }
    }
}
#endif
