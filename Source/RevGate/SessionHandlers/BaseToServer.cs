using RevGate.SilkroadSecurityApi;
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using TcpClient = NetCoreServer.TcpClient;

namespace RevGate.SessionHandlers
{
    internal abstract class BaseToServer : TcpClient
    {
        public readonly BaseToClient ProxyBaseToClient;
        public Security Security;
        public ManualResetEvent IncomingPacketsMre;
        public ManualResetEvent OutgoingPacketsMre;
        public readonly CancellationTokenSource Cancellation;

        #region Events

        protected event Action OnConnectedEvent;

        protected event Action OnDisconnectedEvent;

        protected event Action<byte[], long, long> OnReceivedEvent;

        protected event Action<SocketError> OnErrorEvent;

        #endregion Events

        protected BaseToServer(string address, int port, BaseToClient session, CancellationTokenSource cancellation) : base(address, port)
        {
            ProxyBaseToClient = session;
            Cancellation = cancellation;
        }

        protected override void OnConnected()
        {
            try
            {
                Console.WriteLine($"Proxy connected to the server with Id: {Id}");
                Security = new Security();
                IncomingPacketsMre = new ManualResetEvent(false);
                OutgoingPacketsMre = new ManualResetEvent(false);

                OnConnectedEvent?.Invoke();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        protected override void OnDisconnected()
        {
            Cancellation.Cancel();
            ProxyBaseToClient.Disconnect();
            Console.WriteLine($"Proxy Client Disconnected from Server: {Id}");

            OnDisconnectedEvent?.Invoke();
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            Security.Recv(buffer, (int)offset, (int)size);
            IncomingPacketsMre.Set();
            OutgoingPacketsMre.Set();

            OnReceivedEvent?.Invoke(buffer, offset, size);
        }

        protected override void OnError(SocketError error)
        {
            Debug.WriteLine(error);
            Console.WriteLine($"Server session caught an error with code {error}");

            OnErrorEvent?.Invoke(error);
        }

        protected abstract void HandlePackets();

        protected virtual void HandleOutgoingPackets()
        {
            try
            {
                for (; ; )
                {
                    OutgoingPacketsMre.WaitOne();
                    Cancellation.Token.ThrowIfCancellationRequested();

                    var packets = Security.TransferOutgoing();
                    foreach (var (transferBuffer, packet) in packets)
                    {
                        Console.WriteLine($"[P->S | Out][{packet.Opcode:X4}]{Environment.NewLine}{Utility.HexDump(transferBuffer.Buffer)}{Environment.NewLine}");
                        SendAsync(transferBuffer.Buffer, transferBuffer.Offset, transferBuffer.Size);
                    }

                    OutgoingPacketsMre.Reset();
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