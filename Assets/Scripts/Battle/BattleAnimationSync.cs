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

        // 네트워크 오브젝트가 없을 때는 에디터/로컬 테스트를 위해 바로 재생한다.
        if (!IsSpawned)
        {
            PlayLocal(playerIndex, triggerName);
            return;
        }

        FixedString64Bytes trigger = new FixedString64Bytes(triggerName);

        if (NetworkCommandAuthority.CanLocalServerRunCommand(this))
        {
            // Host는 서버 권한을 이미 갖고 있으므로 바로 모든 클라이언트에 브로드캐스트한다.
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

        // 원격 클라이언트 요청은 Host 소유 명령인지 서버에서 다시 검사한다.
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
        // 각 클라이언트는 자기 씬의 스탠딩 레지스트리에서 대상 캐릭터를 찾아 재생한다.
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

        // 권한 거절 메시지는 명령을 보낸 클라이언트에게만 돌려준다.
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
