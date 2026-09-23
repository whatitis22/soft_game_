using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Sirenix.OdinInspector;
using DG.Tweening;

public partial class UIAnimationHandler : MonoBehaviour, IPlayableMedia
{
    private Sequence _currentSequence;
    private UIAnimationState _state = UIAnimationState.INACTIVE;
    private readonly List<Tween> _exceptTweens = new List<Tween>();

    [TitleGroup("Preset"), LabelText("AnimationPreset")]
    [SerializeField] private List<UIAnimationStep> _animationPreset = new List<UIAnimationStep>();

    [TitleGroup("Event")]
    [SerializeField] private UnityEvent _onPlay = new UnityEvent();
    [SerializeField] private UnityEvent _onPause = new UnityEvent();
    [SerializeField] private UnityEvent _onResume = new UnityEvent();
    [SerializeField] private UnityEvent _onStop = new UnityEvent();
    [SerializeField] private UnityEvent _onReplay = new UnityEvent();
    [SerializeField] private UnityEvent _onComplete = new UnityEvent();
    [SerializeField] private UnityEvent _onKill = new UnityEvent();

    public Sequence CurrentSequence => _currentSequence;
    public UIAnimationState State => _state;
    public IReadOnlyList<UIAnimationStep> AnimationPreset => _animationPreset;

    public UnityEvent OnPlay => _onPlay;
    public UnityEvent OnPause => _onPause;
    public UnityEvent OnResume => _onResume;
    public UnityEvent OnStop => _onStop;
    public UnityEvent OnReplay => _onReplay;
    public UnityEvent OnComplete => _onComplete;
    public UnityEvent OnKill => _onKill;

    public bool IsActive => HasActiveAnimation();
    public bool IsPlaying => _state == UIAnimationState.PLAYING;
    public bool IsPaused => _state == UIAnimationState.PAUSED;

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            StopEditorPreview(true);
            return;
        }
#endif

        if (HasActiveAnimation())
        {
            Interrupt(false);
        }
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            StopEditorPreview(true);
        }
#endif
    }

    [PropertyOrder(-89)]
    [TitleGroup("Runtime"), Button]
    public void Play()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            PlayEditorPreview();
            return;
        }
#endif

        bool hasActiveAnimation = HasActiveAnimation();

        if (hasActiveAnimation)
        {
            Interrupt(false);
        }

        StartPreset();
    }

    [PropertyOrder(-88)]
    [TitleGroup("Runtime"), Button]
    public void Pause()
    {
        if (_state != UIAnimationState.PLAYING)
        {
            return;
        }

        _currentSequence?.Pause();
        PauseExceptTweens();

        _state = UIAnimationState.PAUSED;
        _onPause?.Invoke();
    }

    [PropertyOrder(-87)]
    [TitleGroup("Runtime"), Button]
    public void Resume()
    {
        if (_state != UIAnimationState.PAUSED)
        {
            return;
        }

        _currentSequence?.Play();
        ResumeExceptTweens();

        _state = UIAnimationState.PLAYING;
        _onResume?.Invoke();
    }

    [PropertyOrder(-86)]
    [TitleGroup("Runtime"), Button]
    public void Stop()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            StopEditorPreview(true);
            return;
        }
#endif

        if (!HasActiveAnimation())
        {
            return;
        }

        StopCurrentSequence();
        StopExceptTweens();

        _state = UIAnimationState.STOPPED;
        _onStop?.Invoke();
    }

    [PropertyOrder(-85)]
    [TitleGroup("Runtime"), Button]
    public void Replay()
    {
        bool hasActiveAnimation = HasActiveAnimation();

        if (hasActiveAnimation)
        {
            Interrupt(false);
        }

        _onReplay?.Invoke();
        StartPreset();
    }

    [PropertyOrder(-84)]
    [TitleGroup("Runtime"), Button]
    public void Complete()
    {
        if (!HasActiveAnimation())
        {
            return;
        }

        bool hasSequence = _currentSequence != null && _currentSequence.IsActive();

        _currentSequence?.Complete(true);
        CompleteExceptTweens();

        if (!hasSequence)
        {
            CompleteAnimation();
        }
    }

    [PropertyOrder(-83)]
    [TitleGroup("Runtime"), Button]
    public void Kill()
    {
        if (!HasActiveAnimation())
        {
            return;
        }

        KillCurrentSequence();
        KillExceptTweens();

        _state = UIAnimationState.KILLED;
        _onKill?.Invoke();
    }
}
