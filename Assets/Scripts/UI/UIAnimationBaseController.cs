using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Sirenix.OdinInspector;

// ==============================================================================
//  ■ UIAnimationBaseController
// ------------------------------------------------------------------------------
//  최상위 UI 오브젝트에 붙여 자식 UIAnimationHandler들을 의미 단위로 관리하는 베이스 컨트롤러
// ==============================================================================

public class UIAnimationBaseController : SerializedMonoBehaviour, IPlayableMedia
{
    // --------------------------------------------------------------------------
    //  ● Title
    // --------------------------------------------------------------------------
    [PropertyOrder(-100)]
    [TitleGroup("Title")]
    [SerializeField] private string _label; // 라벨 이름. 언제든지 수정 가능

    // --------------------------------------------------------------------------
    //  ● Runtime
    // --------------------------------------------------------------------------
    [PropertyOrder(-90)]
    [TitleGroup("Runtime"), ShowInInspector, ReadOnly]
    private UIAnimationDisplayState _state = UIAnimationDisplayState.HIDDEN;

    private UIAnimationHandler _currentHandler;
    private UIAnimationDisplayState _lastAnimationState = UIAnimationDisplayState.SHOWING;

    // --------------------------------------------------------------------------
    //  ● Event
    // --------------------------------------------------------------------------
    [TitleGroup("Event")]
    [SerializeField] private UnityEvent _onPlay = new UnityEvent();
    [SerializeField] private UnityEvent _onShow = new UnityEvent();
    [SerializeField] private UnityEvent _onHide = new UnityEvent();

    // --------------------------------------------------------------------------
    //  ● Debug Monitor
    // --------------------------------------------------------------------------
    [TitleGroup("Debug Monitor")]
    [SerializeField] private bool _debugMode = false; // 체크박스 활성화 시, 밑의 것들이 보임

