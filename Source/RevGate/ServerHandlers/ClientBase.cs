using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace RevGate.ServerHandlers
{
    public class ClientBase
    {
        public Guid Id { get; }
        public Socket Socket { get; private set; }
        public IPEndPoint TargetEndPoint { get; }

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
        private readonly object _sessionLock = new object();

        public ClientBase(IPEndPoint targetEndPoint)
        {
            Id = Guid.NewGuid();
            TargetEndPoint = targetEndPoint;
        }

        public ClientBase(IPAddress targetIp, int targetPort) : this(new IPEndPoint(targetIp, targetPort))
        {
        }

        public ClientBase(string targetIp, int targetPort) : this(IPAddress.Parse(targetIp), targetPort)
        {
        }

        public virtual void Connect()
        {
            if (IsConnected)
                return;

            BytesSending = BytesSent = BytesReceived = 0;

            Socket = new Socket(TargetEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _receiveBuffer = new byte[OptionReceiveBufferSize];
            IsSocketDisposed = false;
            try
            {
                Socket.BeginConnect(TargetEndPoint, EndConnect, null);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void EndConnect(IAsyncResult iar)
        {
            try
            {
                IsConnected = true;
                Socket.EndConnect(iar);
                OnConnected();
                TryReceive();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode != SocketError.Success)
                {
                    Console.WriteLine($"SocketException hata kodu: {ex.SocketErrorCode}");
                    Disconnect();
                }
            }
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
                OnReceived(_receiveBuffer, 0, bytesReceived);
            }
            else
            {
                Console.WriteLine("boş");
            }

            IsReceiving = false;
            if (errorCode == SocketError.Success)
            {
                if (bytesReceived > 0)
                {
                    _receiveFlag = true;
                    return;
                }
            }
            else
            {
                SendError(errorCode);
            }

            Disconnect();
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

            return true;
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
    }
}