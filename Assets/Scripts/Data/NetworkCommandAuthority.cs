using Unity.Netcode;

public static class NetworkCommandAuthority
{
    public static bool CanRunHostOwnedCommand(ulong senderClientId)
    {
        if (senderClientId == NetworkManager.ServerClientId)
            return true;

        NetworkSessionState session = NetworkSessionState.Instance;
        if (session == null)
            return false;

        ulong hostClientId = session.HostClientId.Value;
        return hostClientId != ulong.MaxValue && senderClientId == hostClientId;
    }

    public static bool CanLocalServerRunCommand(NetworkBehaviour behaviour)
    {
        return behaviour != null && behaviour.IsServer;
    }
}
