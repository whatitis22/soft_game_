using System.Collections.Generic;
using System.Text;
using DungeonProject.Core.Data;
using DungeonProject.Core.Enum;
using DungeonProject.Core.Rule;
using DungeonProject.Core.Runtime;
using UnityEngine;

namespace DungeonProject.Core.Manager
{
    public class PrototypeGameManager : MonoBehaviour
    {
        [Header("규칙/데이터")]
        [SerializeField] private BattleRuleResolver battleRuleResolver;     // 전투 규칙 계산기
        [SerializeField] private GameRuleData gameRuleData;                 // 전체 게임 규칙 데이터
        [SerializeField] private MonsterData[] stageMonsterList;            // 스테이지별 몬스터 목록

        [Header("현재 선택")]
        [SerializeField] private int currentInputPlayerIndex = 0;           // 현재 숫자를 입력 중인 플레이어 인덱스

        [Header("디버그 출력")]
        [SerializeField][TextArea(8, 30)] private string latestSummary;    // 현재 상태 요약 문자열

        private GameRuntimeState runtimeState;                              // 전체 런타임 상태
        private List<PlayerRoundState> currentRoundStates;                  // 현재 라운드 제출 상태 목록
        private bool isGameFinished;                                        // 게임 종료 여부

        public string LatestSummary => latestSummary;
        public int CurrentInputPlayerIndex => currentInputPlayerIndex;
        public bool IsGameFinished => isGameFinished;

        private void Start()
        {
            StartPrototypeGame();
        }

        [ContextMenu("Start Prototype Game")]
        public void StartPrototypeGame()
        {
            if (battleRuleResolver == null || gameRuleData == null)
            {
                Debug.LogError("PrototypeGameManager 시작 실패: 필수 참조 누락");
                return;
            }

            runtimeState = new GameRuntimeState();
            currentRoundStates = new List<PlayerRoundState>();
            isGameFinished = false;
            currentInputPlayerIndex = 0;

            CreatePlayers();
            StartStage(0);
            RefreshSummary();
        }

        private void CreatePlayers()
        {
            runtimeState.PlayerStates.Clear();

            for (int i = 0; i < gameRuleData.PlayerCount; i++)
            {
                PlayerStageState playerState = new PlayerStageState(i, $"P{i + 1}");
                runtimeState.PlayerStates.Add(playerState);
            }
        }

        private void StartStage(int stageIndex)
        {
            runtimeState.StageState.CurrentStageIndex = stageIndex;
            runtimeState.StageState.CurrentRoundIndex = 0;
            runtimeState.StageState.RewardPouchState = new RewardPouchState();

            if (stageMonsterList != null && stageMonsterList.Length > 0)
            {
                int safeIndex = Mathf.Clamp(stageIndex, 0, stageMonsterList.Length - 1);
                runtimeState.StageState.CurrentMonsterData = stageMonsterList[safeIndex];
            }
            else
            {
                runtimeState.StageState.CurrentMonsterData = null;
            }

            for (int i = 0; i < runtimeState.PlayerStates.Count; i++)
            {
                runtimeState.PlayerStates[i].ResetUsedNumbers();
            }

            StartRound();
        }

        private void StartRound()
        {
            currentRoundStates.Clear();
            currentInputPlayerIndex = 0;

            for (int i = 0; i < runtimeState.PlayerStates.Count; i++)
            {
                currentRoundStates.Add(new PlayerRoundState(i));
            }
        }

        public bool CanCurrentPlayerSubmit(int number)
        {
            if (isGameFinished)
                return false;

            if (currentInputPlayerIndex < 0 || currentInputPlayerIndex >= runtimeState.PlayerStates.Count)
                return false;

            PlayerStageState playerState = runtimeState.PlayerStates[currentInputPlayerIndex];
            return battleRuleResolver.CanSubmitNumber(playerState, number);
        }

        public void SubmitNumberForCurrentPlayer(int number)
        {
            if (isGameFinished)
                return;

            if (CanCurrentPlayerSubmit(number) == false)
            {
                Debug.Log($"제출 불가 숫자: Player {currentInputPlayerIndex}, Number {number}");
                RefreshSummary();
                return;
            }

            PlayerRoundState roundState = currentRoundStates[currentInputPlayerIndex];
            roundState.Submit(number);

            Debug.Log($"Player {currentInputPlayerIndex} 제출: {number}");

            currentInputPlayerIndex++;

            if (currentInputPlayerIndex >= runtimeState.PlayerStates.Count)
            {
                ResolveCurrentRound();
            }

            RefreshSummary();
        }

        private void ResolveCurrentRound()
        {
            MonsterData monsterData = runtimeState.StageState.CurrentMonsterData;

            BattleResolutionResult result = battleRuleResolver.ResolveBattle(
                monsterData,
                runtimeState.PlayerStates,
                currentRoundStates);

            if (result.IsSuccess)
            {
                ApplySuccessRewards(result);
            }
            else
            {
                ApplyFailDropsToPouch(result);
            }

            LogRoundResult(result);

            runtimeState.StageState.CurrentRoundIndex++;

            bool isStageFinished = runtimeState.StageState.CurrentRoundIndex >= gameRuleData.RoundsPerStage;

            if (isStageFinished)
            {
                int nextStageIndex = runtimeState.StageState.CurrentStageIndex + 1;

                if (nextStageIndex >= gameRuleData.TotalStageCount)
                {
                    isGameFinished = true;
                    Debug.Log("게임 종료");
                }
                else
                {
                    StartStage(nextStageIndex);
                }
            }
            else
            {
                StartRound();
            }
        }

