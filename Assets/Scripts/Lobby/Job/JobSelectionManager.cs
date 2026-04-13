using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class JobSelectionManager : MonoBehaviour
{
    public static JobSelectionManager Instance { get; private set; }

    private static readonly JobType[] AutoAssignPriority =
    {
        JobType.Hero,
        JobType.Saint,
        JobType.Witch,
        JobType.Archer,
        JobType.Fighter,
        JobType.Butcher,
        JobType.Guardian,
        JobType.Assassin
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public JobType GetFirstAvailableJob()
    {
        foreach (JobType job in AutoAssignPriority)
        {
            if (!IsJobTaken(job))
                return job;
        }

        return JobType.None;
    }

    public bool IsJobTaken(JobType targetJob, ulong ignoreOwnerClientId = ulong.MaxValue)
    {
        if (targetJob == JobType.None)
            return false;

        NetworkPlayerState[] players = FindObjectsByType<NetworkPlayerState>(FindObjectsSortMode.None);

        foreach (NetworkPlayerState player in players)
        {
            if (!player.IsSpawned)
                continue;

            if (player.OwnerClientId == ignoreOwnerClientId)
                continue;

            if (player.Job.Value == targetJob)
                return true;
        }

        return false;
    }

    public void ServerAutoAssignJob(NetworkPlayerState player)
    {
        if (player == null || !player.IsServer)
            return;

        JobType autoJob = GetFirstAvailableJob();
        player.ServerSetJob(autoJob);
    }

    public bool ServerTryAssignJob(NetworkPlayerState player, JobType targetJob)
    {
        if (player == null || !player.IsServer)
            return false;

        if (targetJob == JobType.None)
            return false;

        if (player.Job.Value == targetJob)
            return true;

        if (IsJobTaken(targetJob, player.OwnerClientId))
            return false;

        player.ServerSetJob(targetJob);
        return true;
    }

    public static string GetJobDisplayName(JobType job)
    {
        return job switch
        {
            JobType.Hero => "용사",
            JobType.Saint => "성녀",
            JobType.Witch => "마녀",
            JobType.Archer => "궁수",
            JobType.Fighter => "격투가",
            JobType.Butcher => "도살자",
            JobType.Guardian => "수호자",
            JobType.Assassin => "암살자",
            _ => "없음"
        };
    }
}