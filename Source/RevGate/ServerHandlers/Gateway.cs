using RevGate.SessionHandlers.Gateway;

namespace RevGate.ServerHandlers
{
    internal class Gateway : ServerBase
    {
        public Gateway(string listenerIp, int listenerPort) : base(listenerIp, listenerPort)
        {
        }

        protected override SessionBase CreateSession()
        {
            return new Client(this);
        }
    }
}