﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VergeAero.Rdm.Packets.Net
{
    public class DiscoveryState
    {
        public enum DiscoveryStates
        {
            Incomplete = 0x0,
            Incremental = 0x2,
            Full = 0x1,
            NotActive = 0x4
        }

        public class Get : RdmRequestPacket
        {
            public Get()
                : base(RdmCommands.Get, RdmParameters.DiscoveryState)
            {
            }

            public short EndpointID { get; set; }

            protected override void ReadData(RdmBinaryReader data)
            {
                EndpointID = data.ReadHiLoInt16();
            }

            protected override void WriteData(RdmBinaryWriter data)
            {
                data.WriteHiLoInt16(EndpointID);
            }
        }

        public class GetReply : RdmResponsePacket
        {
            public GetReply()
                : base(RdmCommands.GetResponse, RdmParameters.DiscoveryState)
            {
            }

            public short EndpointID { get; set; }

            public short DeviceCount { get; set; }

            public DiscoveryStates DiscoveryState { get; set; }

            protected override void ReadData(RdmBinaryReader data)
            {
                EndpointID = data.ReadHiLoInt16();
                DeviceCount = data.ReadHiLoInt16();
                DiscoveryState = (DiscoveryStates)data.ReadByte();

            }

            protected override void WriteData(RdmBinaryWriter data)
            {
                data.WriteHiLoInt16(EndpointID);
                data.WriteHiLoInt16(DeviceCount);
                data.WriteByte((byte)DiscoveryState);
            }
        }

        public class Set : RdmRequestPacket
        {
            public Set()
                : base(RdmCommands.Set, RdmParameters.DiscoveryState)
            {
            }

            public short EndpointID { get; set; }

            public DiscoveryStates DiscoveryState { get; set; }

            protected override void ReadData(RdmBinaryReader data)
            {
                EndpointID = data.ReadHiLoInt16();
                DiscoveryState = (DiscoveryStates)data.ReadByte();

            }

            protected override void WriteData(RdmBinaryWriter data)
            {
                data.WriteHiLoInt16(EndpointID);
                data.WriteByte((byte)DiscoveryState);
            }
        }

        public class SetReply : RdmResponsePacket
        {
            public SetReply()
                : base(RdmCommands.SetResponse, RdmParameters.DiscoveryState)
            {
            }
            
            protected override void ReadData(RdmBinaryReader data)
            {
                //Parameter Data Empty
            }

            protected override void WriteData(RdmBinaryWriter data)
            {
                //Parameter Data Empty
            }
        }
    }
}
