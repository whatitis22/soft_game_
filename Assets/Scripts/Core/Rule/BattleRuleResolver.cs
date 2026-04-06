using System.Collections.Generic;
using System.Linq;
using DungeonProject.Core.Data;
using DungeonProject.Core.Enum;
using DungeonProject.Core.Runtime;
using UnityEngine;

namespace DungeonProject.Core.Rule
{
    public class BattleRuleResolver : MonoBehaviour
    {
        [Header("규칙 데이터")]
        [SerializeField] private GameRuleData ruleData;   // 게임 전체 규칙 데이터

        public GameRuleData RuleData => ruleData;

        public BattleResolutionResult ResolveBattle(
            MonsterData monsterData,
            List<PlayerStageState> playerStageStates,
            List<PlayerRoundState> playerRoundStates)
        {
            BattleResolutionResult result = new BattleResolutionResult();

            if (monsterData == null || playerStageStates == null || playerRoundStates == null)
            {
                Debug.LogError("ResolveBattle 실패: 입력 데이터가 비어 있음");
                return result;
            }

            ClearRoundFlags(playerRoundStates);

            Dictionary<int, List<PlayerRoundState>> groupedByNumber = GroupSubmittedPlayersByNumber(playerRoundStates);
            MarkCanceledPlayers(groupedByNumber, result);

            List<PlayerRoundState> validAttackers = GetValidAttackers(playerRoundStates);
            validAttackers = validAttackers.OrderBy(x => x.SubmittedNumber).ToList();

            for (int i = 0; i < validAttackers.Count; i++)
            {
                result.ValidAttackOrderPlayerIndices.Add(validAttackers[i].PlayerIndex);
            }

            result.TotalDamage = CalculateTotalDamage(validAttackers);
            result.IsSuccess = result.TotalDamage >= monsterData.MaxHp;

            if (result.IsSuccess == false)
            {
                ApplyFailPenalty(validAttackers, playerStageStates, result);
            }

            MarkUsedNumbers(playerStageStates, playerRoundStates);

            return result;
        }

        public bool CanSubmitNumber(PlayerStageState playerStageState, int number)
        {
            if (playerStageState == null || ruleData == null)
                return false;

            if (number < ruleData.MinPlayableNumber || number > ruleData.MaxPlayableNumber)
                return false;

            return playerStageState.CanUseNumber(number);
        }

        public PlayerStageState FindStageState(List<PlayerStageState> playerStageStates, int playerIndex)
        {
            for (int i = 0; i < playerStageStates.Count; i++)
            {
                if (playerStageStates[i].PlayerIndex == playerIndex)
                    return playerStageStates[i];
            }

            return null;
        }

        private void ClearRoundFlags(List<PlayerRoundState> playerRoundStates)
        {
            for (int i = 0; i < playerRoundStates.Count; i++)
            {
                playerRoundStates[i].ClearRoundFlags();
            }
        }

        private Dictionary<int, List<PlayerRoundState>> GroupSubmittedPlayersByNumber(List<PlayerRoundState> playerRoundStates)
        {
            Dictionary<int, List<PlayerRoundState>> grouped = new Dictionary<int, List<PlayerRoundState>>();

            for (int i = 0; i < playerRoundStates.Count; i++)
            {
                PlayerRoundState roundState = playerRoundStates[i];

                if (roundState.HasSubmitted == false)
                    continue;

                if (grouped.ContainsKey(roundState.SubmittedNumber) == false)
                {
                    grouped.Add(roundState.SubmittedNumber, new List<PlayerRoundState>());
                }

                grouped[roundState.SubmittedNumber].Add(roundState);
            }

            return grouped;
        }

        private void MarkCanceledPlayers(
            Dictionary<int, List<PlayerRoundState>> groupedByNumber,
            BattleResolutionResult result)
        {
            foreach (KeyValuePair<int, List<PlayerRoundState>> pair in groupedByNumber)
            {
                if (pair.Value.Count <= 1)
                    continue;

                for (int i = 0; i < pair.Value.Count; i++)
                {
                    pair.Value[i].IsCanceled = true;
                    result.CanceledPlayerIndices.Add(pair.Value[i].PlayerIndex);
                }
            }
        }

        private List<PlayerRoundState> GetValidAttackers(List<PlayerRoundState> playerRoundStates)
        {
            List<PlayerRoundState> validAttackers = new List<PlayerRoundState>();

            for (int i = 0; i < playerRoundStates.Count; i++)
            {
                PlayerRoundState state = playerRoundStates[i];

                if (state.HasSubmitted == false)
                    continue;

                if (state.IsCanceled)
                    continue;

                validAttackers.Add(state);
            }

            return validAttackers;
        }

        private int CalculateTotalDamage(List<PlayerRoundState> validAttackers)
        {
            int totalDamage = 0;

            for (int i = 0; i < validAttackers.Count; i++)
            {
                totalDamage += validAttackers[i].SubmittedNumber;
            }

            return totalDamage;
        }

        private void ApplyFailPenalty(
            List<PlayerRoundState> validAttackers,
            List<PlayerStageState> playerStageStates,
            BattleResolutionResult result)
        {
            if (validAttackers.Count <= 0)
                return;

            int lowestNumber = validAttackers[0].SubmittedNumber;

            List<PlayerRoundState> penaltyTargets = validAttackers
                .Where(x => x.SubmittedNumber == lowestNumber)
                .ToList();

            for (int i = 0; i < penaltyTargets.Count; i++)
            {
                PlayerRoundState targetRoundState = penaltyTargets[i];
                targetRoundState.IsLowestOnFail = true;
                result.FailPenaltyPlayerIndices.Add(targetRoundState.PlayerIndex);

                PlayerStageState stageState = FindStageState(playerStageStates, targetRoundState.PlayerIndex);
                if (stageState == null)
                    continue;

                CoinType droppedType = stageState.GetMostOwnedCoinType();
                int droppedAmount = stageState.RemoveAllOfCoinType(droppedType);

                result.DroppedCoinTypes[targetRoundState.PlayerIndex] = droppedType;
                result.DroppedCoinAmounts[targetRoundState.PlayerIndex] = droppedAmount;
            }
        }

        private void MarkUsedNumbers(
            List<PlayerStageState> playerStageStates,
            List<PlayerRoundState> playerRoundStates)
        {
            for (int i = 0; i < playerRoundStates.Count; i++)
            {
                PlayerRoundState roundState = playerRoundStates[i];

                if (roundState.HasSubmitted == false)
                    continue;

                PlayerStageState stageState = FindStageState(playerStageStates, roundState.PlayerIndex);
                if (stageState == null)
                    continue;

                stageState.MarkNumberAsUsed(roundState.SubmittedNumber);
            }
        }
    }
}