using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyStandingView : MonoBehaviour
{
    [SerializeField] private Image standingImage;
    [SerializeField] private TMP_Text nicknameText;

    public void Bind(Sprite sprite, string nickname, bool isLocalPlayer, float localScaleMultiplier)
    {
        if (standingImage != null)
            standingImage.sprite = sprite;

        if (nicknameText != null)
            nicknameText.text = nickname;

        float scale = isLocalPlayer ? localScaleMultiplier : 1f;
        transform.localScale = Vector3.one * scale;
    }
}