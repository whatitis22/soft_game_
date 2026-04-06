using System;

namespace DungeonProject.Core.Runtime
{
    [Serializable]
    public class PlayerRoundState
    {
        public int PlayerIndex;           // 플레이어 고정 순번
        public bool HasSubmitted;         // 이번 라운드 제출 여부
        public int SubmittedNumber;       // 이번 라운드 제출 숫자
        public bool IsCanceled;           // 중복으로 인해 공격 취소되었는지 여부
        public bool IsLowestOnFail;       // 실패 시 패널티 대상인지 여부

        public PlayerRoundState(int playerIndex)
        {
            PlayerIndex = playerIndex;
            HasSubmitted = false;
            SubmittedNumber = -1;
            IsCanceled = false;
            IsLowestOnFail = false;
        }

        public void Submit(int number)
        {
            HasSubmitted = true;
            SubmittedNumber = number;
            IsCanceled = false;
            IsLowestOnFail = false;
        }

        public void ClearRoundFlags()
        {
            IsCanceled = false;
            IsLowestOnFail = false;
        }
    }
}