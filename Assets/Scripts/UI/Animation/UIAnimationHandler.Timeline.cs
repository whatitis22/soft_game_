using DG.Tweening;

public partial class UIAnimationHandler
{
    private void StartPreset()
    {
        _currentSequence = CreateSequence();
        _state = UIAnimationState.PLAYING;

        _onPlay?.Invoke();
        _currentSequence.Play();
    }

    private Sequence CreateSequence()
    {
        Sequence sequence = DOTween.Sequence();
        sequence.SetTarget(this);
        sequence.Pause();

        bool hasStep = false;

        foreach (UIAnimationStep step in _animationPreset)
        {
            hasStep |= AppendStep(sequence, step);
        }

        if (!hasStep)
        {
            sequence.AppendCallback(() => { });
        }

        sequence.OnComplete(CompleteAnimation);
        sequence.OnKill(() =>
        {
            if (_currentSequence == sequence)
            {
                _currentSequence = null;
            }
        });

        return sequence;
    }

    private bool AppendStep(Sequence sequence, UIAnimationStep step)
    {
        if (step == null)
        {
            return false;
        }

        if (step.SequenceMode == TypeUIAnimationSequence.EXCEPT)
        {
            sequence.AppendCallback(() =>
            {
                PlayExceptStep(step);
            });
            return true;
        }

        Tween stepTween = step.CreateTween();

        if (step.SequenceMode == TypeUIAnimationSequence.JOIN)
        {
            sequence.Join(stepTween);
            return true;
        }

        sequence.Append(stepTween);
        return true;
    }

    private void PlayExceptStep(UIAnimationStep step)
    {
        Tween tween = step.CreateTween();
        _exceptTweens.Add(tween);

        tween.OnKill(() =>
        {
            _exceptTweens.Remove(tween);
        });

        tween.Play();
    }

    private void CompleteAnimation()
    {
        _state = UIAnimationState.COMPLETED;
        _onComplete?.Invoke();
    }

    private void Interrupt(bool rewind)
    {
        if (rewind)
        {
            StopCurrentSequence();
            StopExceptTweens();
            _state = UIAnimationState.STOPPED;
        }
        else
        {
            KillCurrentSequence();
            KillExceptTweens();
            _state = UIAnimationState.KILLED;
        }
    }

    private void StopCurrentSequence()
    {
        if (_currentSequence == null)
        {
            return;
        }

        _currentSequence.Rewind();
        _currentSequence.Kill(false);
        _currentSequence = null;
    }

    private void KillCurrentSequence()
    {
        if (_currentSequence == null)
        {
            return;
        }

        _currentSequence.Kill(false);
        _currentSequence = null;
    }

    private void PauseExceptTweens()
    {
        for (int i = _exceptTweens.Count - 1; i >= 0; i--)
        {
            _exceptTweens[i]?.Pause();
        }
    }

    private void ResumeExceptTweens()
    {
        for (int i = _exceptTweens.Count - 1; i >= 0; i--)
        {
            _exceptTweens[i]?.Play();
        }
    }

    private void StopExceptTweens()
    {
        for (int i = _exceptTweens.Count - 1; i >= 0; i--)
        {
            Tween tween = _exceptTweens[i];

            if (tween == null)
            {
                continue;
            }

            tween.Rewind();
            tween.Kill(false);
        }

        _exceptTweens.Clear();
    }

    private void CompleteExceptTweens()
    {
        for (int i = _exceptTweens.Count - 1; i >= 0; i--)
        {
            _exceptTweens[i]?.Complete(true);
        }

        _exceptTweens.Clear();
    }

    private void KillExceptTweens()
    {
        for (int i = _exceptTweens.Count - 1; i >= 0; i--)
        {
            _exceptTweens[i]?.Kill(false);
        }

        _exceptTweens.Clear();
    }
}
