using System;
using System.Collections.Generic;
using DungeonProject.Core.Enum;

namespace DungeonProject.Core.Runtime
{
    [Serializable]
    public class BattleResolutionResult
    {
        public bool IsSuccess;                                // 몬스터 처치 성공 여부
        public int TotalDamage;                               // 실제 적용된 총 대미지

        public List<int> CanceledPlayerIndices;              // 중복 취소된 플레이어 인덱스 목록
        public List<int> ValidAttackOrderPlayerIndices;      // 유효 공격자 순서 목록(낮은 숫자 우선)
        public List<int> FailPenaltyPlayerIndices;           // 실패 패널티 대상 플레이어 목록

        public Dictionary<int, CoinType> DroppedCoinTypes;   // 실패 시 각 플레이어가 떨어뜨린 코인 종류
        public Dictionary<int, int> DroppedCoinAmounts;      // 실패 시 각 플레이어가 떨어뜨린 코인 수량

        public BattleResolutionResult()
        {
            CanceledPlayerIndices = new List<int>();
            ValidAttackOrderPlayerIndices = new List<int>();
            FailPenaltyPlayerIndices = new List<int>();
            DroppedCoinTypes = new Dictionary<int, CoinType>();
            DroppedCoinAmounts = new Dictionary<int, int>();
        }
    }
}