using RevGate.SilkroadSecurityApi;
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using TcpClient = NetCoreServer.TcpClient;

namespace RevGate
{
    internal class GateClient : TcpClient
    {
        public readonly GateSession ProxyToClient;
        public Security security;
        public ManualResetEvent IncomingPacketsMre, OutgoingPacketsMre;
        private TransferBuffer _transferBuffer;
        private readonly CancellationTokenSource _cancellation;

        public GateClient(string address, int port, GateSession session, CancellationTokenSource cancellation) : base(address, port)
        {
            ProxyToClient = session;
            _cancellation = cancellation;
        }

        public void DisconnectAndStop()
        {
            DisconnectAsync();
            while (IsConnected)
                Thread.Yield();
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"Proxy connected to the server with Id: {Id}");
            security = new Security();
            _transferBuffer = new TransferBuffer(4096, 0, 0);
            IncomingPacketsMre = new ManualResetEvent(false);
            OutgoingPacketsMre = new ManualResetEvent(false);

            Task.Run(async () => await HandlePackets(), _cancellation.Token);
            Task.Run(async () => await HandleOutgoingPackets(), _cancellation.Token);
        }

        private Task HandlePackets()
        {
            try
            {
                for (; ; )
                {
                    IncomingPacketsMre.WaitOne();
                    _cancellation.Token.ThrowIfCancellationRequested();

                    var packets = security.TransferIncoming();
                    foreach (var packet in packets)
                    {
                        Console.WriteLine($"[S->P | In][{packet.Opcode:X4}]{Environment.NewLine}{Utility.HexDump(packet.GetBytes())}{Environment.NewLine}");
                        switch (packet.Opcode)
                        {
                            case 0x5000:
                            case 0x9000:
                                continue;
                            default:
                                ProxyToClient.Security.Send(packet);
                                break;
                        }
                    }

                    OutgoingPacketsMre.Set();
                    ProxyToClient.IncomingPacketsMre.Set();
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

                    var packets = security.TransferOutgoing();
                    foreach (var packet in packets)
                    {
                        Console.WriteLine($"[P->S | Out][{packet.Value.Opcode:X4}]{Environment.NewLine}{Utility.HexDump(packet.Key.Buffer)}{Environment.NewLine}");

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

        protected override void OnDisconnected()
        {
            Console.WriteLine($"Chat TCP client disconnected a session with Id {Id}");
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            security.Recv(buffer, (int)offset, (int)size);
            IncomingPacketsMre.Set();
            OutgoingPacketsMre.Set();
        }

        protected override void OnError(SocketError error)
        {
            Debug.WriteLine(error);
            Console.WriteLine($"Chat TCP client caught an error with code {error}");
        }
    }
}