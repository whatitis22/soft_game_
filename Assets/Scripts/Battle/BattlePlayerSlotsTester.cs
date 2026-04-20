using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BattlePlayerSlots))]
public class BattlePlayerSlotsTester : MonoBehaviour
{
    [Serializable]
    private class PlayerTestSelection
    {
        public int playerIndex;
        public string selectedAnimation;
    }

    [SerializeField] private List<PlayerTestSelection> selections = new List<PlayerTestSelection>();

    private BattlePlayerSlots slots;

    private void Awake()
    {
        slots = GetComponent<BattlePlayerSlots>();
    }

    public BattlePlayerSlots Slots
    {
        get
        {
            if (slots == null)
                slots = GetComponent<BattlePlayerSlots>();
            return slots;
        }
    }

    public List<int> GetCurrentPlayerIndices()
    {
        List<int> result = new List<int>();

        if (Slots == null || Slots.SpawnedStandings == null)
            return result;

        foreach (var pair in Slots.SpawnedStandings)
            result.Add(pair.Key);

        result.Sort();
        SyncSelections(result);

        return result;
    }

    public string[] GetAnimationOptions(int playerIndex)
    {
        if (Slots == null)
            return Array.Empty<string>();

        if (!Slots.TryGetStanding(playerIndex, out BattlePlayerStanding standing) || standing == null)
            return Array.Empty<string>();

        return standing.GetAvailableAnimationNames();
    }

    public string GetSelectedAnimation(int playerIndex)
    {
        PlayerTestSelection data = GetOrCreateSelection(playerIndex);
        return data.selectedAnimation;
    }

    public void SetSelectedAnimation(int playerIndex, string animationName)
    {
        PlayerTestSelection data = GetOrCreateSelection(playerIndex);
        data.selectedAnimation = animationName;
    }

    public void PlaySelected(int playerIndex)
    {
        if (Slots == null)
            return;

        if (!Slots.TryGetStanding(playerIndex, out BattlePlayerStanding standing) || standing == null)
        {
            Debug.LogWarning($"BattlePlayerSlotsTester: p{playerIndex + 1} 스탠딩을 찾지 못했습니다.");
            return;
        }

        PlayerTestSelection data = GetOrCreateSelection(playerIndex);
        if (string.IsNullOrWhiteSpace(data.selectedAnimation))
        {
            string[] options = standing.GetAvailableAnimationNames();
            if (options.Length > 0)
                data.selectedAnimation = options[0];
        }

        if (string.IsNullOrWhiteSpace(data.selectedAnimation))
            return;

        if (BattleAnimationSync.Instance != null)
        {
            BattleAnimationSync.Instance.RequestPlayAnimation(playerIndex, data.selectedAnimation);
        }
        else
        {
            // 네트워크 동기 오브젝트가 없을 때는 로컬 테스트만
            if (!standing.TryPlayByName(data.selectedAnimation))
            {
                Debug.LogWarning($"BattlePlayerSlotsTester: p{playerIndex + 1} 에서 '{data.selectedAnimation}' 재생 실패.");
            }
        }
    }

    private void SyncSelections(List<int> currentPlayerIndices)
    {
        for (int i = selections.Count - 1; i >= 0; i--)
        {
            if (!currentPlayerIndices.Contains(selections[i].playerIndex))
                selections.RemoveAt(i);
        }

        for (int i = 0; i < currentPlayerIndices.Count; i++)
        {
            int playerIndex = currentPlayerIndices[i];
            GetOrCreateSelection(playerIndex);
        }

        for (int i = 0; i < currentPlayerIndices.Count; i++)
        {
            int playerIndex = currentPlayerIndices[i];
            PlayerTestSelection data = GetOrCreateSelection(playerIndex);

            if (string.IsNullOrWhiteSpace(data.selectedAnimation))
            {
                string[] options = GetAnimationOptions(playerIndex);
                if (options.Length > 0)
                    data.selectedAnimation = options[0];
            }
        }
    }

    private PlayerTestSelection GetOrCreateSelection(int playerIndex)
    {
        for (int i = 0; i < selections.Count; i++)
        {
            if (selections[i].playerIndex == playerIndex)
                return selections[i];
        }

        PlayerTestSelection created = new PlayerTestSelection
        {
            playerIndex = playerIndex,
            selectedAnimation = string.Empty
        };

        selections.Add(created);
        return created;
    }
}