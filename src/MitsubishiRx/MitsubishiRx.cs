// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;

namespace MitsubishiRx
{
    /// <summary>
    /// MitsubishiRx.
    /// </summary>
    public class MitsubishiRx : IDisposable
    {
        private const int BufferSize = 4096;
        private readonly object _lockObject = new();
        private CpuType _cpuType;
        private int _timeout;
        private Socket _socket;
        private bool _isAutoOpen = true;
        private bool _disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="MitsubishiRx" /> class.
        /// </summary>
        /// <param name="cpuType">Type of the cpu.</param>
        /// <param name="ip">The ip.</param>
        /// <param name="port">The port.</param>
        /// <param name="timeout">The timeout.</param>
        public MitsubishiRx(CpuType cpuType, string ip, int port, int timeout = 1500)
        {
            _cpuType = cpuType;
            if (!IPAddress.TryParse(ip, out var address))
            {
                address = Dns.GetHostEntry(ip).AddressList?.FirstOrDefault();
            }

            IpEndPoint = new IPEndPoint(address, port);
            _timeout = timeout;
        }

        /// <summary>
        /// Gets the ip end point.
        /// </summary>
        /// <value>
        /// The ip end point.
        /// </value>
        public IPEndPoint IpEndPoint { get; }

        /// <summary>
        /// Gets a value indicating whether this <see cref="MitsubishiRx"/> is connected.
        /// </summary>
        /// <value>
        ///   <c>true</c> if connected; otherwise, <c>false</c>.
        /// </value>
        public bool Connected => _socket?.Connected ?? false;

        /// <summary>
        /// Opens this instance.
        /// </summary>
        /// <returns>Responce.</returns>
        public Responce Open()
        {
            _isAutoOpen = false;
            return Connect();
        }

        /// <summary>
        /// Closes this instance.
        /// </summary>
        /// <returns>Responce.</returns>
        public Responce Close()
        {
            _isAutoOpen = true;
            return DisposeSocket();
        }

        /// <summary>
        /// Sends the package single.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>Responce.</returns>
        public Responce<byte[]> SendPackageSingle(byte[] command)
        {
            lock (_lockObject)
            {
                var result = new Responce<byte[]>();
                try
                {
                    _socket.Send(command);
                    var socketReadResul = MitsubishiRx.SocketRead(_socket, 9);
                    if (!socketReadResul.IsSucceed)
                    {
                        return socketReadResul;
                    }

                    var headPackage = socketReadResul.Value;

                    var contentLength = BitConverter.ToUInt16(headPackage, 7);
                    socketReadResul = MitsubishiRx.SocketRead(_socket, contentLength);
                    if (!socketReadResul.IsSucceed)
                    {
                        return socketReadResul;
                    }

                    var dataPackage = socketReadResul.Value;

                    result.Value = headPackage.Concat(dataPackage).ToArray();
                    return result.EndTime();
                }
                catch (Exception ex)
                {
                    result.IsSucceed = false;
                    result.Err = ex.Message;
                    result.AddErr2List();
                    return result.EndTime();
                }
            }
        }

        /// <summary>
        /// Sends the package.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="receiveCount">The receive count.</param>
        /// <returns>Responce.</returns>
        public Responce<byte[]> SendPackage(byte[] command, int receiveCount)
        {
            Responce<byte[]> GetResponce()
            {
                lock (_lockObject)
                {
                    var result = new Responce<byte[]>();
                    _socket.Send(command);
                    var socketReadResul = MitsubishiRx.SocketRead(_socket, receiveCount);
                    if (!socketReadResul.IsSucceed)
                    {
                        return socketReadResul;
                    }

                    var dataPackage = socketReadResul.Value;

                    result.Value = dataPackage.ToArray();
                    return result.EndTime();
                }
            }

            try
            {
                var result = GetResponce();
                if (!result.IsSucceed)
                {
                    //// WarningLog?.Invoke(result.Err, result.Exception);
                    var conentResult = Connect();
                    if (!conentResult.IsSucceed)
                    {
                        return new Responce<byte[]>(conentResult);
                    }

                    return GetResponce();
                }

                return result;
            }
            catch (Exception ex)
            {
                //// WarningLog?.Invoke(ex.Message, ex);
                var conentResult = Connect();
                if (!conentResult.IsSucceed)
                {
                    return new Responce<byte[]>(conentResult);
                }

                return GetResponce();
            }
        }

