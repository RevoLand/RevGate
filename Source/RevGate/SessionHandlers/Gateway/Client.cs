using RevGate.ServerHandlers;
using RevGate.SilkroadSecurityApi;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RevGate.SessionHandlers.Gateway
{
    internal class Client : BaseToClient
    {
        public Client(ServerBase server) : base(server)
        {
            OnConnectedEvent += () =>
            {
                ProxyToServer = new Server("10.0.0.0", 28362, this, Cancellation);
                ProxyToServer.Connect();

                while (!ProxyToServer.IsConnected && IsConnected)
                {
                    Console.WriteLine($"{Id} ToServer is not connected!!");
                    Thread.Sleep(100);
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
                    Console.WriteLine($"[Client] IncomingPacketsMre active");
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
                                //case 0x6323: // captcha?
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
            catch (OperationCanceledException e)
            {
                Console.WriteLine(e);
                throw;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}