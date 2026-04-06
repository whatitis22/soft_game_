using System;
using System.Collections.Generic;
using DungeonProject.Core.Enum;

namespace DungeonProject.Core.Runtime
{
    [Serializable]
    public class PlayerStageState
    {
        public int PlayerIndex;                    // 플레이어 고정 순번
        public string PlayerName;                  // 플레이어 표시 이름

        public int GoldCoinCount;                  // 보유 골드 개수
        public int SilverCoinCount;                // 보유 실버 개수
        public int CopperCoinCount;                // 보유 코퍼 개수

        public List<int> UsedNumbers;              // 이번 스테이지에서 이미 사용한 숫자 목록

        public PlayerStageState(int playerIndex, string playerName)
        {
            PlayerIndex = playerIndex;
            PlayerName = playerName;
            GoldCoinCount = 0;
            SilverCoinCount = 0;
            CopperCoinCount = 0;
            UsedNumbers = new List<int>();
        }

        public bool CanUseNumber(int number)
        {
            return UsedNumbers.Contains(number) == false;
        }

        public void MarkNumberAsUsed(int number)
        {
            if (UsedNumbers.Contains(number))
                return;

            UsedNumbers.Add(number);
        }

        public void ResetUsedNumbers()
        {
            UsedNumbers.Clear();
        }

        public void AddCoin(CoinType coinType, int amount)
        {
            if (amount <= 0)
                return;

            switch (coinType)
            {
                case CoinType.Gold:
                    GoldCoinCount += amount;
                    break;

                case CoinType.Silver:
                    SilverCoinCount += amount;
                    break;

                case CoinType.Copper:
                    CopperCoinCount += amount;
                    break;
            }
        }

        public int GetCoinCount(CoinType coinType)
        {
            switch (coinType)
            {
                case CoinType.Gold:
                    return GoldCoinCount;

                case CoinType.Silver:
                    return SilverCoinCount;

                case CoinType.Copper:
                    return CopperCoinCount;
            }

            return 0;
        }

        public int CalculateScore(int setBonus)
        {
            int fullSetCount = Math.Min(GoldCoinCount, Math.Min(SilverCoinCount, CopperCoinCount));
            int totalCoinCount = GoldCoinCount + SilverCoinCount + CopperCoinCount;
            int bonusScore = fullSetCount * setBonus;

            return totalCoinCount + bonusScore;
        }

        public CoinType GetMostOwnedCoinType()
        {
            int maxValue = GoldCoinCount;
            CoinType result = CoinType.Gold;

            if (SilverCoinCount > maxValue)
            {
                maxValue = SilverCoinCount;
                result = CoinType.Silver;
            }

            if (CopperCoinCount > maxValue)
            {
                result = CoinType.Copper;
            }

            return result;
        }

        public int RemoveAllOfCoinType(CoinType coinType)
        {
            int removedAmount = 0;

            switch (coinType)
            {
                case CoinType.Gold:
                    removedAmount = GoldCoinCount;
                    GoldCoinCount = 0;
                    break;

                case CoinType.Silver:
                    removedAmount = SilverCoinCount;
                    SilverCoinCount = 0;
                    break;

                case CoinType.Copper:
                    removedAmount = CopperCoinCount;
                    CopperCoinCount = 0;
                    break;
            }

            return removedAmount;
        }
    }
}