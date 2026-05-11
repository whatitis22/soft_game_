using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class GameStatusMessageSettings : MonoBehaviour
{
    public static GameStatusMessageSettings Instance { get; private set; }

    [SerializeField] private TMP_FontAsset statusFont;

    public TMP_FontAsset StatusFont => statusFont;

    private void OnEnable()
    {
        Instance = this;
        GameStatusMessageRouter.RefreshFallbackFont();
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;

        GameStatusMessageRouter.RefreshFallbackFont();
    }
}
