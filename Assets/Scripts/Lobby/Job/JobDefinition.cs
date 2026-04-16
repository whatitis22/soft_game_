using UnityEngine;

[CreateAssetMenu(fileName = "JobDefinition", menuName = "P2P/Job Definition")]
public class JobDefinition : ScriptableObject
{
    public JobType jobType;
    public string displayName;

    [Header("직업 선택 팝업")]
    public Sprite portrait;

    [Header("로비 스탠딩")]
    public Sprite lobbyStandingImage;

    [Header("Battle")]
    [SerializeField] private GameObject battlePlayerStandingPrefab;
    public GameObject BattlePlayerStandingPrefab => battlePlayerStandingPrefab;

    [TextArea(2, 4)]
    public string shortDescription;

    [TextArea(4, 10)]
    public string detailDescription;

    public enum BattleStandingAction
    {
        Idle,
        Attack,
        Victory,
        Defeat
    }
}