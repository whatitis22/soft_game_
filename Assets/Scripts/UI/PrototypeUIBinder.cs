using DungeonProject.Core.Manager;
using TMPro;
using UnityEngine;

namespace DungeonProject.UI
{
    public class PrototypeUIBinder : MonoBehaviour
    {
        [SerializeField] private PrototypeGameManager gameManager;   // 프로토타입 게임 매니저
        [SerializeField] private TMP_Text summaryText;               // 상태 요약 표시 텍스트
        [SerializeField] private TMP_Text rankingText;               // 최종 순위 표시 텍스트

        private void Update()
        {
            if (gameManager == null)
                return;

            if (summaryText != null)
            {
                gameManager.RefreshSummary();
                summaryText.text = gameManager.LatestSummary;
            }

            if (rankingText != null)
            {
                if (gameManager.IsGameFinished)
                    rankingText.text = gameManager.GetFinalRankingText();
                else
                    rankingText.text = string.Empty;
            }
        }
    }
}