namespace Server
{
    public enum MessageType
    {
        ClientHello = 1,
        ServerHello = 2,
        Ping = 8,
        Pong = 9,
        ServerUpdate = 10,
        ClientUpdateStarted = 11,
        ClientUpdateInProgress = 12,
        ClientUpdateInEnd = 13,
        Disconnected = 14,
    }
}
