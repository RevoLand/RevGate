using NetCoreServer;
using RevGate.SilkroadSecurityApi;
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;

namespace RevGate.SessionHandlers
{
    internal abstract class BaseToClient : TcpSession
    {
        public BaseToServer ProxyToServer;
        public Security Security;
        public ManualResetEvent IncomingPacketsMre;
        public ManualResetEvent OutgoingPacketsMre;
        public CancellationTokenSource Cancellation;

        #region Events

        protected event Action OnConnectedEvent;

        protected event Action OnDisconnectedEvent;

        protected event Action<byte[], long, long> OnReceivedEvent;

        protected event Action<SocketError> OnErrorEvent;

        #endregion Events

        protected BaseToClient(TcpServer server) : base(server)
        {
        }

        protected override void OnConnected()
        {
            try
            {
                Console.WriteLine($"Client connected with ID: {Id}");

                Cancellation = new CancellationTokenSource();
                Security = new Security();
                Security.GenerateSecurity(true, true, true);

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
            ProxyToServer.DisconnectAsync();
            Console.WriteLine($"Client with Id: {Id} disconnected!");

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
                        Console.WriteLine($"[C->P | Out][{packet.Opcode:X4}]{Environment.NewLine}{Utility.HexDump(transferBuffer.Buffer)}{Environment.NewLine}");
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