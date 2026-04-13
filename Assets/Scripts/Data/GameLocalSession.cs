using UnityEngine;

[DisallowMultipleComponent]
public class GameLocalSession : MonoBehaviour
{
    public static GameLocalSession Instance { get; private set; }

    [Header("로컬 전용")]
    [SerializeField] private string localNickname = "";
    [SerializeField] private string lastJoinedIp = "127.0.0.1";
    [SerializeField] private bool chatPopupOpened;
    [SerializeField] private int localSelectedSkinId;

    public string LocalNickname
    {
        get => localNickname;
        set => localNickname = value?.Trim() ?? string.Empty;
    }

    public string LastJoinedIp
    {
        get => lastJoinedIp;
        set => lastJoinedIp = string.IsNullOrWhiteSpace(value) ? "127.0.0.1" : value.Trim();
    }

    public bool ChatPopupOpened
    {
        get => chatPopupOpened;
        set => chatPopupOpened = value;
    }

    public int LocalSelectedSkinId
    {
        get => localSelectedSkinId;
        set => localSelectedSkinId = value;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}