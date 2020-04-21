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
                for (; ; )
                {
                    IncomingPacketsMre.WaitOne();
                    Cancellation.Token.ThrowIfCancellationRequested();

                    var packets = Security.TransferIncoming();
                    foreach (var packet in packets)
                    {
                        Console.WriteLine($"[S->P | In][{packet.Opcode:X4}]{Environment.NewLine}{Utility.HexDump(packet.GetBytes())}{Environment.NewLine}");
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

                                    var newPacket = new Packet(0xA102, true);
                                    newPacket.WriteByte(1);
                                    newPacket.WriteUInt32(id);

                                    newPacket.WriteAscii("10.0.0.0");
                                    newPacket.WriteUInt16("15884");
                                    newPacket.WriteInt(0);

                                    ProxyBaseToClient.Security.Send(newPacket);
                                }
                                break;

                            default:
                                ProxyBaseToClient.Security.Send(packet);
                                break;
                        }
                    }

                    OutgoingPacketsMre.Set();
                    ProxyBaseToClient.IncomingPacketsMre.Set();
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