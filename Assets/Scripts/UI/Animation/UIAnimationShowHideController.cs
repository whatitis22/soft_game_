using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;

public class UIAnimationShowHideController : MonoBehaviour
{
    [TitleGroup("Event")]
    [SerializeField] private UnityEvent _onShow = new UnityEvent();
    [SerializeField] private UnityEvent _onHide = new UnityEvent();

    [TitleGroup("Animation")]
    [SerializeField] private UIAnimationHandler _showHandler;
    [SerializeField] private UIAnimationHandler _hideHandler;

    public UnityEvent OnShow => _onShow;
    public UnityEvent OnHide => _onHide;
    public UIAnimationHandler ShowHandler => _showHandler;
    public UIAnimationHandler HideHandler => _hideHandler;

    [PropertyOrder(-88)]
    [TitleGroup("Runtime"), Button]
    public void Show()
    {
        _onShow?.Invoke();
        _showHandler?.Play();
    }

    [PropertyOrder(-87)]
    [TitleGroup("Runtime"), Button]
    public void Hide()
    {
        _onHide?.Invoke();
        _hideHandler?.Play();
    }

    [PropertyOrder(-86)]
    [TitleGroup("Runtime"), Button]
    public void ShowImmediate()
    {
        _onShow?.Invoke();
        PlayAndComplete(_showHandler);
    }

    [PropertyOrder(-85)]
    [TitleGroup("Runtime"), Button]
    public void HideImmediate()
    {
        _onHide?.Invoke();
        PlayAndComplete(_hideHandler);
    }

    private void PlayAndComplete(UIAnimationHandler handler)
    {
        if (handler == null)
        {
            return;
        }

        handler.Play();
        handler.Complete();
    }
}
