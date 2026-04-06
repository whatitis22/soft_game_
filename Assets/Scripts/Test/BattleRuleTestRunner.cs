using System.Collections.Generic;
using DungeonProject.Core.Data;
using DungeonProject.Core.Rule;
using DungeonProject.Core.Runtime;
using UnityEngine;

namespace DungeonProject.Test
{
    public class BattleRuleTestRunner : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField] private BattleRuleResolver resolver;     // 규칙 계산기
        [SerializeField] private MonsterData testMonsterData;     // 테스트용 몬스터 데이터

        private void Start()
        {
            RunTest();
        }

        [ContextMenu("Run Test")]
        public void RunTest()
        {
            if (resolver == null || testMonsterData == null)
            {
                Debug.LogError("테스트 실패: 참조 누락");
                return;
            }

            List<PlayerStageState> stageStates = new List<PlayerStageState>()
            {
                new PlayerStageState(0, "P1"),
                new PlayerStageState(1, "P2"),
                new PlayerStageState(2, "P3"),
                new PlayerStageState(3, "P4"),
                new PlayerStageState(4, "P5"),
            };

            stageStates[0].GoldCoinCount = 2;
            stageStates[1].SilverCoinCount = 3;
            stageStates[2].CopperCoinCount = 1;
            stageStates[3].GoldCoinCount = 1;
            stageStates[4].SilverCoinCount = 2;

            List<PlayerRoundState> roundStates = new List<PlayerRoundState>()
            {
                new PlayerRoundState(0),
                new PlayerRoundState(1),
                new PlayerRoundState(2),
                new PlayerRoundState(3),
                new PlayerRoundState(4),
            };

            roundStates[0].Submit(1);
            roundStates[1].Submit(2);
            roundStates[2].Submit(2);
            roundStates[3].Submit(5);
            roundStates[4].Submit(6);

            BattleResolutionResult result = resolver.ResolveBattle(testMonsterData, stageStates, roundStates);

            Debug.Log($"전투 성공 여부: {result.IsSuccess}");
            Debug.Log($"총 대미지: {result.TotalDamage}");
            Debug.Log($"취소된 플레이어 수: {result.CanceledPlayerIndices.Count}");
            Debug.Log($"유효 공격자 수: {result.ValidAttackOrderPlayerIndices.Count}");

            for (int i = 0; i < result.ValidAttackOrderPlayerIndices.Count; i++)
            {
                Debug.Log($"공격 순위 {i + 1}: Player {result.ValidAttackOrderPlayerIndices[i]}");
            }

            for (int i = 0; i < result.FailPenaltyPlayerIndices.Count; i++)
            {
                int playerIndex = result.FailPenaltyPlayerIndices[i];
                Debug.Log($"실패 패널티 대상: Player {playerIndex}");
            }
        }
    }
}