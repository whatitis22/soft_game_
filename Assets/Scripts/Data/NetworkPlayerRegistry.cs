using System.Collections.Generic;
using Unity.Netcode;

public static class NetworkPlayerRegistry
{
    private static readonly List<NetworkPlayerState> players = new List<NetworkPlayerState>();

    public static event System.Action PlayersChanged;

    public static void Register(NetworkPlayerState player)
    {
        if (player == null)
            return;

        // 씬 검색 대신 현재 스폰된 플레이어 목록을 한 곳에서 관리한다.
        Compact();

        if (players.Contains(player))
            return;

        players.Add(player);
        PlayersChanged?.Invoke();
    }

    public static void Unregister(NetworkPlayerState player)
    {
        if (player == null)
            return;

        // 디스폰/파괴된 플레이어는 즉시 목록에서 빠져야 UI가 낡은 상태를 보지 않는다.
        if (players.Remove(player))
            PlayersChanged?.Invoke();
    }

    public static List<NetworkPlayerState> GetSpawnedPlayers()
    {
        List<NetworkPlayerState> result = new List<NetworkPlayerState>();
        FillSpawnedPlayers(result);
        return result;
    }

    public static List<NetworkPlayerState> GetSpawnedPlayersSortedByIndex()
    {
        List<NetworkPlayerState> result = GetSpawnedPlayers();
        result.Sort(CompareByPlayerIndex);
        return result;
    }

    public static void FillSpawnedPlayers(List<NetworkPlayerState> target)
    {
        if (target == null)
            return;

        // 호출자가 재사용 가능한 리스트를 넘기면 할당을 줄일 수 있게 한다.
        target.Clear();
        Compact();

        for (int i = 0; i < players.Count; i++)
        {
            NetworkPlayerState player = players[i];
            if (IsAvailable(player))
                target.Add(player);
        }
    }

    public static bool TryGetLocalPlayer(out NetworkPlayerState player)
    {
        player = null;

        // 로컬 클라이언트 ID 기준으로 자기 PlayerObject를 찾는다.
        if (NetworkManager.Singleton == null)
            return false;

        return TryGetByOwnerClientId(NetworkManager.Singleton.LocalClientId, out player);
    }

    public static bool TryGetByOwnerClientId(ulong ownerClientId, out NetworkPlayerState player)
    {
        Compact();

        for (int i = 0; i < players.Count; i++)
        {
            NetworkPlayerState candidate = players[i];
            if (!IsAvailable(candidate))
                continue;

            if (candidate.OwnerClientId == ownerClientId)
            {
                player = candidate;
                return true;
            }
        }

        player = null;
        return false;
    }

    public static bool TryGetByPlayerIndex(int playerIndex, out NetworkPlayerState player)
    {
        Compact();

        for (int i = 0; i < players.Count; i++)
        {
            NetworkPlayerState candidate = players[i];
            if (!IsAvailable(candidate))
                continue;

            if (candidate.PlayerIndex.Value == playerIndex)
            {
                player = candidate;
                return true;
            }
        }

        player = null;
        return false;
    }

    public static int CountSpawnedPlayers()
    {
        int count = 0;
        Compact();

        for (int i = 0; i < players.Count; i++)
        {
            if (IsAvailable(players[i]))
                count++;
        }

        return count;
    }

    public static void Clear()
    {
        if (players.Count == 0)
            return;

        players.Clear();
        PlayersChanged?.Invoke();
    }

    private static void Compact()
    {
        // Unity 오브젝트 파괴 후 남는 null 참조를 접근 전에 정리한다.
        for (int i = players.Count - 1; i >= 0; i--)
        {
            if (players[i] == null)
                players.RemoveAt(i);
        }
    }

    private static bool IsAvailable(NetworkPlayerState player)
    {
        return player != null && player.IsSpawned;
    }

    private static int CompareByPlayerIndex(NetworkPlayerState a, NetworkPlayerState b)
    {
        // 표시 순서는 PlayerIndex가 우선이고, 동률이면 OwnerClientId로 안정성을 준다.
        int result = a.PlayerIndex.Value.CompareTo(b.PlayerIndex.Value);
        if (result != 0)
            return result;

        return a.OwnerClientId.CompareTo(b.OwnerClientId);
    }
}
