namespace SimpleUDPProtocol
{
    /// <summary>
    /// Types of messages. Reliable needs confirmation from our target. Unreliable does not need it. Ackonwledgment is packet responsible for confirmation
    /// </summary>
    public enum NetClientMessageType
    {
        Unreliable = 0,
        Reliable = 1,
        Ack = 2,
    }
}