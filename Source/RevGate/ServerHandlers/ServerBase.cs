using NetCoreServer;
using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;

namespace RevGate.ServerHandlers
{
    internal abstract class ServerBase : TcpServer
    {
        public readonly ObservableCollection<SessionHandlers.BaseToClient> Clients;

        #region Events

        public event Action<TcpSession> OnConnectedEvent;

        public event Action<TcpSession> OnDisconnectedEvent;

        public event Action OnStartedEvent;

        public event Action<SocketError> OnErrorEvent;

        #endregion Events

        protected ServerBase(IPAddress address, int port) : base(address, port)
        {
            Clients = new ObservableCollection<SessionHandlers.BaseToClient>();
        }

        protected abstract override TcpSession CreateSession();

        protected override void OnStarted()
        {
            OnStartedEvent?.Invoke();
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"[{Endpoint}] Server caught an error with code {error}");

            OnErrorEvent?.Invoke(error);
        }

        protected override void OnConnected(TcpSession session)
        {
            Clients.Add((SessionHandlers.BaseToClient)session);

            OnConnectedEvent?.Invoke(session);
        }

        protected override void OnDisconnected(TcpSession session)
        {
            Clients.Remove((SessionHandlers.BaseToClient)session);

            OnDisconnectedEvent?.Invoke(session);
        }
    }
}