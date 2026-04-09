using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayerState : NetworkBehaviour
{
    [Header("공개 상태")]
    public NetworkVariable<int> PlayerIndex =
        new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<JobType> Job =
        new(JobType.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> CrystalRed =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> CrystalBlue =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> CrystalGreen =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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
        if (InventoryItemIds != null)
            InventoryItemIds.Dispose();

        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            Debug.Log($"내 PlayerObject 스폰 완료 / OwnerClientId={OwnerClientId}");
        }
    }

    public void ServerInitializePlayer(int playerIndex, JobType jobType)
    {
        if (!IsServer)
            return;

        PlayerIndex.Value = playerIndex;
        Job.Value = jobType;

        CrystalRed.Value = 0;
        CrystalBlue.Value = 0;
        CrystalGreen.Value = 0;

        RemainingNumberCardsMask.Value = 0b0011_1111;
        InventoryItemIds.Clear();
    }

    public void ServerAddCrystals(int redDelta, int blueDelta, int greenDelta)
    {
        if (!IsServer)
            return;

        CrystalRed.Value = Mathf.Max(0, CrystalRed.Value + redDelta);
        CrystalBlue.Value = Mathf.Max(0, CrystalBlue.Value + blueDelta);
        CrystalGreen.Value = Mathf.Max(0, CrystalGreen.Value + greenDelta);
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

    [Rpc(SendTo.Server)]
    public void RequestUseNumberCardRpc(int cardValue, RpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != OwnerClientId)
            return;

        if (!HasNumberCard(cardValue))
            return;

        ServerConsumeNumberCard(cardValue);
        Debug.Log($"PlayerIndex={PlayerIndex.Value} / Card={cardValue} 사용");
    }
}