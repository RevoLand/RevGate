using NetCoreServer;
using RevGate.SilkroadSecurityApi;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RevGate.SessionHandlers.Agent
{
    internal class Client : BaseToClient
    {
        public Client(TcpServer server) : base(server)
        {
            OnConnectedEvent += () =>
            {
                ProxyToServer = new Server("10.0.0.0", 23781, this, Cancellation);
                ProxyToServer.ConnectAsync();

                while (!ProxyToServer.IsConnected)
                {
                    Console.WriteLine($"{Id} ToServer is not connected!!");
                    Thread.Sleep(1);
                }

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
                    foreach (var packet in packets)
                    {
                        //Console.WriteLine($"[P->C | In][{packet.Opcode:X4}]{Environment.NewLine}{Utility.HexDump(packet.GetBytes())}{Environment.NewLine}");
                        switch (packet.Opcode)
                        {
                            case 0x2001:
                            case 0x5000:
                            case 0x9000:
                                continue;
                        }
                        ProxyToServer.Security.Send(packet);
                    }

                    OutgoingPacketsMre.Set();
                    if (packets.Count > 0)
                    {
                        ProxyToServer.IncomingPacketsMre.Set();
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