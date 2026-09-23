using UnityEngine.SceneManagement;
using DG.Tweening;

public partial class UIAnimationHandler
{
    private bool HasActiveAnimation()
    {
        if (_currentSequence != null && _currentSequence.IsActive())
        {
            return true;
        }

        for (int i = _exceptTweens.Count - 1; i >= 0; i--)
        {
            Tween tween = _exceptTweens[i];

            if (tween != null && tween.IsActive())
            {
                return true;
            }
        }

        return false;
    }

    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        if (HasActiveAnimation())
        {
            Interrupt(false);
        }
    }
}
