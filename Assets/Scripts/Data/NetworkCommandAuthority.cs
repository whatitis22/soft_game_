using Unity.Netcode;

public static class NetworkCommandAuthority
{
    public static bool CanRunHostOwnedCommand(ulong senderClientId)
    {
        // Host 모드에서는 ServerClientId가 곧 방장 클라이언트다.
        if (senderClientId == NetworkManager.ServerClientId)
            return true;

        // HostClientId가 따로 동기화된 뒤에는 그 값을 기준으로 방장 명령을 판정한다.
        NetworkSessionState session = NetworkSessionState.Instance;
        if (session == null)
            return false;

        ulong hostClientId = session.HostClientId.Value;
        return hostClientId != ulong.MaxValue && senderClientId == hostClientId;
    }

    public static bool CanLocalServerRunCommand(NetworkBehaviour behaviour)
    {
        // 로컬에서 바로 실행하는 명령도 서버 권한이 있는 인스턴스에서만 허용한다.
        return behaviour != null && behaviour.IsServer;
    }
}
