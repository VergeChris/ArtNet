using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using VergeAero.ArtNet.IO;
using VergeAero.ArtNet.Packets;
using VergeAero.Net;
using VergeAero.Rdm;
using VergeAero.Sockets;

namespace VergeAero.ArtNet.Sockets
{
    public class ArtNetSocket : Socket, IRdmSocket, IListenInterface, IDMXStream, ITimecodeStream
    {
        public const int Port = 6454;

        public event UnhandledExceptionEventHandler UnhandledException;
        public event EventHandler<NewPacketEventArgs<ArtNetPacket>> NewPacket;
        public event EventHandler<NewPacketEventArgs<RdmPacket>> NewRdmPacket;
        public event EventHandler<NewPacketEventArgs<RdmPacket>> RdmPacketSent;
        private ISocketConfiguration _listenConfiguration;
        public bool IsOpen => IsListening();
        public bool IsListening() => PortOpen;
        
        private List<int> _filteredUniverses = new List<int>();
        public DateTime? LastPacket { get; protected set; } = null;
        private Dictionary<int, UniverseInfo> _dmxUniverseStats = new Dictionary<int, UniverseInfo>();
        public IEnumerable<UniverseInfo> UniverseStats => _dmxUniverseStats.Values;
        public ArtNetSocket(UId rdmId)
            : base(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp)
        {
            RdmId = rdmId;
        }

        public ArtNetSocket()
            : this(UId.Empty)
        {
        }

        /// <summary>
        /// Gets or sets the RDM Id to use when sending packets.
        /// </summary>
        public UId RdmId { get; protected set; }

        public bool PortOpen { get; set; } = false;

        public IPAddress LocalIP { get; protected set; }

        public IPAddress LocalSubnetMask { get; protected set; }

        public Func<ushort, ArtNetReceiveData, ArtNetPacket> CustomPacketCreator { get; set; }

        private static IPAddress GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
        {
            byte[] ipAdressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

            byte[] broadcastAddress = new byte[ipAdressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
            }
            return new IPAddress(broadcastAddress);
        }

        public IPAddress BroadcastAddress
        {
            get
            {
                if (LocalSubnetMask == null)
                    return IPAddress.Broadcast;
                return GetBroadcastAddress(LocalIP, LocalSubnetMask);
            }
        }

        public void Open(IPAddress localIp, IPAddress localSubnetMask, IPAddress bindAddress = null)
        {
            LocalIP = localIp;
            LocalSubnetMask = localSubnetMask;

            SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            Bind(new IPEndPoint(bindAddress ?? LocalIP, Port));
            SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
            PortOpen = true;

            StartReceive();
        }

        public void StartReceive()
        {
            try
            {
                EndPoint localPort = new IPEndPoint(IPAddress.Any, Port);
                var receiveState = new ArtNetReceiveData();
                BeginReceiveFrom(receiveState.buffer, 0, receiveState.bufferSize, SocketFlags.None, ref localPort, OnReceive, receiveState);
            }
            catch (Exception ex)
            {
                OnUnhandledException(new ApplicationException("An error ocurred while trying to start recieving ArtNet.", ex));
            }
        }

        private void OnReceive(IAsyncResult state)
        {
            EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);

