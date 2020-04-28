using System;
using System.Net.Sockets;
using System.Threading;

namespace RevGate.ServerHandlers
{
    public class SessionBase
    {
        public ServerBase Server { get; private set; }
        public Guid Id { get; private set; }
        public Socket Socket { get; protected set; }

        public long BytesSending { get; private set; }
        public long BytesSent { get; private set; }
        public long BytesReceived { get; private set; }

        public bool IsConnected { get; private set; }
        public bool IsReceiving { get; private set; }
        public bool IsSending { get; private set; }
        public bool IsSocketDisposed { get; private set; } = true;

        public int OptionReceiveBufferSize
        {
            get => Socket.ReceiveBufferSize;
            set
            {
                _receiveBuffer = new byte[value];
                Socket.ReceiveBufferSize = value;
            }
        }

        private byte[] _receiveBuffer;
        private bool _receiveFlag;
        private readonly object _sessionLock;

        public SessionBase(ServerBase server)
        {
            Server = server;
            Id = Guid.NewGuid();
            _sessionLock = new object();
        }

        internal void Connect(Socket socket)
        {
            Socket = socket;
            IsSocketDisposed = false;
            BytesSending = BytesSent = BytesReceived = 0;
            _receiveBuffer = new byte[OptionReceiveBufferSize];

            IsConnected = true;

            OnConnected();
            Server.OnConnectedIntenal(this);
            TryReceive();
        }

        protected virtual void OnConnected()
        {
        }

        protected virtual void OnDisconnected()
        {
        }

        protected virtual void OnError(SocketError error)
        {
        }

        protected virtual void OnReceived(byte[] buffer, long offset, long size)
        {
        }

        private void TryReceive()
        {
            if (IsReceiving || !IsConnected || IsSocketDisposed)
                // TODO: throw an exception?
                return;

            _receiveFlag = true;

            while (_receiveFlag)
            {
                _receiveFlag = false;
                try
                {
                    IsReceiving = true;
                    Socket.BeginReceive(_receiveBuffer, 0, _receiveBuffer.Length, SocketFlags.None, DoReceive, null);
                }
                catch (ObjectDisposedException e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    throw;
                }
            }
        }

        private void DoReceive(IAsyncResult iar)
        {
            if (!IsConnected)
            {
                _receiveFlag = false;
                return;
            }
            var bytesReceived = Socket.EndReceive(iar, out var errorCode);

            if (bytesReceived > 0)
            {
                BytesReceived += bytesReceived;
                Interlocked.Add(ref Server._bytesReceived, bytesReceived);
                OnReceived(_receiveBuffer, 0, bytesReceived);
            }

            IsReceiving = false;
            if (errorCode == SocketError.Success)
            {
                if (bytesReceived > 0)
                {
                    _receiveFlag = true;
                    return;
                }
                else
                {
                    Console.WriteLine("boş");
                }
            }
            else
            {
                SendError(errorCode);
            }

            Disconnect();
        }

        public virtual bool Disconnect()
        {
            if (!IsConnected)
                return false;

            try
            {
                try
                {
                    Socket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException e)
                {
                }
                Socket.Close();
                Socket.Dispose();
                IsSocketDisposed = true;
            }
            catch (ObjectDisposedException ex)
            {
            }

            IsConnected = IsReceiving = IsSending = false;

            ClearBuffers();
            OnDisconnected();
            Server.OnDisconnectedIntenal(this);
            Server.UnregisterSession(Id);

            return true;
        }

        public virtual void Send(byte[] buffer)
        {
            Send(buffer, 0, buffer.Length);
        }

        public virtual void Send(byte[] buffer, int offset, int size)
        {
            if (!IsConnected || size == 0)
                return;

            try
            {
                Socket.BeginSend(buffer, offset, size, SocketFlags.None, DoSend, null);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        private void DoSend(IAsyncResult iar)
        {
            try
            {
                var bytesSent = Socket.EndSend(iar, out var errorCode);
                if (bytesSent > 0)
                {
                    BytesSent += bytesSent;
                    Interlocked.Add(ref Server._bytesSent, bytesSent);
                    OnSent(bytesSent, BytesSending);
                }
                else
                {
                    Console.WriteLine("boş");
                }

                if (errorCode != SocketError.Success)
                {
                    SendError(errorCode);
                    Disconnect();
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        protected virtual void OnSent(int bytesSent, long bytesPending)
        {
        }

        private void ClearBuffers()
        {
            bool locked = false;
            try
            {
                Monitor.Enter(_sessionLock, ref locked);

                _receiveBuffer = null;

                BytesSending = 0;
            }
            finally
            {
                if (locked)
                    Monitor.Exit(_sessionLock);
            }
        }

        private void SendError(SocketError error)
        {
            if (error == SocketError.ConnectionAborted || error == SocketError.ConnectionRefused || (error == SocketError.ConnectionReset || error == SocketError.OperationAborted) || error == SocketError.Shutdown)
                return;

            OnError(error);
        }
    }
}