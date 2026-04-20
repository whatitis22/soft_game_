using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class BattleAnimationSync : NetworkBehaviour
{
    public static BattleAnimationSync Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("BattleAnimationSync 중복 인스턴스가 감지되었습니다.");
            return;
        }

        Instance = this;
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        base.OnDestroy();
    }

    public void RequestPlayAnimation(int playerIndex, string triggerName)
    {
        if (string.IsNullOrWhiteSpace(triggerName))
            return;

        // 네트워크 없이 단일 실행 중일 때도 테스트 가능하도록 로컬 폴백
        if (!IsSpawned)
        {
            PlayLocal(playerIndex, triggerName);
            return;
        }

        FixedString64Bytes trigger = new FixedString64Bytes(triggerName);

        if (IsServer)
        {
            PlayAnimationOnAllRpc(playerIndex, trigger);
        }
        else
        {
            RequestPlayAnimationRpc(playerIndex, trigger);
        }
    }

    [Rpc(SendTo.Server)]
    private void RequestPlayAnimationRpc(int playerIndex, FixedString64Bytes triggerName, RpcParams rpcParams = default)
    {
        if (!IsServer)
            return;

        PlayAnimationOnAllRpc(playerIndex, triggerName);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void PlayAnimationOnAllRpc(int playerIndex, FixedString64Bytes triggerName)
    {
        PlayLocal(playerIndex, triggerName.ToString());
    }

    private void PlayLocal(int playerIndex, string triggerName)
    {
        if (!BattlePlayerStandingRegistry.TryGet(playerIndex, out BattlePlayerStanding standing) || standing == null)
        {
            Debug.LogWarning($"BattleAnimationSync: p{playerIndex + 1} 스탠딩을 찾지 못했습니다.");
            return;
        }

        standing.PlayCustom(triggerName);
    }
}