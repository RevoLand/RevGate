using NetCoreServer;
using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;

namespace RevGate
{
    internal class GateServer : TcpServer
    {
        public readonly ObservableCollection<GateSession> Clients;

        public GateServer(IPAddress address, int port) : base(address, port)
        {
            Clients = new ObservableCollection<GateSession>();
        }

        protected override TcpSession CreateSession()
        {
            return new GateSession(this);
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"Chat TCP server caught an error with code {error}");
        }

        protected override void OnConnected(TcpSession session)
        {
            Clients.Add((GateSession)session);
            Console.Title = $"Connected Gateway Clients: {ConnectedSessions}";
        }

        protected override void OnDisconnected(TcpSession session)
        {
            Clients.Remove((GateSession)session);
            Console.Title = $"Connected Gateway Clients: {ConnectedSessions}";
        }
    }
}