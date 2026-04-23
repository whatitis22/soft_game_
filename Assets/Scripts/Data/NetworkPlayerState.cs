using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayerState : NetworkBehaviour
{
    [Header("공개 상태")]
    public NetworkVariable<int> PlayerIndex =
        new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<FixedString64Bytes> DisplayNickname =
        new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<JobType> Job =
        new(JobType.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> CrystalRed =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> CrystalBlue =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> CrystalGreen =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    public NetworkVariable<bool> IsReady =
        new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> SubmittedCard =
        new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("개인 비공개 상태")]
    // bit 0 = 카드1, bit 1 = 카드2 ... bit 5 = 카드6
    public NetworkVariable<byte> RemainingNumberCardsMask =
        new(0b0011_1111, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    public NetworkList<int> InventoryItemIds;

    private void Awake()
    {
        InventoryItemIds = new NetworkList<int>();
    }

    public override void OnDestroy()
    {
        NetworkPlayerRegistry.Unregister(this);

        if (InventoryItemIds != null)
            InventoryItemIds.Dispose();

        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        NetworkPlayerRegistry.Register(this);

        if (IsOwner)
        {
            Debug.Log($"내 PlayerObject 스폰 완료 / OwnerClientId={OwnerClientId}");
        }
    }

    public override void OnNetworkDespawn()
    {
        NetworkPlayerRegistry.Unregister(this);
    }

    public void ServerInitializePlayer(int playerIndex, string nickname, JobType initialJob)
    {
        if (!IsServer)
            return;

        PlayerIndex.Value = playerIndex;
        DisplayNickname.Value = new FixedString64Bytes(nickname ?? string.Empty);
        Job.Value = initialJob;

        CrystalRed.Value = 0;
        CrystalBlue.Value = 0;
        CrystalGreen.Value = 0;

        RemainingNumberCardsMask.Value = 0b0011_1111;
        InventoryItemIds.Clear();

        IsReady.Value = false;

        SubmittedCard.Value = -1;
    }


    public void ServerAddCrystals(int redDelta, int blueDelta, int greenDelta)
    {
        if (!IsServer)
            return;

        CrystalRed.Value = Mathf.Max(0, CrystalRed.Value + redDelta);
        CrystalBlue.Value = Mathf.Max(0, CrystalBlue.Value + blueDelta);
        CrystalGreen.Value = Mathf.Max(0, CrystalGreen.Value + greenDelta);
    }

    public void ServerSetJob(JobType newJob)
    {
        if (!IsServer)
            return;

        Job.Value = newJob;
    }

    public void ServerAddItem(ItemId itemId)
    {
        if (!IsServer || itemId == ItemId.None)
            return;

        InventoryItemIds.Add((int)itemId);
    }

    public void ServerRemoveItem(ItemId itemId)
    {
        if (!IsServer)
            return;

        int target = (int)itemId;
        for (int i = 0; i < InventoryItemIds.Count; i++)
        {
            if (InventoryItemIds[i] == target)
            {
                InventoryItemIds.RemoveAt(i);
                return;
            }
        }
    }

    public bool HasNumberCard(int cardValue)
    {
        if (cardValue < 1 || cardValue > 6)
            return false;

        byte mask = (byte)(1 << (cardValue - 1));
        return (RemainingNumberCardsMask.Value & mask) != 0;
    }

    public void ServerConsumeNumberCard(int cardValue)
    {
        if (!IsServer || cardValue < 1 || cardValue > 6)
            return;

        byte mask = (byte)(1 << (cardValue - 1));
        byte current = RemainingNumberCardsMask.Value;

        if ((current & mask) == 0)
            return;

        RemainingNumberCardsMask.Value = (byte)(current & ~mask);
    }

    public void ServerSetReady(bool ready)
    {
        if (!IsServer)
            return;

        IsReady.Value = ready;
    }

    public void ServerClearSubmittedCard()
    {
        if (!IsServer)
            return;

        SubmittedCard.Value = -1;
    }

    [Rpc(SendTo.Server)]
    public void RequestUseNumberCardRpc(int cardValue, RpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != OwnerClientId)
        {
            SendCommandFeedback(senderId, "자신의 카드만 제출할 수 있습니다.");
            return;
        }

        if (cardValue < 1 || cardValue > 6)
        {
            SendCommandFeedback(senderId, "유효하지 않은 카드입니다.");
            return;
        }

        // 이미 이번 라운드에 제출했으면 무시
        if (SubmittedCard.Value >= 1 && SubmittedCard.Value <= 6)
        {
            SendCommandFeedback(senderId, "이미 이번 라운드에 카드를 제출했습니다.");
            return;
        }

        // 아직 남아 있는 카드인지 확인
        if (!HasNumberCard(cardValue))
        {
            SendCommandFeedback(senderId, $"카드 {cardValue}는 이미 사용했거나 보유하지 않았습니다.");
            return;
        }

        // 서버 확정
        SubmittedCard.Value = cardValue;
        ServerConsumeNumberCard(cardValue);

        Debug.Log($"PlayerIndex={PlayerIndex.Value} / Card={cardValue} 제출");
    }

    [Rpc(SendTo.Server)]
    public void RequestChangeJobRpc(JobType targetJob, RpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != OwnerClientId)
        {
            SendCommandFeedback(senderId, "자신의 직업만 변경할 수 있습니다.");
            return;
        }

        if (JobSelectionManager.Instance == null)
        {
            SendCommandFeedback(senderId, "직업 선택 시스템이 아직 준비되지 않았습니다.");
            return;
        }

        if (!JobSelectionManager.Instance.ServerTryAssignJob(this, targetJob, out string failureReason))
            SendCommandFeedback(senderId, failureReason);
    }

    [Rpc(SendTo.Server)]
    public void RequestSetReadyRpc(bool ready, RpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != OwnerClientId)
        {
            SendCommandFeedback(senderId, "자신의 준비 상태만 변경할 수 있습니다.");
            return;
        }

        // 호스트는 준비 토글 대상이 아님
        if (NetworkSessionState.Instance != null &&
            senderId == NetworkSessionState.Instance.HostClientId.Value)
        {
            SendCommandFeedback(senderId, "호스트는 준비 토글 대상이 아닙니다.");
            return;
        }

        IsReady.Value = ready;
    }

    [ClientRpc]
    private void ShowCommandFeedbackClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        ShowLocalCommandFeedback(message);
    }

    private void SendCommandFeedback(ulong clientId, string message)
    {
        if (string.IsNullOrWhiteSpace(message) || NetworkManager == null)
            return;

        if (clientId == NetworkManager.ServerClientId)
        {
            ShowLocalCommandFeedback(message);
            return;
        }

        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        };

        ShowCommandFeedbackClientRpc(message, rpcParams);
    }

    private void ShowLocalCommandFeedback(string message)
    {
        GameStatusMessageRouter.Show(message);
    }
}