    private bool IsDebugMode => _debugMode;

    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), ShowInInspector, ReadOnly]
    private bool _isActive; // 표시 상태 기준으로 활성화돼있나요?
    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), ShowInInspector, ReadOnly]
    private bool _isPlaying; // 재생 중 인가요?
    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), ShowInInspector, ReadOnly]
    private bool _isPaused; // 일시 정지인가요?

    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), ShowInInspector, ReadOnly]
    private string _currentHandlerName; // 현재 재생 중인 Handler
    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), ShowInInspector, ReadOnly]
    private string _currentPresetName; // 현재 재생 중인 Preset
    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), ShowInInspector, ReadOnly]
    private string _currentStepName; // 현재 재생 중인 Step

    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), ShowInInspector, ReadOnly]
    private float _finalDuration; // 최종적인 연출 길이를 자동으로 합산하여 보여줌
    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), ShowInInspector, ReadOnly]
    private UIAnimationDurationGrade _durationGrade; // _finalDuration 에 따른 연출 평가

    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), ShowInInspector, ReadOnly]
    private float _elapsedDuration; // 현재 Animation이 시작된 뒤 경과한 시간
    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), ShowInInspector, ReadOnly]
    private float _remainingDuration; // 예상 남은 시간
    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), ShowInInspector, ReadOnly]
    private float _normalizedProgress; // 전체 진행도 (0.0 ~ 1.0)
    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), ShowInInspector, ReadOnly]
    private int _activeTweenCount; // 현재 살아있는 Tween 연출 개수

    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), ShowInInspector, ReadOnly]
    private string _lastCompletedHandlerName; // 직전에 정상 종료된 Handler
    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), ShowInInspector, ReadOnly]
    private string _lastCompletedPresetName; // 직전에 정상 종료된 Animation
    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), ShowInInspector, ReadOnly]
    private string _lastCompletedStepName; // 직전에 정상 종료된 Step
    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), ShowInInspector, ReadOnly]
    private UIAnimationInterruptReason _lastInterruptReason = UIAnimationInterruptReason.NONE; // 중간 취소 및 강제 종료의 원인 표시

    // --------------------------------------------------------------------------
    //  ● References
    // --------------------------------------------------------------------------
    [TitleGroup("References")]
    [SerializeField] private List<UIAnimationHandler> _handlers = new List<UIAnimationHandler>(); // 사용할 자식 핸들러들

    // --------------------------------------------------------------------------
    //  ● Read only properties
    // --------------------------------------------------------------------------
    public virtual string Label
    {
        get => string.IsNullOrWhiteSpace(_label) ? name : _label;
        set => _label = value;
    }

    public virtual UIAnimationDisplayState State => _state;
    public virtual UIAnimationHandler CurrentHandler => _currentHandler;
    public virtual IReadOnlyList<UIAnimationHandler> Handlers => _handlers;

    public virtual UnityEvent OnPlay => _onPlay;
    public virtual UnityEvent OnShow => _onShow;
    public virtual UnityEvent OnHide => _onHide;

    public virtual bool IsActive => _isActive;
    public virtual bool IsPlaying => _isPlaying;
    public virtual bool IsPaused => _isPaused;
    public virtual string CurrentHandlerName => _currentHandlerName;
    public virtual string CurrentPresetName => _currentPresetName;
    public virtual string CurrentStepName => _currentStepName;
    public virtual float FinalDuration => _finalDuration;
    public virtual UIAnimationDurationGrade DurationGrade => _durationGrade;
    public virtual float ElapsedDuration => _elapsedDuration;
    public virtual float RemainingDuration => _remainingDuration;
    public virtual float NormalizedProgress => _normalizedProgress;
    public virtual int ActiveTweenCount => _activeTweenCount;
    public virtual string LastCompletedHandlerName => _lastCompletedHandlerName;
    public virtual string LastCompletedPresetName => _lastCompletedPresetName;
    public virtual string LastCompletedStepName => _lastCompletedStepName;
    public virtual UIAnimationInterruptReason LastInterruptReason => _lastInterruptReason;

    // --------------------------------------------------------------------------
    //  ● Unity Events
    // --------------------------------------------------------------------------
    protected virtual void Reset()
    {
        RefreshHandlersButton();
    }

    protected virtual void Awake()
    {
        if (_handlers.Count == 0)
        {
            RefreshHandlersButton();
        }

        RefreshRuntimeMonitor();
    }

    protected virtual void OnEnable()
    {
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
    }

    protected virtual void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

        if (HasActiveAnimation())
        {
            InterruptActiveHandlers(UIAnimationInterruptReason.OBJECT_DISABLED);
        }
    }

    protected virtual void OnDestroy()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
    }

    protected virtual void Update()
    {
        RefreshRuntimeMonitor();
    }

    // --------------------------------------------------------------------------
    //  ● Public Methods
    // --------------------------------------------------------------------------
    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), Button]
    public virtual void Play()
    {
        bool hadActiveAnimation = HasActiveAnimation();

        if (hadActiveAnimation)
        {
            InterruptActiveHandlers(UIAnimationInterruptReason.REPLACED);
        }
        else
        {
            _lastInterruptReason = UIAnimationInterruptReason.NONE;
        }

        _onPlay?.Invoke();
        ResolveCurrentHandler();
        RefreshRuntimeMonitor();
    }

    [PropertyOrder(-88)]
    [TitleGroup("Runtime"), Button]
    public virtual void Show()
    {
        BeginSemanticAnimation(UIAnimationDisplayState.SHOWING, _onShow);
    }

    [PropertyOrder(-87)]
    [TitleGroup("Runtime"), Button]
    public virtual void Hide()
    {
        BeginSemanticAnimation(UIAnimationDisplayState.HIDING, _onHide);
    }

    [PropertyOrder(-86)]
    [TitleGroup("Runtime"), Button]
    public virtual void ShowImmediate()
    {
        Show();
        Complete();
    }

    [PropertyOrder(-85)]
    [TitleGroup("Runtime"), Button]
    public virtual void HideImmediate()
    {
        Hide();
        Complete();
    }

    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), Button]
    public virtual void Pause()
    {
        ApplyToActiveHandlers(handler => handler.Pause());
        RefreshRuntimeMonitor();
    }

    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), Button]
    public virtual void Resume()
    {
        ApplyToHandlers(handler =>
        {
            if (handler.IsPaused)
            {
                handler.Resume();
            }
        });

        RefreshRuntimeMonitor();
    }

    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), Button]
    public virtual void Stop()
    {
        if (!HasActiveAnimation())
        {
            return;
        }

        ApplyToActiveHandlers(handler => handler.Stop());
        _lastInterruptReason = UIAnimationInterruptReason.CANCELLED;

        if (_state == UIAnimationDisplayState.SHOWING)
        {
            SetDisplayState(UIAnimationDisplayState.HIDDEN);
        }
        else if (_state == UIAnimationDisplayState.HIDING)
        {
            SetDisplayState(UIAnimationDisplayState.SHOWN);
        }

        RefreshRuntimeMonitor();
    }

    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), Button]
    public virtual void Replay()
    {
        if (_currentHandler == null)
        {
            Play();
            return;
        }

        if (HasActiveAnimation())
        {
            InterruptActiveHandlers(UIAnimationInterruptReason.REPLACED);
        }

        if (_lastAnimationState == UIAnimationDisplayState.SHOWING || _lastAnimationState == UIAnimationDisplayState.HIDING)
        {
            SetDisplayState(_lastAnimationState);
        }

        _currentHandler.Replay();
        RefreshRuntimeMonitor();
    }

    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), Button]
    public virtual void Complete()
    {
        if (!HasActiveAnimation())
        {
            CompleteDisplayState();
            RefreshRuntimeMonitor();
            return;
        }

        ApplyToActiveHandlers(handler => handler.Complete());
        CompleteDisplayState();
        RefreshRuntimeMonitor();
    }

    [TitleGroup("Debug Monitor"), ShowIf(nameof(IsDebugMode)), Button]
    public virtual void Kill()
    {
        if (!HasActiveAnimation())
        {
            return;
        }

        InterruptActiveHandlers(UIAnimationInterruptReason.CANCELLED);
        RefreshRuntimeMonitor();
    }

    [TitleGroup("References"), Button]
    public virtual void RefreshHandlersButton()
    {
        _handlers.Clear();
        GetComponentsInChildren(true, _handlers);
    }

