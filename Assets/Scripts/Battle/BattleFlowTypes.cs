using System.Collections.Generic;

public enum BattleCrystalColor
{
    Red,
    Blue,
    Green
}

public sealed class BattleRoundParticipant
{
    public BattleRoundParticipant(NetworkPlayerState player, int cardValue)
    {
        Player = player;
        CardValue = cardValue;
        AttackPower = cardValue;
        RewardPriority = cardValue;
    }

    public NetworkPlayerState Player { get; }
    public int CardValue { get; }
    public int AttackPower { get; set; }
    public int RewardPriority { get; set; }
    public bool IsMiss { get; set; }
    public bool AttackCancelled { get; set; }
    public bool DirectAttackSkipped { get; set; }
    public AttackCancelReason CancelReason { get; set; }
    public bool AppliedDamage { get; set; }
    public bool ReceivedBaseReward { get; set; }
    public int PlayerIndex => Player != null ? Player.PlayerIndex.Value : -1;
}

public readonly struct BattleAttackContext
{
    public BattleAttackContext(BattleRoundParticipant participant, MonsterData monsterData, MonsterBattleState monsterState)
    {
        Participant = participant;
        MonsterData = monsterData;
        MonsterState = monsterState;
    }

    public BattleRoundParticipant Participant { get; }
    public MonsterData MonsterData { get; }
    public MonsterBattleState MonsterState { get; }
}

public interface IBattleAttackPowerModifier
{
    int ModifyAttackPower(BattleAttackContext context, int currentPower);
}

public interface IBattleRewardRankModifier
{
    void ModifyRewardRanks(List<BattleRoundParticipant> rewardRanks);
}
