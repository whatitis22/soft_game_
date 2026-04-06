using System;
using DungeonProject.Core.Enum;

namespace DungeonProject.Core.Runtime
{
    [Serializable]
    public class RewardPouchState
    {
        public int GoldCount;     // 파우치 안 골드 개수
        public int SilverCount;   // 파우치 안 실버 개수
        public int CopperCount;   // 파우치 안 코퍼 개수

        public void AddCoin(CoinType coinType, int amount)
        {
            if (amount <= 0)
                return;

            switch (coinType)
            {
                case CoinType.Gold:
                    GoldCount += amount;
                    break;

                case CoinType.Silver:
                    SilverCount += amount;
                    break;

                case CoinType.Copper:
                    CopperCount += amount;
                    break;
            }
        }

        public bool HasAnyCoin()
        {
            return GoldCount > 0 || SilverCount > 0 || CopperCount > 0;
        }

        public int GetTotalCoinCount()
        {
            return GoldCount + SilverCount + CopperCount;
        }

        public bool TryTakeOneCoin(out CoinType coinType)
        {
            if (GoldCount > 0)
            {
                GoldCount--;
                coinType = CoinType.Gold;
                return true;
            }
            if (SilverCount > 0)
            {
                SilverCount--;
                coinType = CoinType.Silver;
                return true;
            }
            if (CopperCount > 0)
            {
                CopperCount--;
                coinType = CoinType.Copper;
                return true;
            }
            coinType = CoinType.Copper;
            return false;
        }
    }
}