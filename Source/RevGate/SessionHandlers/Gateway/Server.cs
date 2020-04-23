using RevGate.SilkroadSecurityApi;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RevGate.SessionHandlers.Gateway
{
    internal class Server : BaseToServer
    {
        public Server(string address, int port, BaseToClient session, CancellationTokenSource cancellation) : base(address, port, session, cancellation)
        {
            OnConnectedEvent += () =>
            {
                Task.Run(HandlePackets, Cancellation.Token);
                Task.Run(HandleOutgoingPackets, Cancellation.Token);
            };
        }

        protected override void HandlePackets()
        {
            try
            {
                while (!Cancellation.IsCancellationRequested)
                {
                    IncomingPacketsMre.WaitOne();
                    Cancellation.Token.ThrowIfCancellationRequested();

                    var packets = Security.TransferIncoming();
                    Packet newPacket;
                    foreach (var packet in packets)
                    {
                        //Console.WriteLine($"[S->P | In][{packet.Opcode:X4}]{Environment.NewLine}{Utility.HexDump(packet.GetBytes())}{Environment.NewLine}");
                        switch (packet.Opcode)
                        {
                            case 0x5000:
                            case 0x9000:
                                continue;
                            case 0xA102:
                                var res = packet.ReadByte();
                                if (res == 1)
                                {
                                    var id = packet.ReadInt();

                                    newPacket = new Packet(0xA102, true);
                                    newPacket.WriteByte(1);
                                    newPacket.WriteUInt32(id);

                                    newPacket.WriteAscii("10.0.0.0");
                                    newPacket.WriteUInt16(15884);
                                    newPacket.WriteInt(0);

                                    ProxyToClient.Security.Send(newPacket);
                                    continue;
                                }
                                break;

                            case 0x2322:
                                newPacket = new Packet(0x6323);
                                newPacket.WriteAscii("1");
                                Security.Send(newPacket);
                                continue;
                        }
                        ProxyToClient.Security.Send(packet);
                    }

                    OutgoingPacketsMre.Set();
                    if (packets.Count > 0)
                    {
                        ProxyToClient.IncomingPacketsMre.Set();
                    }

                    IncomingPacketsMre.Reset();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}