using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sirenix.OdinInspector;

// ==============================================================================
//  ■ UIAnimationReferenceHub
// ------------------------------------------------------------------------------
//  UI 하나의 수많은 레퍼런스들을 모아둔 허브
// ==============================================================================

public class UIAnimationReferenceHub : MonoBehaviour
{
    // --------------------------------------------------------------------------
    //  ● Private Fields
    // --------------------------------------------------------------------------
    [TitleGroup("Basic Component")]
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private CanvasGroup _canvasGroup;

    [TitleGroup("Additional Component")]
    [SerializeField] private TMP_Text _tmpText;
    [SerializeField] private Image _image;

    // --------------------------------------------------------------------------
    //  ● Read only properties
    // --------------------------------------------------------------------------
    public RectTransform RectTransform => _rectTransform != null ? _rectTransform : transform as RectTransform;
    public CanvasGroup CanvasGroup => _canvasGroup != null ? _canvasGroup : GetComponent<CanvasGroup>();

    public TMP_Text TMPText => _tmpText != null ? _tmpText : GetComponent<TMP_Text>();
    public Image Image => _image != null ? _image : GetComponent<Image>();

    // --------------------------------------------------------------------------
    //  ● Unity Events
    // --------------------------------------------------------------------------
    private void Reset()
    {
        CacheMissingReferences();
    }

    private void OnValidate()
    {
        CacheMissingReferences();
    }

    // --------------------------------------------------------------------------
    //  ● Private Methods
    // --------------------------------------------------------------------------
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
