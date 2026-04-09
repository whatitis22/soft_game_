using UnityEngine;

[CreateAssetMenu(fileName = "JobDefinition", menuName = "P2P/Job Definition")]
public class JobDefinition : ScriptableObject
{
    public JobType jobType;
    public string displayName;
    public Sprite portrait;

    [TextArea(2, 4)]
    public string shortDescription;

    [TextArea(4, 10)]
    public string detailDescription;
}