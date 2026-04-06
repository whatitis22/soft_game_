using System;
using DungeonProject.Core.Data;

namespace DungeonProject.Core.Runtime
{
    [Serializable]
    public class StageRuntimeState
    {
        public int CurrentStageIndex;                // 현재 스테이지 번호(0 시작)
        public int CurrentRoundIndex;                // 현재 라운드 번호(0 시작)
        public MonsterData CurrentMonsterData;       // 현재 스테이지 몬스터 데이터
        public RewardPouchState RewardPouchState;    // 현재 스테이지 파우치 상태

        public StageRuntimeState()
        {
            CurrentStageIndex = 0;
            CurrentRoundIndex = 0;
            CurrentMonsterData = null;
            RewardPouchState = new RewardPouchState();
        }
    }
}