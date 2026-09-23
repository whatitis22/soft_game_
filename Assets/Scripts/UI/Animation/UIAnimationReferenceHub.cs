using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sirenix.OdinInspector;

public class UIAnimationReferenceHub : MonoBehaviour
{
    [TitleGroup("Basic Component")]
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private CanvasGroup _canvasGroup;

    [TitleGroup("Additional Component")]
    [SerializeField] private TMP_Text _tmpText;
    [SerializeField] private Image _image;

    public RectTransform RectTransform => _rectTransform != null ? _rectTransform : transform as RectTransform;
    public CanvasGroup CanvasGroup => _canvasGroup != null ? _canvasGroup : GetComponent<CanvasGroup>();

    public TMP_Text TMPText => _tmpText != null ? _tmpText : GetComponent<TMP_Text>();
    public Image Image => _image != null ? _image : GetComponent<Image>();

    private void Reset()
    {
        CacheMissingReferences();
    }

    private void OnValidate()
    {
        CacheMissingReferences();
    }

    private void CacheMissingReferences()
    {
        if (_rectTransform == null)
        {
            _rectTransform = transform as RectTransform;
        }

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        if (_tmpText == null)
        {
            _tmpText = GetComponent<TMP_Text>();
        }

        if (_image == null)
        {
            _image = GetComponent<Image>();
        }
    }
}
