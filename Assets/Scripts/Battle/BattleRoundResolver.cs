using System.Collections.Generic;

public static class BattleRoundResolver
{
    public static RoundContext CreateRoundContext(
        IReadOnlyList<BattleRoundParticipant> participants,
        MonsterBattleState monsterState,
        int roundIndex)
    {
        RoundContext context = new RoundContext
        {
            RoundIndex = roundIndex,
            MonsterHpBefore = monsterState != null ? monsterState.CurrentHp : 0,
            MonsterHpAfter = monsterState != null ? monsterState.CurrentHp : 0
        };

        if (participants == null)
            return context;

        for (int i = 0; i < participants.Count; i++)
        {
            BattleRoundParticipant participant = participants[i];
            if (participant == null)
                continue;

            int playerIndex = participant.PlayerIndex;
            int cardValue = participant.CardValue;
            context.SubmittedNumbersByPlayerIndex[playerIndex] = cardValue;

            if (!context.NumberGroups.TryGetValue(cardValue, out List<int> group))
            {
                group = new List<int>();
                context.NumberGroups.Add(cardValue, group);
            }

            group.Add(playerIndex);
        }

        foreach (KeyValuePair<int, List<int>> pair in context.NumberGroups)
        {
            if (pair.Value.Count <= 1)
                continue;

            context.AnyDuplicate = true;
            for (int i = 0; i < pair.Value.Count; i++)
                context.DuplicatePlayerIndexes.Add(pair.Value[i]);
        }

        return context;
    }

    public static void ApplyDuplicateCancellations(
        IReadOnlyList<BattleRoundParticipant> participants,
        RoundContext context)
    {
        if (participants == null || context == null)
            return;

        for (int i = 0; i < participants.Count; i++)
        {
            BattleRoundParticipant participant = participants[i];
            if (participant == null)
                continue;

            AttackResult result = GetOrCreateAttackResult(context, participant);
            result.WasAttempted = true;
            result.BaseDamage = participant.CardValue;
            result.FinalDamage = participant.AttackPower;

            if (!context.DuplicatePlayerIndexes.Contains(participant.PlayerIndex))
                continue;

            participant.IsMiss = true;
            participant.AttackCancelled = true;
            participant.CancelReason = AttackCancelReason.DuplicateNumber;
            participant.AttackPower = 0;
            result.WasCancelled = true;
            result.DidHit = false;
            result.CancelReason = AttackCancelReason.DuplicateNumber;
            context.AnyAttackCancelled = true;
        }
    }

    public static AttackResult GetOrCreateAttackResult(RoundContext context, BattleRoundParticipant participant)
    {
        int playerIndex = participant != null ? participant.PlayerIndex : -1;
        if (!context.AttackResultsByPlayerIndex.TryGetValue(playerIndex, out AttackResult result))
        {
            result = new AttackResult
            {
                AttackerPlayerIndex = playerIndex,
                SubmittedNumber = participant != null ? participant.CardValue : 0,
                CancelReason = AttackCancelReason.None
            };
            context.AttackResultsByPlayerIndex.Add(playerIndex, result);
        }

        return result;
    }

    public static void MarkAttackCancelled(
        RoundContext context,
        BattleRoundParticipant participant,
        AttackCancelReason reason)
    {
        if (context == null || participant == null)
            return;

        participant.AttackCancelled = true;
        participant.CancelReason = reason;
        participant.AttackPower = 0;

        AttackResult result = GetOrCreateAttackResult(context, participant);
        result.WasAttempted = true;
        result.WasCancelled = true;
        result.DidHit = false;
        result.CancelReason = reason;
        result.FinalDamage = 0;
        context.AnyAttackCancelled = true;
    }

    public static void RecordDamage(
        RoundContext context,
        BattleRoundParticipant participant,
        int monsterHpBefore,
        int monsterHpAfter,
        bool applied)
    {
        if (context == null || participant == null)
            return;

        AttackResult result = GetOrCreateAttackResult(context, participant);
        result.WasAttempted = true;
        result.BaseDamage = participant.CardValue;
        result.FinalDamage = participant.AttackPower;
        result.WasCancelled = participant.IsMiss || participant.AttackCancelled;
        result.CancelReason = participant.CancelReason != AttackCancelReason.None
            ? participant.CancelReason
            : result.CancelReason;
        result.CancelReason = result.WasCancelled && result.CancelReason == AttackCancelReason.None
            ? AttackCancelReason.CharacterEffect
            : result.CancelReason;
        result.DidHit = applied;
        result.MonsterHpBefore = monsterHpBefore;
        result.MonsterHpAfter = monsterHpAfter;

        if (applied)
        {
            context.SuccessfulAttackerIndexes.Add(participant.PlayerIndex);
            context.DamageRecords.Add(new DamageRecord(
                participant.PlayerIndex,
                monsterHpBefore - monsterHpAfter,
                monsterHpBefore,
                monsterHpAfter));
        }

        context.MonsterHpAfter = monsterHpAfter;
        context.MonsterKilled = monsterHpAfter <= 0;
    }

    public static void FinalizeAttackFlags(RoundContext context, IReadOnlyList<BattleRoundParticipant> participants)
    {
        if (context == null || participants == null)
            return;

        bool allHit = participants.Count > 0;
        for (int i = 0; i < participants.Count; i++)
        {
            BattleRoundParticipant participant = participants[i];
            if (participant == null)
                continue;

            AttackResult result = GetOrCreateAttackResult(context, participant);
            if (participant.DirectAttackSkipped)
                continue;

            if (result.WasCancelled || !result.DidHit)
                allHit = false;
        }

        context.AllAttacksHit = allHit;
    }

    public static void RecordSupportDamage(
        RoundContext context,
        BattleRoundParticipant supporter,
        int monsterHpBefore,
        int monsterHpAfter,
        bool applied)
    {
        if (context == null || supporter == null)
            return;

        AttackResult result = GetOrCreateAttackResult(context, supporter);
        result.WasAttempted = true;
        result.WasSupportDamage = true;

        if (applied)
        {
            result.DidHit = true;
            context.SuccessfulAttackerIndexes.Add(supporter.PlayerIndex);
            context.DamageRecords.Add(new DamageRecord(
                supporter.PlayerIndex,
                monsterHpBefore - monsterHpAfter,
                monsterHpBefore,
                monsterHpAfter));
        }

        context.MonsterHpAfter = monsterHpAfter;
        context.MonsterKilled = monsterHpAfter <= 0;
    }
}
