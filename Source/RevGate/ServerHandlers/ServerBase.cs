using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace RevGate.ServerHandlers
{
    public class ServerBase
    {
        public Guid Id { get; }
        public IPEndPoint IpEndPoint { get; private set; }

        public long BytesPending => _bytesPending;
        public long BytesSent => _bytesSent;
        public long BytesReceived => _bytesReceived;

        public int OptionBacklog { get; set; } = 100;

        public bool IsStarted { get; private set; }
        public bool IsAccepting { get; private set; }
        public bool IsSocketDisposed { get; private set; } = true;

        public readonly ConcurrentDictionary<Guid, SessionBase> Sessions = new ConcurrentDictionary<Guid, SessionBase>();

        private Socket ListenerSocket;
        private Thread _sessionHandlerThread;
        private ManualResetEvent _listenerThreadMre;

        internal long _bytesPending, _bytesSent, _bytesReceived;

        public ServerBase(IPEndPoint listenerEndPoint)
        {
            Id = Guid.NewGuid();

            if (System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
                .Count(x => x.Port == listenerEndPoint.Port) > 0)
            {
                throw new Exceptions.PortIsInUseException($"The given port ({listenerEndPoint.Port}) is in use!");
            }

            IpEndPoint = listenerEndPoint;
        }

        public ServerBase(IPAddress listenerAddress, int listenerPort) : this(new IPEndPoint(listenerAddress, listenerPort))
        {
        }

        public ServerBase(string listenerIp, int listenerPort) : this(new IPEndPoint(IPAddress.Parse(listenerIp), listenerPort))
        {
        }

        public virtual bool Start()
        {
            if (IsStarted)
                // TODO: throw a new Exception
                return false;

            _bytesPending = _bytesSent = _bytesReceived = 0;
            _listenerThreadMre = new ManualResetEvent(false);
            ListenerSocket = new Socket(IpEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            ListenerSocket.Bind(IpEndPoint);
            ListenerSocket.Listen(OptionBacklog);
            IpEndPoint = (IPEndPoint)ListenerSocket.LocalEndPoint;
            IsSocketDisposed = false;

            _sessionHandlerThread = new Thread(SessionHandler);
            _sessionHandlerThread.Start();
            OnStarted();

            return IsStarted = IsAccepting = true;
        }

        public virtual bool Stop()
        {
            if (!IsStarted)
                return false;

            IsStarted = IsAccepting = false;
            ListenerSocket.Close();
            ListenerSocket.Dispose();
            IsSocketDisposed = true;

            DisconnectAll();
            OnStopped();

            return true;
        }

        public virtual bool DisconnectAll()
        {
            if (!IsStarted)
                return false;

            foreach (var session in Sessions.Values)
            {
                session.Disconnect();
            }

            return true;
        }

        protected virtual SessionBase CreateSession()
        {
            return new SessionBase(this);
        }

        protected virtual void OnStarted()
        {
        }

        protected virtual void OnStopped()
        {
        }

        protected virtual void SessionHandler()
        {
            try
            {
                while (IsAccepting)
                {
                    _listenerThreadMre.Reset();

                    ListenerSocket.BeginAccept(SessionHandlerEnd, null);

                    _listenerThreadMre.WaitOne();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        protected virtual void SessionHandlerEnd(IAsyncResult iar)
        {
            try
            {
                _listenerThreadMre.Set();

                var session = CreateSession();
                RegisterSession(session);
                session.Connect(ListenerSocket.EndAccept(iar));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        internal void RegisterSession(SessionBase session)
        {
            Sessions.TryAdd(session.Id, session);
        }

        internal void UnregisterSession(Guid sessionId)
        {
            Sessions.TryRemove(sessionId, out _);
        }

        internal void OnConnectedIntenal(SessionBase session)
        {
            OnConnected(session);
        }

        internal void OnDisconnectedIntenal(SessionBase session)
        {
            OnDisconnected(session);
        }

        protected virtual void OnConnected(SessionBase session)
        {
        }

        protected virtual void OnDisconnected(SessionBase session)
        {
        }
    }
}