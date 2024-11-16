using System;
using System.Net;
using VergeAero.ArtNet.Packets;
using VergeAero.Sockets;

namespace VergeAero.Samples
{
    public class SampleCapture : SampleBase
    {
        public SampleCapture(IPAddress localIp, IPAddress localSubnetMask)
            : base(localIp, localSubnetMask)
        {
        }

        protected override void Socket_NewPacket(object sender, NewPacketEventArgs<ArtNetPacket> e)
        {
            Console.WriteLine($"Received ArtNet packet with OpCode: {e.Packet.OpCode} from {e.Source}");
        }
    }
}
