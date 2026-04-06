using UnityEngine;

namespace DungeonProject.Core.Data
{
    [CreateAssetMenu(fileName = "GameRuleData_", menuName = "DungeonProject/Data/Game Rule Data")]
    public class GameRuleData : ScriptableObject
    {
        [Header("플레이어 설정")]
        [SerializeField] private int playerCount = 5;           // 기본 플레이어 수
        [SerializeField] private int minPlayableNumber = 1;     // 플레이 가능한 최소 숫자
        [SerializeField] private int maxPlayableNumber = 6;     // 플레이 가능한 최대 숫자

        [Header("스테이지 설정")]
        [SerializeField] private int roundsPerStage = 4;        // 한 스테이지당 라운드 수
        [SerializeField] private int totalStageCount = 3;       // 전체 스테이지 수

        [Header("점수 설정")]
        [SerializeField] private int completeSetBonus = 3;      // 금/은/동 세트 완성 보너스 점수

        public int PlayerCount => playerCount;
        public int MinPlayableNumber => minPlayableNumber;
        public int MaxPlayableNumber => maxPlayableNumber;
        public int RoundsPerStage => roundsPerStage;
        public int TotalStageCount => totalStageCount;
        public int CompleteSetBonus => completeSetBonus;
    }
}