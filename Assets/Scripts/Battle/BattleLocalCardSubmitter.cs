using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class BattleLocalCardSubmitter : MonoBehaviour
{
    [SerializeField] private CardDrawPopupController popupController;

    public void OnCardSubmitted(int cardValue)
    {
        if (cardValue < 1 || cardValue > 6)
        {
            ShowLocalFeedback($"유효하지 않은 카드입니다. ({cardValue})");

            if (popupController != null)
                popupController.SetOpenButtonInteractable(true);

            return;
        }

        if (!TryGetLocalPlayerState(out NetworkPlayerState localPlayer))
        {
            ShowLocalFeedback("로컬 플레이어 정보를 찾지 못했습니다.");

            if (popupController != null)
                popupController.SetOpenButtonInteractable(true);

            return;
        }

        if (localPlayer.SubmittedCard.Value >= 1 && localPlayer.SubmittedCard.Value <= 6)
        {
            ShowLocalFeedback("이미 이번 라운드에 카드를 제출했습니다.");

            if (popupController != null)
                popupController.SetOpenButtonInteractable(true);

            return;
        }

        if (!localPlayer.HasNumberCard(cardValue))
        {
            ShowLocalFeedback($"카드 {cardValue}는 이미 사용했거나 보유하지 않았습니다.");

            if (popupController != null)
                popupController.SetOpenButtonInteractable(true);

            return;
        }

        localPlayer.RequestUseNumberCardRpc(cardValue);
    }

    private bool TryGetLocalPlayerState(out NetworkPlayerState result)
    {
        return NetworkPlayerRegistry.TryGetLocalPlayer(out result);
    }

    private void ShowLocalFeedback(string message)
    {
        if (P2PChatUI.Instance != null)
            P2PChatUI.Instance.SetStatus(message);
        else
            Debug.LogWarning(message);
    }
}
