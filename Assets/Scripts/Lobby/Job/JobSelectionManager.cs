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
        JobType.Assassin,
        JobType.Defender
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
        // 자동 배정은 고정 우선순위를 따라 아직 비어 있는 직업을 찾는다.
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

        // 현재 스폰된 플레이어 상태를 기준으로 직업 점유 여부를 판단한다.
        foreach (NetworkPlayerState player in NetworkPlayerRegistry.GetSpawnedPlayers())
        {
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
        return ServerTryAssignJob(player, targetJob, out _);
    }

    public bool ServerTryAssignJob(NetworkPlayerState player, JobType targetJob, out string failureReason)
    {
        // 직업 변경은 서버가 최종 확정하고, 실패 시 요청자에게 보여줄 이유를 함께 돌려준다.
        if (player == null || !player.IsServer)
        {
            failureReason = "직업 변경은 호스트가 확정해야 합니다.";
            return false;
        }

        if (targetJob == JobType.None)
        {
            failureReason = "선택할 수 없는 직업입니다.";
            return false;
        }

        if (player.Job.Value == targetJob)
        {
            failureReason = string.Empty;
            return true;
        }

        if (IsJobTaken(targetJob, player.OwnerClientId))
        {
            failureReason = $"{GetJobDisplayName(targetJob)} 직업은 이미 다른 플레이어가 선택했습니다.";
            return false;
        }

        player.ServerSetJob(targetJob);
        failureReason = string.Empty;
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
            JobType.Defender => "\uB514\uD39C\uB354(\uB124\uD2B8\uB77C)",
            _ => "없음"
        };
    }
}
