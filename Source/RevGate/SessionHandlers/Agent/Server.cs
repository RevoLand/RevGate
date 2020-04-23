using RevGate.SilkroadSecurityApi;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RevGate.SessionHandlers.Agent
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
                    foreach (var packet in packets)
                    {
                        //Console.WriteLine($"[S->P | In][{packet.Opcode:X4}]{Environment.NewLine}{Utility.HexDump(packet.GetBytes())}{Environment.NewLine}");
                        switch (packet.Opcode)
                        {
                            case 0x5000:
                            case 0x9000:
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