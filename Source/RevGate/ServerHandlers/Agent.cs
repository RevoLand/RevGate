using NetCoreServer;
using System;
using System.Net;

namespace RevGate.ServerHandlers
{
    internal class Agent : ServerBase
    {
        public Agent(IPAddress address, int port) : base(address, port)
        {
            OnStartedEvent += () =>
            {
                Console.WriteLine("AgentServer Listener started...");
            };
        }

        protected override TcpSession CreateSession()
        {
            return new SessionHandlers.Agent.Client(this);
        }
    }
}