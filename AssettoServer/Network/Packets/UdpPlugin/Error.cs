﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssettoServer.Network.Packets.Outgoing;

namespace AssettoServer.Network.Packets.UdpPlugin
{
    public readonly record struct Error : IOutgoingNetworkPacket
    {
        public string? Message { get; init; }

        public void ToWriter(ref PacketWriter writer)
        {
            writer.Write((byte)UdpPluginProtocol.Error);
            writer.WriteString(Message, Encoding.UTF32);
        }
    }
}
