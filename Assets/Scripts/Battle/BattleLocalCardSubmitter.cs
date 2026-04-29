using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class BattleLocalCardSubmitter : MonoBehaviour
{
    [SerializeField] private CardDrawPopupController popupController;

    public void OnCardSubmitted(int cardValue)
    {
        // UI에서 온 제출 요청도 먼저 로컬에서 걸러 사용자에게 즉시 이유를 보여준다.
        if (NetworkSessionState.Instance != null && NetworkSessionState.Instance.IsRoundLocked.Value)
        {
            ShowLocalFeedback("라운드가 잠겨 카드 제출을 변경할 수 없습니다.");

            if (popupController != null)
            {
                popupController.RejectTemporarySubmission();
                popupController.SetOpenButtonInteractable(true);
            }

            return;
        }

        if (cardValue < 1 || cardValue > 6)
        {
            ShowLocalFeedback($"유효하지 않은 카드입니다. ({cardValue})");

            if (popupController != null)
            {
                popupController.RejectTemporarySubmission();
                popupController.SetOpenButtonInteractable(true);
            }

            return;
        }

        if (!TryGetLocalPlayerState(out NetworkPlayerState localPlayer))
        {
            // 네트워크 플레이어가 아직 준비되지 않은 타이밍에는 서버 요청을 보내지 않는다.
            ShowLocalFeedback("로컬 플레이어 정보를 찾지 못했습니다.");

            if (popupController != null)
            {
                popupController.RejectTemporarySubmission();
                popupController.SetOpenButtonInteractable(true);
            }

            return;
        }

        if (localPlayer.SubmittedCard.Value >= 1 && localPlayer.SubmittedCard.Value <= 6)
        {
            ShowLocalFeedback("이미 이번 라운드에 카드를 제출했습니다.");

            if (popupController != null)
            {
                popupController.RejectTemporarySubmission();
                popupController.SetOpenButtonInteractable(true);
            }

            return;
        }

        if (!localPlayer.HasNumberCard(cardValue))
        {
            ShowLocalFeedback($"카드 {cardValue}는 이미 사용했거나 보유하지 않았습니다.");

            if (popupController != null)
            {
                popupController.RejectTemporarySubmission();
                popupController.SetOpenButtonInteractable(true);
            }

            return;
        }

        localPlayer.RequestUseNumberCardRpc(cardValue);
    }

    private bool TryGetLocalPlayerState(out NetworkPlayerState result)
    {
        // 씬 검색 대신 중앙 레지스트리에서 로컬 PlayerState를 가져온다.
        return NetworkPlayerRegistry.TryGetLocalPlayer(out result);
    }

    private void ShowLocalFeedback(string message)
    {
        GameStatusMessageRouter.Show(message);
    }
}
