using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(MonsterBattleState))]
public class NetworkMonsterState : NetworkBehaviour
{
    [SerializeField] private string hpTextObjectName = "HP Text";

    public NetworkVariable<FixedString64Bytes> MonsterId =
        new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<FixedString128Bytes> MonsterName =
        new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> CurrentHp =
        new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> MaxHp =
        new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> DeathAnimationSequence =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private MonsterBattleState monsterState;
    private bool applyingNetworkState;

    public void InitializeBeforeSpawn(MonsterData monsterData, string hpTextName)
    {
        hpTextObjectName = string.IsNullOrWhiteSpace(hpTextName) ? "HP Text" : hpTextName;
        CacheReferences();

        if (monsterState != null)
            monsterState.Initialize(monsterData, hpTextObjectName);

        MonsterId.Value = new FixedString64Bytes(monsterData != null ? monsterData.id ?? string.Empty : string.Empty);
        MonsterName.Value = new FixedString128Bytes(monsterData != null ? monsterData.monster_name ?? string.Empty : string.Empty);
        MaxHp.Value = monsterState != null ? monsterState.MaxHp : Mathf.Max(1, monsterData != null ? monsterData.HP : 1);
        CurrentHp.Value = monsterState != null ? monsterState.CurrentHp : MaxHp.Value;
        DeathAnimationSequence.Value = 0;
    }

    public override void OnNetworkSpawn()
    {
        CacheReferences();
        SubscribeNetworkVariables();

        if (IsServer && monsterState != null)
            monsterState.HpChanged += HandleMonsterHpChanged;

        ApplyNetworkValuesToState(resetDeathState: true);

        if (DeathAnimationSequence.Value > 0 && monsterState != null)
            monsterState.PlayPendingDeathAnimation();
    }

    public override void OnNetworkDespawn()
    {
        UnsubscribeNetworkVariables();

        if (monsterState != null)
            monsterState.HpChanged -= HandleMonsterHpChanged;
    }

    public void ServerRefreshHpFromState()
    {
        if (!CanServerWrite() || monsterState == null)
            return;

        MaxHp.Value = monsterState.MaxHp;
        CurrentHp.Value = monsterState.CurrentHp;
    }

    public void ServerPlayDeathAnimation()
    {
        if (!CanServerWrite())
            return;

        CacheReferences();
        if (monsterState != null)
            monsterState.PlayPendingDeathAnimation();

        DeathAnimationSequence.Value = DeathAnimationSequence.Value + 1;
    }

    private void CacheReferences()
    {
        if (monsterState == null)
            monsterState = GetComponent<MonsterBattleState>();
    }

    private void SubscribeNetworkVariables()
    {
        CurrentHp.OnValueChanged += HandleCurrentHpChanged;
        MaxHp.OnValueChanged += HandleMaxHpChanged;
        MonsterName.OnValueChanged += HandleMonsterNameChanged;
        DeathAnimationSequence.OnValueChanged += HandleDeathAnimationSequenceChanged;
    }

    private void UnsubscribeNetworkVariables()
    {
        CurrentHp.OnValueChanged -= HandleCurrentHpChanged;
        MaxHp.OnValueChanged -= HandleMaxHpChanged;
        MonsterName.OnValueChanged -= HandleMonsterNameChanged;
        DeathAnimationSequence.OnValueChanged -= HandleDeathAnimationSequenceChanged;
    }

    private void HandleMonsterHpChanged(MonsterBattleState state, int currentHp, int maxHp)
    {
        if (!CanServerWrite() || applyingNetworkState)
            return;

        MaxHp.Value = maxHp;
        CurrentHp.Value = currentHp;
    }

    private void HandleCurrentHpChanged(int previousValue, int newValue)
    {
        ApplyNetworkValuesToState(resetDeathState: newValue > 0);
    }

    private void HandleMaxHpChanged(int previousValue, int newValue)
    {
        ApplyNetworkValuesToState(resetDeathState: CurrentHp.Value > 0);
    }

    private void HandleMonsterNameChanged(FixedString128Bytes previousValue, FixedString128Bytes newValue)
    {
        ApplyMonsterNameToState();
    }

    private void HandleDeathAnimationSequenceChanged(int previousValue, int newValue)
    {
        if (newValue <= previousValue)
            return;

        CacheReferences();
        if (monsterState != null)
            monsterState.PlayPendingDeathAnimation();
    }

    private void ApplyNetworkValuesToState(bool resetDeathState)
    {
        CacheReferences();
        if (monsterState == null)
            return;

        applyingNetworkState = true;
        monsterState.ApplyNetworkState(MaxHp.Value, CurrentHp.Value, hpTextObjectName, resetDeathState);
        ApplyMonsterNameToState();
        applyingNetworkState = false;
    }

    private void ApplyMonsterNameToState()
    {
        CacheReferences();
        if (monsterState == null)
            return;

        monsterState.SetDisplayName(MonsterName.Value.ToString());
    }

    private bool CanServerWrite()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return manager == null || !manager.IsListening || manager.IsServer;
    }
}
