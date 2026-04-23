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

        if (NetworkCommandAuthority.CanLocalServerRunCommand(this))
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

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (!NetworkCommandAuthority.CanRunHostOwnedCommand(senderId))
        {
            WarnAnimationCommandDenied(senderId);
            return;
        }

        PlayAnimationOnAllRpc(playerIndex, triggerName);
    }

    [ClientRpc]
    private void ShowAnimationCommandDeniedClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        GameStatusMessageRouter.Show(message);
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

    private void WarnAnimationCommandDenied(ulong clientId)
    {
        string message = "전투 애니메이션 명령은 호스트만 사용할 수 있습니다.";

        if (NetworkManager == null)
            return;

        if (clientId == NetworkManager.ServerClientId)
        {
            GameStatusMessageRouter.Show(message);
            return;
        }

        ClientRpcParams rpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        };

        ShowAnimationCommandDeniedClientRpc(message, rpcParams);
        Debug.LogWarning($"[AnimationCommandDenied] clientId={clientId}");
    }
}