        /// <summary>
        /// Sends the package reliable.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>Responce.</returns>
        public Responce<byte[]> SendPackageReliable(byte[] command)
        {
            try
            {
                var result = SendPackageSingle(command);
                if (!result.IsSucceed)
                {
                    //// WarningLog?.Invoke(result.Err, result.Exception);
                    var conentResult = Connect();
                    if (!conentResult.IsSucceed)
                    {
                        return new Responce<byte[]>(conentResult);
                    }

                    return SendPackageSingle(command);
                }

                return result;
            }
            catch (Exception ex)
            {
                try
                {
                    //// WarningLog?.Invoke(ex.Message, ex);
                    var conentResult = Connect();
                    if (!conentResult.IsSucceed)
                    {
                        return new Responce<byte[]>(conentResult);
                    }

                    return SendPackageSingle(command);
                }
                catch (Exception ex2)
                {
                    var result = new Responce<byte[]>();
                    result.IsSucceed = false;
                    result.Err = ex2.Message;
                    result.AddErr2List();
                    return result.EndTime();
                }
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _socket.Dispose();
                }

                _disposedValue = true;
            }
        }

        private static Responce<byte[]> SocketRead(Socket socket, int receiveCount)
        {
            var result = new Responce<byte[]>();
            if (receiveCount < 0)
            {
                result.IsSucceed = false;
                result.Err = $"Read length : {receiveCount}";
                result.AddErr2List();
                return result;
            }

            var receiveBytes = new byte[receiveCount];
            var receiveFinish = 0;
            while (receiveFinish < receiveCount)
            {
                var receiveLength = (receiveCount - receiveFinish) >= BufferSize ? BufferSize : (receiveCount - receiveFinish);
                try
                {
                    var readLeng = socket.Receive(receiveBytes, receiveFinish, receiveLength, SocketFlags.None);
                    if (readLeng == 0)
                    {
                        socket?.SafeClose();
                        result.IsSucceed = false;
                        result.Err = "the connection dropped";
                        result.AddErr2List();
                        return result;
                    }

                    receiveFinish += readLeng;
                }
                catch (SocketException ex)
                {
                    socket?.SafeClose();
                    if (ex.SocketErrorCode == SocketError.TimedOut)
                    {
                        result.Err = $"Connection timed out：{ex.Message}";
                    }
                    else
                    {
                        result.Err = $"The connection dropped，{ex.Message}";
                    }

                    result.IsSucceed = false;
                    result.AddErr2List();
                    result.Exception = ex;
                    return result;
                }
            }

            result.Value = receiveBytes;
            return result.EndTime();
        }

        private Responce Connect()
        {
            var result = new Responce();
            _socket?.SafeClose();
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                _socket.ReceiveTimeout = _timeout;
                _socket.SendTimeout = _timeout;
                var connectResult = _socket.BeginConnect(IpEndPoint, null, null);
                if (!connectResult.AsyncWaitHandle.WaitOne(_timeout))
                {
                    throw new TimeoutException("Connection timed out");
                }

                _socket.EndConnect(connectResult);
            }
            catch (Exception ex)
            {
                _socket?.SafeClose();
                result.IsSucceed = false;
                result.Err = ex.Message;
                result.ErrCode = 408;
                result.Exception = ex;
            }

            return result.EndTime();
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <returns>Responce.</returns>
        private Responce DisposeSocket()
        {
            var result = new Responce();
            try
            {
                _socket?.SafeClose();
                return result;
            }
            catch (Exception ex)
            {
                result.IsSucceed = false;
                result.Err = ex.Message;
                result.Exception = ex;
                return result;
            }
        }
    }
}
