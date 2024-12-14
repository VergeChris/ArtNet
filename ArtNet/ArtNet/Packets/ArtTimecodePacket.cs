using System;
using VergeAero.ArtNet.IO;
using VergeAero.Wrapper;

namespace VergeAero.ArtNet.Packets
{
    public class ArtTimecodePacket : ArtNetPacket
    {
        public ArtTimecodePacket()
            : base(ArtNetOpCodes.TimeCode)
        {
        }

        public ArtTimecodePacket(ArtNetReceiveData data)
            : base(data)
        {

        }
        
        public enum TimecodeType
        {
            Film,
            EBU,
            DF,
            SMPTE
        }

        public float TimecodeTypeToFrameRate(TimecodeType type)
        {
            switch (type)
            {
                case TimecodeType.Film:
                    return 24;
                case TimecodeType.EBU:
                    return 25;
                case TimecodeType.DF:
                    return 29.97f;
                case TimecodeType. SMPTE:
                    return 30;
            }

            throw new NotImplementedException();
        }
        
        #region Packet Properties

        private SMPTETimecode _timecode = new SMPTETimecode(30);

        public SMPTETimecode Timecode
        {
            get { return _timecode; }
            set { _timecode = value; }
        }

        private byte _streamID;
        public byte StreamID
        {
            get
            {
                return _streamID;
            }
            set
            {
                _streamID = value;
            }
        }

        #endregion

        public override void ReadData(ArtNetBinaryReader data)
        {
            base.ReadData(data);
            var filler1 = data.ReadByte();
            _streamID = data.ReadByte();
            var frame = data.ReadByte();
            var seconds = data.ReadByte();
            var minutes = data.ReadByte();
            var hours = data.ReadByte();
            var type = (TimecodeType)data.ReadByte();
            _timecode = new SMPTETimecode(hours, minutes, seconds, frame, TimecodeTypeToFrameRate(type));
        }

        public override void WriteData(ArtNetBinaryWriter data)
        {
            base.WriteData(data);

            data.WriteByte(0);
            data.WriteByte(_streamID);
            data.WriteByte(_timecode.Frame);
            data.WriteByte(_timecode.Seconds);
            data.WriteByte(_timecode.Minutes);
            data.WriteByte(_timecode.Hours);
            switch (_timecode.FrameRate)
            {
                case 24:
                    data.WriteByte(0);
                    break;
                case 25:
                    data.WriteByte(1);
                    break;
                case 29.97f:
                    data.WriteByte(2);
                    break;
                case 30:
                    data.WriteByte(3);
                    break;
                default:
                    data.WriteByte(3);
                    break;
            }
        }

    }
}