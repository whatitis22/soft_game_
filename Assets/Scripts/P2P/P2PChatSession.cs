using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class P2PChatSession : NetworkBehaviour
{
    private readonly NetworkVariable<FixedString128Bytes> hostNickname =
        new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool clientMessageShown;

    public override void OnNetworkSpawn()
    {
        hostNickname.OnValueChanged += OnHostNicknameChanged;

        if (IsServer)
        {
            string nick = P2PChatBootstrap.Instance != null
                ? P2PChatBootstrap.Instance.HostNickname
                : string.Empty;

            hostNickname.Value = new FixedString128Bytes(nick);
        }

        TryNotifyClientJoin();
    }

    public override void OnNetworkDespawn()
    {
        hostNickname.OnValueChanged -= OnHostNicknameChanged;
        clientMessageShown = false;
    }

    private void OnHostNicknameChanged(FixedString128Bytes previousValue, FixedString128Bytes newValue)
    {
        TryNotifyClientJoin();
    }

    private void TryNotifyClientJoin()
    {
        if (!IsClient || IsServer || clientMessageShown)
            return;

        string nick = hostNickname.Value.ToString();

        if (string.IsNullOrWhiteSpace(nick))
            return;

        clientMessageShown = true;
        P2PChatBootstrap.Instance.NotifyClientJoinedRoom(nick);
    }
}