        private void ApplySuccessRewards(BattleResolutionResult result)
        {
            MonsterData monsterData = runtimeState.StageState.CurrentMonsterData;
            if (monsterData == null)
                return;

            List<CoinType> rewardQueue = new List<CoinType>();

            for (int i = 0; i < monsterData.GoldReward; i++)
                rewardQueue.Add(CoinType.Gold);

            for (int i = 0; i < monsterData.SilverReward; i++)
                rewardQueue.Add(CoinType.Silver);

            for (int i = 0; i < monsterData.CopperReward; i++)
                rewardQueue.Add(CoinType.Copper);

            int rewardIndex = 0;

            for (int i = 0; i < result.ValidAttackOrderPlayerIndices.Count; i++)
            {
                if (rewardIndex >= rewardQueue.Count)
                    break;

                int playerIndex = result.ValidAttackOrderPlayerIndices[i];
                PlayerStageState playerState = battleRuleResolver.FindStageState(runtimeState.PlayerStates, playerIndex);
                if (playerState == null)
                    continue;

                playerState.AddCoin(rewardQueue[rewardIndex], 1);
                rewardIndex++;
            }

            GivePouchCoinsToAttackOrder(result.ValidAttackOrderPlayerIndices);
        }

        private void ApplyFailDropsToPouch(BattleResolutionResult result)
        {
            foreach (KeyValuePair<int, CoinType> pair in result.DroppedCoinTypes)
            {
                int playerIndex = pair.Key;
                CoinType coinType = pair.Value;

                if (result.DroppedCoinAmounts.ContainsKey(playerIndex) == false)
                    continue;

                int amount = result.DroppedCoinAmounts[playerIndex];
                runtimeState.StageState.RewardPouchState.AddCoin(coinType, amount);
            }
        }

        private void GivePouchCoinsToAttackOrder(List<int> attackOrder)
        {
            RewardPouchState pouch = runtimeState.StageState.RewardPouchState;
            if (pouch == null || pouch.HasAnyCoin() == false)
                return;

            if (attackOrder == null || attackOrder.Count == 0)
                return;

            int loopIndex = 0;

            while (pouch.HasAnyCoin())
            {
                int playerIndex = attackOrder[loopIndex % attackOrder.Count];
                PlayerStageState playerState = battleRuleResolver.FindStageState(runtimeState.PlayerStates, playerIndex);

                if (playerState != null && pouch.TryTakeOneCoin(out CoinType coinType))
                {
                    playerState.AddCoin(coinType, 1);
                }

                loopIndex++;
            }
        }

        private void LogRoundResult(BattleResolutionResult result)
        {
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

        public void RefreshSummary()
        {
            if (runtimeState == null)
            {
                latestSummary = "게임 시작 전";
                return;
            }

            StringBuilder builder = new StringBuilder();

            int stageNumber = runtimeState.StageState.CurrentStageIndex + 1;
            int roundNumber = runtimeState.StageState.CurrentRoundIndex + 1;

            builder.AppendLine($"Stage : {stageNumber}/{gameRuleData.TotalStageCount}");
            builder.AppendLine($"Round : {roundNumber}/{gameRuleData.RoundsPerStage}");

            if (runtimeState.StageState.CurrentMonsterData != null)
            {
                builder.AppendLine($"Monster : {runtimeState.StageState.CurrentMonsterData.MonsterName}");
                builder.AppendLine($"Monster HP : {runtimeState.StageState.CurrentMonsterData.MaxHp}");
            }

            builder.AppendLine();

            if (isGameFinished == false)
            {
                builder.AppendLine($"Current Input Player : P{currentInputPlayerIndex + 1}");
                builder.AppendLine();
            }
            else
            {
                builder.AppendLine("게임 종료");
                builder.AppendLine();
            }

            builder.AppendLine("[Player State]");

            for (int i = 0; i < runtimeState.PlayerStates.Count; i++)
            {
                PlayerStageState state = runtimeState.PlayerStates[i];

                builder.AppendLine(
                    $"{state.PlayerName} | Gold:{state.GoldCoinCount} Silver:{state.SilverCoinCount} Copper:{state.CopperCoinCount} | Score:{state.CalculateScore(gameRuleData.CompleteSetBonus)}");
            }

            builder.AppendLine();
            builder.AppendLine("[Current Round Submit]");

            for (int i = 0; i < currentRoundStates.Count; i++)
            {
                PlayerRoundState roundState = currentRoundStates[i];

                if (roundState.HasSubmitted)
                    builder.AppendLine($"P{i + 1} -> {roundState.SubmittedNumber}");
                else
                    builder.AppendLine($"P{i + 1} -> (not submitted)");
            }

            builder.AppendLine();
            builder.AppendLine("[Reward Pouch]");
            builder.AppendLine(
                $"Gold:{runtimeState.StageState.RewardPouchState.GoldCount} Silver:{runtimeState.StageState.RewardPouchState.SilverCount} Copper:{runtimeState.StageState.RewardPouchState.CopperCount}");

            latestSummary = builder.ToString();
        }

        public string GetFinalRankingText()
        {
            List<PlayerStageState> copied = new List<PlayerStageState>(runtimeState.PlayerStates);

            copied.Sort((a, b) =>
            {
                int aScore = a.CalculateScore(gameRuleData.CompleteSetBonus);
                int bScore = b.CalculateScore(gameRuleData.CompleteSetBonus);
                return bScore.CompareTo(aScore);
            });

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("[Final Ranking]");

            for (int i = 0; i < copied.Count; i++)
            {
                int score = copied[i].CalculateScore(gameRuleData.CompleteSetBonus);
                builder.AppendLine($"{i + 1}위 : {copied[i].PlayerName} / Score {score}");
            }

            return builder.ToString();
        }
    }
}