            if (PortOpen)
            {
                try
                {
                    var receiveState = (ArtNetReceiveData)(state.AsyncState);
                    if (receiveState != null)
                    {
                        var socketFlags = SocketFlags.None;
                        receiveState.DataLength = EndReceiveFrom(state, ref remoteEndPoint);
                        //Protect against UDP loopback where we receive our own packets, except for poll/pollreply commands.
                        if (receiveState.Valid && (!LocalEndPoint.Equals(remoteEndPoint) ||
                            receiveState.OpCode == (ushort)ArtNetOpCodes.Poll ||
                            receiveState.OpCode == (ushort)ArtNetOpCodes.PollReply))
                        {
                            LastPacket = DateTime.Now;
                            IPEndPoint destination = new IPEndPoint(IPAddress.Any, Port);//new IPEndPoint(ipPacketInfo.Address, ((IPEndPoint)LocalEndPoint).Port);
                            ProcessPacket((IPEndPoint)remoteEndPoint, destination, ArtNetPacket.Create(receiveState, CustomPacketCreator));
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnUnhandledException(ex);
                }
                finally
                {
                    //Attempt to receive another packet.
                    StartReceive();
                }
            }
        }

        private void ProcessPacket(IPEndPoint source, IPEndPoint destination, ArtNetPacket packet)
        {
            if (packet != null)
            {
                NewPacket?.Invoke(this, new NewPacketEventArgs<ArtNetPacket>(source, destination, packet));

                if (packet is ArtRdmPacket rdmPacket && NewRdmPacket != null)
                {
                    RdmPacket rdm = RdmPacket.ReadPacket(new RdmBinaryReader(new MemoryStream(rdmPacket.RdmData)));
                    NewRdmPacket(this, new NewPacketEventArgs<RdmPacket>(source, destination, rdm));
                }
                else if (packet is ArtNetDmxPacket dmxPacket)
                {
                    //Convert to local universe
                    for (int i = 0; i < _filteredUniverses.Count; i++)
                    {
                        if (dmxPacket.Universe == _filteredUniverses[i])
                        {
                            var genericDMXPacket = new DMXPacket(dmxPacket.Sequence, (short)_filteredUniverses[i], dmxPacket.DmxData) { Protocol = DMXProtocol.Artnet };
                            _dmxUniverseStats[_filteredUniverses[i]].Update(genericDMXPacket);
                            foreach (var dmxTarget in _dmxTargets)
                            {
                                dmxTarget.OnReceiveDMXPacket(_filteredUniverses[i], genericDMXPacket);
                            }
                            break;
                        }
                    }
                }
                else if (packet is ArtTimecodePacket timecodePacket)
                {
                    foreach (var timecodeTarget in _timecodeTargets)
                    {
                        timecodeTarget.OnReceiveTimecode(timecodePacket.Timecode, this);
                    }
                }
            }
        }

        protected void OnUnhandledException(Exception ex)
        {
            UnhandledException?.Invoke(this, new UnhandledExceptionEventArgs((object)ex, false));
        }

        public void Send(ArtNetPacket packet)
        {
            SendTo(packet.ToArray(), new IPEndPoint(BroadcastAddress, Port));
        }

        public void Send(ArtNetPacket packet, RdmEndPoint address)
        {
            SendTo(packet.ToArray(), new IPEndPoint(address.IpAddress, Port));
        }

        public void SendRdm(RdmPacket packet, RdmEndPoint targetAddress, UId targetId)
        {
            SendRdm(packet, targetAddress, targetId, RdmId);
        }

        public void SendRdm(RdmPacket packet, RdmEndPoint targetAddress, UId targetId, UId sourceId)
        {
            //Fill in addition details
            packet.Header.SourceId = sourceId;
            packet.Header.DestinationId = targetId;

            //Sub Devices
            if (targetId is SubDeviceUId)
                packet.Header.SubDevice = ((SubDeviceUId)targetId).SubDeviceId;

            //Create Rdm Packet
            using (var rdmData = new MemoryStream())
            {
                var rdmWriter = new RdmBinaryWriter(rdmData);

                //Write the RDM packet
                RdmPacket.WritePacket(packet, rdmWriter);

                //Write the checksum
                rdmWriter.WriteHiLoInt16((short)(RdmPacket.CalculateChecksum(rdmData.GetBuffer()) + (int)RdmVersions.SubMessage + (int)DmxStartCodes.RDM));

                //Create sACN Packet
                var rdmPacket = new ArtRdmPacket();
                rdmPacket.Address = (byte)(targetAddress.Universe & 0x00FF);
                rdmPacket.Net = (byte)(targetAddress.Universe >> 8);
                rdmPacket.SubStartCode = (byte)RdmVersions.SubMessage;
                rdmPacket.RdmData = rdmData.ToArray();

                Send(rdmPacket, targetAddress);

                RdmPacketSent?.Invoke(this, new NewPacketEventArgs<RdmPacket>((IPEndPoint)LocalEndPoint, new IPEndPoint(targetAddress.IpAddress, Port), packet));
            }
        }

        public void SendRdm(List<RdmPacket> packets, RdmEndPoint targetAddress, UId targetId)
        {
            if (packets.Count < 1)
                throw new ArgumentException("Rdm packets list is empty.");

            RdmPacket primaryPacket = packets[0];

            //Create sACN Packet
            var rdmPacket = new ArtRdmSubPacket();
            rdmPacket.DeviceId = targetId;
            rdmPacket.RdmVersion = (byte)RdmVersions.SubMessage;
            rdmPacket.Command = primaryPacket.Header.Command;
            rdmPacket.ParameterId = primaryPacket.Header.ParameterId;
            rdmPacket.SubDevice = (short)primaryPacket.Header.SubDevice;
            rdmPacket.SubCount = (short)packets.Count;

            using (var rdmData = new MemoryStream())
            {
                var dataWriter = new RdmBinaryWriter(rdmData);

                foreach (RdmPacket item in packets)
                    RdmPacket.WritePacket(item, dataWriter, true);

                rdmPacket.RdmData = rdmData.ToArray();

                Send(rdmPacket, targetAddress);
            }
        }

        protected override void Dispose(bool disposing)
        {
            PortOpen = false;

            base.Dispose(disposing);
        }

        public void ConfigureListen(ISocketConfiguration config)
        {
            _listenConfiguration = config;
        }

        public void Open()
        {
            _listenConfiguration?.ConfigureSocket(this);
            PortOpen = true;
            StartReceive();
        }

        public void ClearDMXFilters()
        {
            _filteredUniverses.Clear();
            _dmxUniverseStats.Clear();
        }
        public void AddDMXFilter(int universe)
        {
            if(_filteredUniverses.Contains(universe))
                return;
            
            _filteredUniverses.Add(universe);
            _dmxUniverseStats.Add(universe, new UniverseInfo()
            {
                Universe = universe
            });
        }
        
        HashSet<IDMXTarget> _dmxTargets = new HashSet<IDMXTarget>();
        public void RegisterDMXTarget(IDMXTarget target)
        {
            _dmxTargets.Add(target);
        }

        public void RemoveDMXTarget(IDMXTarget target)
        {
            _dmxTargets.Remove(target);
        }

        HashSet<ITimecodeTarget> _timecodeTargets = new HashSet<ITimecodeTarget>();
        public bool IsTimecodeSourceActive => IsListening();
        public string SourceName => "Artnet";

        public void RegisterTimecodeTarget(ITimecodeTarget target)
        {
            _timecodeTargets.Add(target);
        }

        public void RemoveTimecodeTarget(ITimecodeTarget target)
        {
            _timecodeTargets.Remove(target);
        }
    }
}
