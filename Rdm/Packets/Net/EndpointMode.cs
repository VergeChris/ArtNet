﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VergeAero.Rdm.Packets.Net
{
    public class EndpointMode
    {
        public enum EndpointModes
        {
            Disabled = 0x0,
            Input = 0x1,
            Output = 0x2
        }

        public class Get : RdmRequestPacket
        {
            public Get()
                : base(RdmCommands.Get, RdmParameters.EndpointMode)
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
                : base(RdmCommands.GetResponse, RdmParameters.EndpointMode)
            {
            }

            public short EndpointID { get; set; }

            public EndpointModes EndpointMode { get; set; }

            protected override void ReadData(RdmBinaryReader data)
            {
                EndpointID = data.ReadHiLoInt16();
                EndpointMode = (EndpointModes) data.ReadByte();
            }

            protected override void WriteData(RdmBinaryWriter data)
            {
                data.WriteHiLoInt16(EndpointID);
                data.WriteByte((byte) EndpointMode);
            }
        }

        public class Set : RdmRequestPacket
        {
            public Set()
                : base(RdmCommands.Set, RdmParameters.EndpointMode)
            {
            }
            
            public short EndpointID { get; set; }

            public EndpointModes EndpointMode { get; set; }

            protected override void ReadData(RdmBinaryReader data)
            {
                EndpointID = data.ReadHiLoInt16();
                EndpointMode = (EndpointModes) data.ReadByte();
            }

            protected override void WriteData(RdmBinaryWriter data)
            {
                data.WriteHiLoInt16(EndpointID);
                data.WriteByte((byte) EndpointMode);
            }
        }

        public class SetReply : RdmResponsePacket
        {
            public SetReply()
                : base(RdmCommands.SetResponse, RdmParameters.EndpointMode)
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
