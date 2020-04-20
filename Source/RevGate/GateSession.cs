using NetCoreServer;
using RevGate.SilkroadSecurityApi;
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace RevGate
{
    internal class GateSession : TcpSession
    {
        public GateClient ProxyToServer;
        public Security Security;
        public ManualResetEvent IncomingPacketsMre, OutgoingPacketsMre;
        private TransferBuffer _transferBuffer;
        private CancellationTokenSource _cancellation;

        public GateSession(TcpServer server) : base(server)
        {
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"Client connected with ID: {Id}");

            _cancellation = new CancellationTokenSource();
            Security = new Security();
            Security.GenerateSecurity(true, true, true);

            _transferBuffer = new TransferBuffer(4096, 0, 0);
            IncomingPacketsMre = new ManualResetEvent(false);
            OutgoingPacketsMre = new ManualResetEvent(false);

            Task.Run(async () => await HandlePackets(), _cancellation.Token);
            Task.Run(async () => await HandleOutgoingPackets(), _cancellation.Token);

            ProxyToServer = new GateClient("192.168.1.100", 28362, this, _cancellation);
            ProxyToServer.ConnectAsync();
        }

        protected override void OnDisconnected()
        {
            _cancellation.Cancel();
            ProxyToServer.DisconnectAndStop();
            Console.WriteLine($"Client with Id: {Id} disconnected!");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Security.Recv(buffer, (int)offset, (int)size);
            IncomingPacketsMre.Set();
            OutgoingPacketsMre.Set();
        }

        private Task HandlePackets()
        {
            try
            {
                for (; ; )
                {
                    IncomingPacketsMre.WaitOne();
                    _cancellation.Token.ThrowIfCancellationRequested();

                    var packets = Security.TransferIncoming();
                    foreach (var packet in packets)
                    {
                        Console.WriteLine($"[P->C | In][{packet.Opcode:X4}]{Environment.NewLine}{Utility.HexDump(packet.GetBytes())}{Environment.NewLine}");
                        switch (packet.Opcode)
                        {
                            case 0x5000:
                            case 0x9000:
                                continue;
                            default:
                                ProxyToServer.security.Send(packet);
                                break;
                        }
                    }

                    OutgoingPacketsMre.Set();
                    ProxyToServer.IncomingPacketsMre.Set();
                    IncomingPacketsMre.Reset();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private Task HandleOutgoingPackets()
        {
            try
            {
                for (; ; )
                {
                    OutgoingPacketsMre.WaitOne();
                    _cancellation.Token.ThrowIfCancellationRequested();

                    var packets = Security.TransferOutgoing();
                    foreach (var packet in packets)
                    {
                        Console.WriteLine($"[C->P | Out][{packet.Value.Opcode:X4}]{Environment.NewLine}{Utility.HexDump(packet.Key.Buffer)}{Environment.NewLine}");
                        SendAsync(packet.Key.Buffer, packet.Key.Offset, packet.Key.Size);
                    }

                    OutgoingPacketsMre.Reset();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        protected override void OnError(SocketError error)
        {
            Debug.WriteLine(error);
            Console.WriteLine($"Chat TCP session caught an error with code {error}");
        }
    }
}