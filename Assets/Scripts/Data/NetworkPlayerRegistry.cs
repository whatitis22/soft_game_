using System.Collections.Generic;
using Unity.Netcode;

public static class NetworkPlayerRegistry
{
    private static readonly List<NetworkPlayerState> players = new List<NetworkPlayerState>();

    public static void Register(NetworkPlayerState player)
    {
        if (player == null)
            return;

        Compact();

        if (!players.Contains(player))
            players.Add(player);
    }

    public static void Unregister(NetworkPlayerState player)
    {
        if (player == null)
            return;

        players.Remove(player);
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
        players.Clear();
    }

    private static void Compact()
    {
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
        int result = a.PlayerIndex.Value.CompareTo(b.PlayerIndex.Value);
        if (result != 0)
            return result;

        return a.OwnerClientId.CompareTo(b.OwnerClientId);
    }
}
