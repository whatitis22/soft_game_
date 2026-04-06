using System;
using System.Collections.Generic;

namespace DungeonProject.Core.Runtime
{
    [Serializable]
    public class GameRuntimeState
    {
        public List<PlayerStageState> PlayerStates;      // 전체 플레이어 스테이지 상태 목록
        public StageRuntimeState StageState;             // 현재 스테이지 진행 상태

        public GameRuntimeState()
        {
            PlayerStates = new List<PlayerStageState>();
            StageState = new StageRuntimeState();
        }
    }
}