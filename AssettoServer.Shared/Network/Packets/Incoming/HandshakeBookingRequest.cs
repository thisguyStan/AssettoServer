namespace AssettoServer.Shared.Network.Packets.Incoming;

public struct HandshakeBookingRequest : IIncomingNetworkPacket
{
    public ushort ClientVersion;
    public ulong Guid;
    public string Name;
    public string Team;
    public string Nation;
    public string RequestedCar;
    public string Password;
    public string? Features;
    public byte[]? SessionTicket;

    public void FromReader(PacketReader reader)
    {
        ClientVersion = reader.Read<ushort>();
        Guid = ulong.Parse(reader.ReadUTF8String());

        if (reader.Buffer.Length > reader.ReadPosition + 2)
        {
            Features = reader.ReadUTF8String(true);

            if (reader.Buffer.Length > reader.ReadPosition + 2)
            {
                short ticketLength = reader.Read<short>();
                if (ticketLength == reader.Buffer.Length - reader.ReadPosition)
                {
                    SessionTicket = new byte[ticketLength];
                    reader.ReadBytes(SessionTicket);
                }
            }
        }
    }

    public HandshakeRequest ToPickupPacket(string name, string team, string nation, string requestedCar, string password)
    {
        return new HandshakeRequest
        {
            ClientVersion = ClientVersion,
            Guid = Guid,
            Name = name,
            Team = team,
            Nation = nation,
            RequestedCar = requestedCar,
            Password = password,
            Features = Features,
            SessionTicket = SessionTicket,
        };
    }
}
