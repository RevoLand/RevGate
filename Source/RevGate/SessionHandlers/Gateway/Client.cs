using NetCoreServer;
using RevGate.SilkroadSecurityApi;
using System;
using System.Threading.Tasks;

namespace RevGate.SessionHandlers.Gateway
{
    internal class Client : BaseToClient
    {
        public Client(TcpServer server) : base(server)
        {
            OnConnectedEvent += () =>
            {
                Task.Run(HandlePackets, Cancellation.Token);
                Task.Run(HandleOutgoingPackets, Cancellation.Token);

                ProxyToServer = new Server("10.0.0.0", 28362, this, Cancellation);
                ProxyToServer.ConnectAsync();
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
                        Console.WriteLine($"[P->C | In][{packet.Opcode:X4}]{Environment.NewLine}{Utility.HexDump(packet.GetBytes())}{Environment.NewLine}");
                        switch (packet.Opcode)
                        {
                            case 0x2001:
                            case 0x5000:
                            case 0x9000:
                                continue;
                            default:
                                ProxyToServer.Security.Send(packet);
                                break;
                        }
                    }

                    OutgoingPacketsMre.Set();
                    ProxyToServer.IncomingPacketsMre.Set();
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