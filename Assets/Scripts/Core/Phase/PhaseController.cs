using UnityEngine;
using UnityEngine.Events;
using DungeonProject.Core.Enum;

namespace DungeonProject.Core.Phase
{
    public class PhazeController : MonoBehaviour
    {
        [System.Serializable]
        public class PhaseChangedEvent : UnityEvent<GamePhaseType> { }

        [Header("현재 페이즈")]
        [SerializeField] private GamePhaseType currentPhase = GamePhaseType.None;   // 현재 활성 페이즈

        [Header("라이프사이클 이벤트")]
        [SerializeField] private UnityEvent onCreate;           // Awake 시 호출할 이벤트
        [SerializeField] private UnityEvent onStartPhase;       // Start 시 호출할 이벤트
        [SerializeField] private UnityEvent onEnablePhase;      // OnEnable 시 호출할 이벤트
        [SerializeField] private UnityEvent onDisablePhase;     // OnDisable 시 호출할 이벤트
        [SerializeField] private UnityEvent onDestroyPhase;     // OnDestroy 시 호출할 이벤트

        [Header("페이즈 전환 이벤트")]
        [SerializeField] private PhaseChangedEvent onPhaseChanged;  // 페이즈 변경 시 전달할 이벤트

        public GamePhaseType CurrentPhase => currentPhase;

        private void Awake()
        {
            onCreate?.Invoke();
        }

        private void Start()
        {
            onStartPhase?.Invoke();
        }

        private void OnEnable()
        {
            onEnablePhase?.Invoke();
        }

        private void OnDisable()
        {
            onDisablePhase?.Invoke();
        }

        private void OnDestroy()
        {
            onDestroyPhase?.Invoke();
        }

        public void ChangePhase(GamePhaseType nextPhase)
        {
            if (currentPhase == nextPhase)
                return;

            currentPhase = nextPhase;
            onPhaseChanged?.Invoke(currentPhase);

            Debug.Log($"[PhazeController] 현재 페이즈 변경: {currentPhase}");
        }
    }
}