#if UNITY_EDITOR
    [PropertyOrder(-84)]
    [TitleGroup("Runtime"), Button("Stop Editor Preview")]
    private void StopEditorPreviewButton()
    {
        ApplyToHandlers(handler => handler.Stop());
        RefreshRuntimeMonitor();
    }
#endif

    // --------------------------------------------------------------------------
    //  ● Protected Methods
    // --------------------------------------------------------------------------
    protected virtual void BeginSemanticAnimation(UIAnimationDisplayState animationState, UnityEvent animationEvent)
    {
        bool hadActiveAnimation = HasActiveAnimation();

        if (hadActiveAnimation)
        {
            InterruptActiveHandlers(UIAnimationInterruptReason.REPLACED);
        }
        else
        {
            _lastInterruptReason = UIAnimationInterruptReason.NONE;
        }

        _lastAnimationState = animationState;
        SetDisplayState(animationState);

        animationEvent?.Invoke();

        ResolveCurrentHandler();
        RefreshRuntimeMonitor();
    }

    protected virtual void SetDisplayState(UIAnimationDisplayState state)
    {
        _state = state;
    }

    protected virtual void CompleteDisplayState()
    {
        if (_state == UIAnimationDisplayState.SHOWING)
        {
            SetDisplayState(UIAnimationDisplayState.SHOWN);
        }
        else if (_state == UIAnimationDisplayState.HIDING)
        {
            SetDisplayState(UIAnimationDisplayState.HIDDEN);
        }
    }

    protected virtual void RefreshRuntimeMonitor()
    {
        ResolveCurrentHandler();
        CompleteDisplayStateIfAnimationEnded();

        _isPlaying = HasPlayingHandler();
        _isPaused = HasPausedHandler();
        _activeTweenCount = CountActiveTweens();
        _isActive = _state == UIAnimationDisplayState.SHOWING
            || _state == UIAnimationDisplayState.SHOWN
            || _activeTweenCount > 0;

        RefreshCurrentHandlerMonitor();
    }

    protected virtual void ResolveCurrentHandler()
    {
        if (_currentHandler != null && !_handlers.Contains(_currentHandler))
        {
            _currentHandler = null;
        }

        UIAnimationHandler activeHandler = FindActiveHandler();

        if (activeHandler != null)
        {
            _currentHandler = activeHandler;
        }
    }

    // --------------------------------------------------------------------------
    //  ● Private Methods
    // --------------------------------------------------------------------------
    private void RefreshCurrentHandlerMonitor()
    {
        UIAnimationHandler monitorHandler = _currentHandler;

        if (monitorHandler == null)
        {
            _currentHandlerName = string.Empty;
            _currentPresetName = string.Empty;
            _currentStepName = string.Empty;
            _finalDuration = CalculateMaxHandlerDuration();
            _durationGrade = GetDurationGrade(_finalDuration);
            _elapsedDuration = 0f;
            _remainingDuration = _finalDuration;
            _normalizedProgress = 0f;
            return;
        }

        _currentHandlerName = monitorHandler.Label;
        _currentPresetName = monitorHandler.CurrentPresetName;
        _currentStepName = monitorHandler.CurrentStepName;
        _finalDuration = monitorHandler.FinalDuration;
        _durationGrade = monitorHandler.DurationGrade;
        _elapsedDuration = monitorHandler.ElapsedDuration;
        _remainingDuration = monitorHandler.RemainingDuration;
        _normalizedProgress = monitorHandler.NormalizedProgress;

        if (monitorHandler.LastInterruptReason != UIAnimationInterruptReason.NONE)
        {
            _lastInterruptReason = monitorHandler.LastInterruptReason;
        }

        if (monitorHandler.State == UIAnimationState.COMPLETED)
        {
            _lastCompletedHandlerName = monitorHandler.Label;
            _lastCompletedPresetName = monitorHandler.LastCompletedPresetName;
            _lastCompletedStepName = monitorHandler.LastCompletedStepName;
        }
    }

    private void CompleteDisplayStateIfAnimationEnded()
    {
        if (_state != UIAnimationDisplayState.SHOWING && _state != UIAnimationDisplayState.HIDING)
        {
            return;
        }

        if (HasActiveAnimation())
        {
            return;
        }

        CompleteDisplayState();
    }

    private UIAnimationHandler FindActiveHandler()
    {
        for (int i = 0; i < _handlers.Count; i++)
        {
            UIAnimationHandler handler = _handlers[i];

            if (handler != null && handler.IsActive)
            {
                return handler;
            }
        }

        return null;
    }

    private void InterruptActiveHandlers(UIAnimationInterruptReason reason)
    {
        ApplyToActiveHandlers(handler => handler.Kill());
        _lastInterruptReason = reason;
    }

    private void ApplyToActiveHandlers(System.Action<UIAnimationHandler> action)
    {
        ApplyToHandlers(handler =>
        {
            if (handler.IsActive)
            {
                action?.Invoke(handler);
            }
        });
    }

    private void ApplyToHandlers(System.Action<UIAnimationHandler> action)
    {
        for (int i = 0; i < _handlers.Count; i++)
        {
            UIAnimationHandler handler = _handlers[i];

            if (handler == null)
            {
                continue;
            }

            action?.Invoke(handler);
        }
    }

    private bool HasActiveAnimation()
    {
        for (int i = 0; i < _handlers.Count; i++)
        {
            UIAnimationHandler handler = _handlers[i];

            if (handler != null && handler.IsActive)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasPlayingHandler()
    {
        for (int i = 0; i < _handlers.Count; i++)
        {
            UIAnimationHandler handler = _handlers[i];

            if (handler != null && handler.IsPlaying)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasPausedHandler()
    {
        for (int i = 0; i < _handlers.Count; i++)
        {
            UIAnimationHandler handler = _handlers[i];

            if (handler != null && handler.IsPaused)
            {
                return true;
            }
        }

        return false;
    }

    private int CountActiveTweens()
    {
        int count = 0;

        for (int i = 0; i < _handlers.Count; i++)
        {
            UIAnimationHandler handler = _handlers[i];

            if (handler != null)
            {
                count += handler.ActiveTweenCount;
            }
        }

        return count;
    }

    private float CalculateMaxHandlerDuration()
    {
        float duration = 0f;

        for (int i = 0; i < _handlers.Count; i++)
        {
            UIAnimationHandler handler = _handlers[i];

            if (handler != null)
            {
                duration = Mathf.Max(duration, handler.FinalDuration);
            }
        }

        return duration;
    }

    private UIAnimationDurationGrade GetDurationGrade(float duration)
    {
        if (duration <= 0.10f)
        {
            return UIAnimationDurationGrade.INSTANT;
        }

        if (duration <= 0.25f)
        {
            return UIAnimationDurationGrade.VERY_SHORT;
        }

        if (duration <= 0.45f)
        {
            return UIAnimationDurationGrade.SHORT;
        }

        if (duration <= 0.80f)
        {
            return UIAnimationDurationGrade.NORMAL;
        }

        if (duration <= 1.40f)
        {
            return UIAnimationDurationGrade.LONG;
        }

        return UIAnimationDurationGrade.VERY_LONG;
    }

    private void HandleActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        if (HasActiveAnimation())
        {
            InterruptActiveHandlers(UIAnimationInterruptReason.SCENE_CHANGED);
        }
    }
}
