namespace Adventure3D.Core
{
    /// <summary>How the current process takes part in a session.</summary>
    public enum SessionMode
    {
        /// <summary>Single player on this device. No sockets are opened.</summary>
        Offline = 0,
        /// <summary>Authoritative server plus a local player in the same process (LAN host).</summary>
        Host = 1,
        /// <summary>Remote client that connects to a host or a dedicated server (LAN or online).</summary>
        Client = 2,
        /// <summary>Headless authoritative server without a local player, camera, UI or input.</summary>
        DedicatedServer = 3,
    }

    public enum SessionStatus
    {
        Idle = 0,
        Connecting = 1,
        Handshaking = 2,
        Running = 3,
        Ended = 4,
    }

    /// <summary>Why a session ended or failed. Mapped to Arabic messages by the presentation layer.</summary>
    public enum SessionEndReason
    {
        None = 0,
        UserLeft = 1,
        InvalidAddress = 2,
        ConnectTimeout = 3,
        HostNotFound = 4,
        HandshakeTimeout = 5,
        ConnectionLost = 6,
        ServerClosed = 7,
        IncompatibleVersion = 8,
        ServerFull = 9,
        ReplacedByNewConnection = 10,
        BindFailed = 11,
        TransportError = 12,
    }

    /// <summary>What the menu asks the session layer to start.</summary>
    public sealed class LaunchRequest
    {
        public SessionMode Mode;
        public string Address;
        public ushort Port;

        public static LaunchRequest Offline() => new LaunchRequest { Mode = SessionMode.Offline };

        public override string ToString() => $"{Mode} {Address}:{Port}";
    }

    public static class ProtocolInfo
    {
        /// <summary>Bump when the wire format or simulation rules change incompatibly.</summary>
        public const ushort ProtocolVersion = 2;
        public const string GameVersion = "0.1.0";
    }
}
