using NetCoreServer;
using System;
using System.Net;

namespace RevGate.ServerHandlers
{
    internal class Gateway : ServerBase
    {
        public Gateway(IPAddress address, int port) : base(address, port)
        {
            OnStartedEvent += () =>
            {
                Console.WriteLine("GatewayServer Listener started...");
            };
        }

        protected override TcpSession CreateSession()
        {
            return new SessionHandlers.Gateway.Client(this);
        }
    }
}