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
            Debug.LogWarning($"BattleLocalCardSubmitter: 잘못된 카드 값이 들어왔습니다. ({cardValue})");

            if (popupController != null)
                popupController.SetOpenButtonInteractable(true);

            return;
        }

        if (!TryGetLocalPlayerState(out NetworkPlayerState localPlayer))
        {
            Debug.LogWarning("BattleLocalCardSubmitter: 로컬 플레이어의 NetworkPlayerState를 찾지 못했습니다.");

            if (popupController != null)
                popupController.SetOpenButtonInteractable(true);

            return;
        }

        if (localPlayer.SubmittedCard.Value >= 1 && localPlayer.SubmittedCard.Value <= 6)
            return;

        if (!localPlayer.HasNumberCard(cardValue))
        {
            Debug.LogWarning($"BattleLocalCardSubmitter: 카드 {cardValue} 는 이미 사용된 카드입니다.");

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
}
