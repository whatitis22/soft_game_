#if UNITY_EDITOR
using UnityEngine;
using TMPro;
using DG.Tweening;

public partial class UIAnimationHandler
{
    private static UIAnimationHandler _activeEditorPreviewHandler;
    private readonly System.Collections.Generic.List<EditorPreviewSnapshot> _editorPreviewSnapshots =
        new System.Collections.Generic.List<EditorPreviewSnapshot>();

    private void PlayEditorPreview()
    {
        if (_activeEditorPreviewHandler != null && _activeEditorPreviewHandler != this)
        {
            _activeEditorPreviewHandler.StopEditorPreview(true);
        }

        StopEditorPreview(true);
        CaptureEditorPreviewTargets();

        _currentSequence = CreateSequence();
        _state = UIAnimationState.PLAYING;
        _activeEditorPreviewHandler = this;

        _onPlay?.Invoke();
        PrepareTweenForEditorPreview(_currentSequence, false, true, true);
        UnityEditor.EditorApplication.hierarchyChanged -= StopActiveEditorPreviewOnHierarchyChanged;
        UnityEditor.EditorApplication.hierarchyChanged += StopActiveEditorPreviewOnHierarchyChanged;
        StartEditorPreview(null);
    }

    private void StopEditorPreview(bool resetTargets)
    {
        StopDotweenEditorPreview(false, true);

        if (resetTargets)
        {
            RestoreEditorPreviewTargets();
        }

        _currentSequence = null;
        _exceptTweens.Clear();
        _editorPreviewSnapshots.Clear();

        if (_activeEditorPreviewHandler == this)
        {
            _activeEditorPreviewHandler = null;
            UnityEditor.EditorApplication.hierarchyChanged -= StopActiveEditorPreviewOnHierarchyChanged;
        }

        _state = UIAnimationState.STOPPED;
        RepaintEditorViews();
    }

    private void CaptureEditorPreviewTargets()
    {
        _editorPreviewSnapshots.Clear();

        for (int i = 0; i < _animationPreset.Count; i++)
        {
            UIAnimationStep step = _animationPreset[i];

            if (step == null || step.Target == null)
            {
                continue;
            }

            AddEditorPreviewSnapshot(step);
        }
    }

    private void AddEditorPreviewSnapshot(UIAnimationStep step)
    {
        UIAnimationReferenceHub target = step.Target;
        bool captureRectTransform = step.RequiresRectTransformPreviewSnapshot;
        bool captureCanvasGroup = step.RequiresCanvasGroupPreviewSnapshot;
        bool captureImageSprite = step.RequiresImageSpritePreviewSnapshot;
        bool captureImageColor = step.RequiresImageColorPreviewSnapshot;
        bool captureTextColor = step.RequiresTextColorPreviewSnapshot;

        if (!captureRectTransform && !captureCanvasGroup && !captureImageSprite && !captureImageColor && !captureTextColor)
        {
            return;
        }

        RectTransform rectTransform = target.RectTransform;
        CanvasGroup canvasGroup = target.CanvasGroup;
        UnityEngine.UI.Image image = target.Image;
        TMP_Text tmpText = target.TMPText;

        for (int i = 0; i < _editorPreviewSnapshots.Count; i++)
        {
            if (_editorPreviewSnapshots[i].Matches(rectTransform, canvasGroup, image, tmpText))
            {
                _editorPreviewSnapshots[i].Include(
                    captureRectTransform,
                    captureCanvasGroup,
                    captureImageSprite,
                    captureImageColor,
                    captureTextColor);
                return;
            }
        }

        _editorPreviewSnapshots.Add(new EditorPreviewSnapshot(
            rectTransform,
            canvasGroup,
            image,
            tmpText,
            captureRectTransform,
            captureCanvasGroup,
            captureImageSprite,
            captureImageColor,
            captureTextColor));
    }

    private void RestoreEditorPreviewTargets()
    {
        for (int i = 0; i < _editorPreviewSnapshots.Count; i++)
        {
            _editorPreviewSnapshots[i].Restore();
        }
    }

    private static void PrepareTweenForEditorPreview(Tween tween, bool clearCallbacks, bool preventAutoKill, bool andPlay)
    {
        if (tween == null)
        {
            return;
        }

        System.Type previewType = GetDotweenEditorPreviewType();

        if (previewType == null)
        {
            Debug.LogWarning("DOTween editor preview API를 찾을 수 없습니다.");
            tween.Play();
            return;
        }

        System.Reflection.MethodInfo method = previewType.GetMethod(
            "PrepareTweenForPreview",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(Tween), typeof(bool), typeof(bool), typeof(bool) },
            null);

        method?.Invoke(null, new object[] { tween, clearCallbacks, preventAutoKill, andPlay });
    }

    private static void StartEditorPreview(System.Action onUpdated)
    {
        System.Type previewType = GetDotweenEditorPreviewType();

        if (previewType == null)
        {
            return;
        }

        System.Action repaintAction = () =>
        {
            onUpdated?.Invoke();
            RepaintEditorViews();
        };

        System.Reflection.MethodInfo method = previewType.GetMethod(
            "Start",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(System.Action) },
            null);

        method?.Invoke(null, new object[] { repaintAction });
    }

    private static void StopDotweenEditorPreview(bool resetTweenTargets, bool clearTweens)
    {
        System.Type previewType = GetDotweenEditorPreviewType();

        if (previewType == null)
        {
            return;
        }

        System.Reflection.MethodInfo method = previewType.GetMethod(
            "Stop",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            new[] { typeof(bool), typeof(bool) },
            null);

        method?.Invoke(null, new object[] { resetTweenTargets, clearTweens });
    }

    private static System.Type GetDotweenEditorPreviewType()
    {
        System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();

        for (int i = 0; i < assemblies.Length; i++)
        {
            System.Type previewType = assemblies[i].GetType("DG.DOTweenEditor.DOTweenEditorPreview");

            if (previewType != null)
            {
                return previewType;
            }
        }

        return null;
    }

    private static void RepaintEditorViews()
    {
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
    }

    private static void StopActiveEditorPreviewOnHierarchyChanged()
    {
        if (_activeEditorPreviewHandler == null)
        {
            UnityEditor.EditorApplication.hierarchyChanged -= StopActiveEditorPreviewOnHierarchyChanged;
            return;
        }

        if (_activeEditorPreviewHandler.HasMissingEditorPreviewTarget())
        {
            _activeEditorPreviewHandler.StopEditorPreview(true);
        }
    }

    private bool HasMissingEditorPreviewTarget()
    {
        for (int i = 0; i < _editorPreviewSnapshots.Count; i++)
        {
            if (_editorPreviewSnapshots[i].HasMissingCapturedTarget())
            {
                return true;
            }
        }

        return false;
    }

}
#endif
