using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CommunicationChannel
{
    /// <summary>
    /// This class is used for establishing link connection and communicating with the server.
    /// </summary>
    internal class Tcp
    {
        internal Tcp(Channel channel)
        {
            Channel = channel;
            TimerCheckConnection = new Timer(OnTimerCheckConnection, null, Timeout.Infinite, Timeout.Infinite);
            _timerAutoDisconnect = new Timer(OnTimerAutoDisconnect, null, Timeout.Infinite, Timeout.Infinite);
            TimerKeepAlive = new Timer(OnTimerKeepAlive, null, Timeout.Infinite, Timeout.Infinite);
        }
        internal readonly Channel Channel;

        // =================== This timer checks if the connection has been lost and reestablishes it ====================================
        internal Timer TimerCheckConnection;
        internal int TimerIntervalCheckConnection = 10 * 1000;
        internal object LockIsConnected = new object();
        private void OnTimerCheckConnection(object o)
        {
            lock (LockIsConnected)
            {
                if (Channel.InternetAccess)
                    if (!Connect())
                        TimerCheckConnection.Change(TimerIntervalCheckConnection, Timeout.Infinite); // restart again
            }
        }
        // ===============================================================================================================================

        // =================== This timer automatically closes the connection after a certain period of network inactivity ===============
        //public int ConnectionTimeout = Timeout.Infinite;
        private readonly Timer _timerAutoDisconnect;
        private void OnTimerAutoDisconnect(object o) => Disconnect(false);
        private void SuspendAutoDisconnectTimer()
        {

            _timerAutoDisconnect.Change(Timeout.Infinite, Timeout.Infinite);
        }
        private DateTime _timerStartedTime = DateTime.MinValue;
        private void ResumeAutoDisconnectTimer(int? connectionTimeout = null)
        {
            if (connectionTimeout == null)
                _timerStartedTime = DateTime.UtcNow;
            if (Channel.ConnectionTimeout != Timeout.Infinite)
                _timerAutoDisconnect.Change(connectionTimeout != null ? (int)connectionTimeout : Channel.ConnectionTimeout, Timeout.Infinite);
        }
        // ===============================================================================================================================

        // =============== keep alive timer ==============================================================================================
        internal Timer TimerKeepAlive;
        internal int KeepAliveInterval = 60 * 1000; // Milliseconds
        private void OnTimerKeepAlive(object o)
        {
            try
            {
                var stream = Client?.GetStream();
                stream?.Write(new byte[] { 0, 0, 0, 0 }, 0, 4);
                stream?.Flush();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                Channel.KeepAliveFailures++;
#if DEBUG
                if (ex.HResult != -2146233079) // The server is pinging and may have dropped the connection because it is not responding during debugging. If the error is not the same, then it breaks on the next line
                    Debugger.Break();
#endif
            }
            if (IsConnected())
                TimerKeepAlive.Change(KeepAliveInterval, Timeout.Infinite); // restart again
            else
                Disconnect();
        }
        private void KeepAliveStart()
        {
            if (Channel.ConnectionTimeout == Timeout.Infinite) //I'm a server
                TimerKeepAlive.Change(KeepAliveInterval, Timeout.Infinite);
        }
        private void KeepAliveStop()
        {
            if (Channel.ConnectionTimeout == Timeout.Infinite) //I'm a server
                TimerKeepAlive.Change(Timeout.Infinite, Timeout.Infinite);
        }
        // ===============================================================================================================================


        private const int _maxDataLength = 64000000; //64 MB - max data length enable to received the server
        internal TcpClient Client;
        /// <summary>
        /// Send the data, which will be parked in the spooler, cannot be forwarded immediately: If there is a queue or if there is no internet line the data will be parked.
        /// </summary>
        /// <param name="data">Data to be sent</param>
        public void SendData(byte[] data)
        {
            Channel.Spooler.AddToQuee(data);
        }
        private const int TimeOutMs = 5000;
        internal SemaphoreSlim WaitConfirmation;
        //internal List<byte[]> DataAwaitingConfirmation = new List<byte[]>();
        internal int ToWrite;
        /// <summary>
        /// Send data to server (router) without going through the spooler
        /// </summary>
        /// <param name="data">Data to be sent</param>
        /// <param name="executeOnConfirmReceipt">Action to be taken when the router has successfully received the data sent</param>
        /// <param name="directlyWithoutSpooler">If true, it indicates to the router (server) that it should not park the data if the receiver is not connected</param>
        internal void ExecuteSendData(byte[] data, Action executeOnConfirmReceipt = null, bool directlyWithoutSpooler = false)
        {
            var dataLength = (uint)data.Length;
            ToWrite += data.Length;
            lock (this)
            {
#if DEBUG
                if (!Logged && dataLength > 0 && data[0] != (byte)Protocol.Command.ConnectionEstablished)
                {
                    Debug.WriteLine(Channel.ServerUri); // Current entry point
                    if (directlyWithoutSpooler)
                        Debugger.Break(); // Don't send message directly without spooler before authentication on the server!
                    else
                        Debugger.Break(); // Verify if the server running and if you have internet connection!  (Perhaps there is no server at the current entry point)
                }
#endif
                if (dataLength > _maxDataLength) { Channel.Spooler.OnSendCompleted(data, new Exception("excess data length"), false); return; }
                SuspendAutoDisconnectTimer();
                var command = (Protocol.Command)data[0];
                // var waitConfirmation = !directlyWithoutSpooler && command != Protocol.Command.DataReceivedConfirmation && command != Protocol.Command.ConnectionEstablished;
                var waitConfirmation = !directlyWithoutSpooler && command != Protocol.Command.DataReceivedConfirmation;

                if (Client == null || !Client.Connected)
                {
                    Channel.Spooler.OnSendCompleted(data, new Exception("not connected"), true);
                }
                else
                {
                    try
                    {
                        lock (Client)
                        {
                            var stream = Client.GetStream();
                            var mask = 0b10000000_00000000_00000000_00000000;
                            var lastBit = directlyWithoutSpooler ? mask : 0;
                            stream.Write((dataLength | lastBit).GetBytes(), 0, 4);
                            var writed = 0;
                            KeepAliveStop();
                            WaitConfirmation = waitConfirmation ? new SemaphoreSlim(0, 1) : null;
                            var timeoutMs = TimeOutMs + data.Length / 50;
                            stream.WriteTimeout = timeoutMs;
                            var watch = Stopwatch.StartNew();
                            stream.Write(data, writed, data.Length - writed);
                            stream.Flush();
                            watch.Stop();
                            var elapsedMs = watch.ElapsedMilliseconds;
                            if (elapsedMs > 300)
                            {
                                Debugger.Break();
                            }
                            if (WaitConfirmation == null || WaitConfirmation.Wait(TimeOutMs))
                            {
                                // confirmation received                              
                                if (waitConfirmation)
                                {
                                    executeOnConfirmReceipt?.Invoke();
                                    Channel.Spooler.OnSendCompleted(data, null, false);
                                }
                                KeepAliveStart();
                                ResumeAutoDisconnectTimer();
                                if (Logged)
                                    Channel.Spooler.SendNext(); //Upon receipt confirmation, sends the next message
                                else
                                    Debugger.Break();
                            }
                            else
                            {
                                // wait timed out
                                Channel.Spooler.OnSendCompleted(data, null, true);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Channel.Spooler.OnSendCompleted(data, ex, true);
                    }
                }
                ToWrite -= data.Length;
            }
        }

        /// <summary>
        /// Establish the connection and start the spooler
        /// </summary>
        internal bool Connect()
        {
            if (!Channel.ContextIsReady())
                return false;
            lock (this)
            {
                if (!IsConnected() && Channel.InternetAccess)
                {;
                    StartLinger(443, out var exception);
                    if (exception != null)
                    {
                        Channel.OnTcpError(ErrorType.ConnectionFailure, exception.Message);
                        Disconnect();
                    }
                    else
                    {
                        OnCennected();
                        KeepAliveStart();
                        return true;
                    }
                }
            }
            return false;
        }
        private SemaphoreSlim OnConnectedSemaphore;
        internal bool Logged;
        private void OnCennected()
        {
            TimerCheckConnection.Change(Timeout.Infinite, Timeout.Infinite); // Stop check if connection is lost
            ResumeAutoDisconnectTimer();
            BeginRead(Client);
            void startSpooler()
            {
                Debug.WriteLine("Logged");
                Logged = true;
                OnConnectedSemaphore.Release();
                OnConnectedSemaphore = null;
            };
            var data = Channel.CommandsForServer.CreateCommand(Protocol.Command.ConnectionEstablished, null, null, Channel.MyId); // log in
            OnConnectedSemaphore = new SemaphoreSlim(0, 1);
            ExecuteSendData(data, startSpooler);
            OnConnectedSemaphore?.Wait(TimeOutMs);
        }

        private void StartLinger(int port, out Exception exception)
        {
            exception = null;
            try
            {
                var addresses = Dns.GetHostAddresses(Channel.ServerUri.Host).Reverse();
                foreach (var ip in addresses)
                {
                    try
                    {
                        //Client = new TcpClient(ip.ToString(), port)
                        Client = new TcpClient()
                        {
                            LingerState = new LingerOption(true, 0)
                        };

                        //Client.Connect(ip, port);
                        if (!Client.ConnectAsync(ip, port).Wait(TimeOutMs)) // ms timeout
                        {
                            throw new Exception("Failed to connect");
                        }

                        //var result = Client.BeginConnect(ip, port, null, null);
                        //var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(10));
                        //if (!success || !Client.Connected)
                        //{
                        //    throw new Exception("Failed to connect");
                        //}
                        exception = null;
                        break;
                    }
                    catch (Exception ex)
                    {
                        exception = ex;
                    }
                }
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        }

        private void BeginRead(TcpClient client)
        {
            NetworkStream stream;
            try
            {
                if (client == null)
                {
                    Disconnect();
                    return;
                }
                stream = client.GetStream();
            }
            catch (Exception ex)
            {
                Channel.OnTcpError(ErrorType.LostConnection, ex.Message);
                Disconnect();
                return;
            }
            var dataLength = new byte[4];
            void onReadLength(IAsyncResult result)
            {
                var bytesRead = 0;
                try { bytesRead = stream.EndRead(result); }
                catch (Exception ex)
                {
                    if (ex.Message.StartsWith("Cannot access a disposed object"))
                        Channel.OnTcpError(ErrorType.ConnectionClosed, "The timer has closed the connection");
                    else
                        Channel.OnTcpError(ErrorType.LostConnection, ex.Message);
                    Debug.WriteLine(client.Connected.ToString());
                    Disconnect();
                    return;
                }
                if (bytesRead != 4)
                    Channel.OnTcpError(ErrorType.WrondDataLength, null);
                else
                {
                    var firstUint = BitConverter.ToUInt32(dataLength, 0);
                    var lengthIncomingData = (int)(0B01111111_11111111_11111111_11111111 & firstUint);
                    var directlyWithoutSpooler = (firstUint & 0b10000000_00000000_00000000_00000000) != 0;
                    ReadBytes(lengthIncomingData, stream, directlyWithoutSpooler);
                }
            }
            try
            {
                stream.BeginRead(dataLength, 0, 4, onReadLength, client);
            }
            catch (Exception ex)
            {
                Channel.OnTcpError(ErrorType.LostConnection, ex.Message);
                Disconnect();
            }
        }

        internal int ToRead;
        private void ReadBytes(int lengthIncomingData, NetworkStream stream, bool directlyWithoutSpooler)
        {
            ToRead = lengthIncomingData;
            var timerStarted = _timerStartedTime;
            SuspendAutoDisconnectTimer();
            byte[] data;
            try
            {
                // The server can send several messages in a single data packet, so it makes no sense to check the length
                data = new byte[lengthIncomingData];
                var readed = 0;
                KeepAliveStop();
                while (readed < ToRead)
                {
                    readed += stream.Read(data, readed, ToRead - readed);
                }
                KeepAliveStart();
            }
            catch (Exception ex)
            {
                Channel.OnTcpError(ErrorType.LostConnection, ex.Message);
                ToRead = 0;
                Disconnect();
                return;
            }

            if (data.Length == 5 && data[0] == (byte)Protocol.Command.DataReceivedConfirmation)
            {
                var dataId = BitConverter.ToUInt32(data, 1);
                Channel.Spooler.OnConfirmReceipt(dataId);
            }
            //count += 1;
            Channel.OnDataReceives(data, out var error, directlyWithoutSpooler);
            if (error != null)
            {
                Debugger.Break(); //something went wrong!
                var textData = System.Text.Encoding.UTF8.GetString(data);
                Debug.WriteLine(textData); //Let's try to see if the transformation of the received data packet into text gives us some clues to understand what happened!
                Channel.OnTcpError(error.Item1, error.Item2);
            }
            if (Channel.ConnectionTimeout != Timeout.Infinite)
            {
                if (data.Length >= 1 && data[0] == (byte)Protocol.Command.Ping) // Pinging from the server does not reset the connection timeout, otherwise, if the pings occur frequently, the connection will never be closed
                {
                    var timePassedMs = (int)(DateTime.UtcNow - timerStarted).TotalMilliseconds;
                    var remainingTimeMs = Channel.ConnectionTimeout - timePassedMs;
                    if (remainingTimeMs < 0)
                        remainingTimeMs = 0; // It will immediately trigger the timer closing the connection
                    ResumeAutoDisconnectTimer(remainingTimeMs);
                }
                else
                    ResumeAutoDisconnectTimer();
            }
            BeginRead(Client); //loop - restart to wait for data

            ToRead = 0;
        }


        internal void InvokeError(ErrorType errorId, string description) => Channel.OnTcpError(errorId, description);
        /// <summary>
        /// Provides the base for enumerations to represent errors.
        /// </summary>
        public enum ErrorType
        {
            Working,
            ConnectionFailure,
            WrondDataLength,
            LostConnection,
            SendDataError,
            CommandNotSupported,
            ConnectionClosed
        }

        /// <summary>
        /// Used to make a connection if the communication link breaks.
        /// </summary>
        /// <param name="tryConnectAgain"></param>
        public void Disconnect(bool tryConnectAgain = true)
        {
            if (!Channel.ContextIsReady())
                return;
            lock (this)
            {
                Debug.WriteLine("Disconnect");
                if (Client != null) // Do not disconnect again
                {
                    Logged = false;
                    KeepAliveStop();
                    if (tryConnectAgain)
                        TimerCheckConnection.Change(TimerIntervalCheckConnection, Timeout.Infinite); // restart check if connection is lost
                    SuspendAutoDisconnectTimer();
                    if (Client != null)
                    {
                        Client.Close();
                        Client.Dispose();
                    }
                    Client = null;
                }
            }
        }
        /// <summary>
        /// Find if the socket is connected to the remote host.
        /// </summary>
        /// <returns></returns>
        public bool IsConnected() =>
            //According to the specifications, the property _client.Connected returns the connection status based on the last data transmission. The server may not be connected even if this property returns true
            // https://docs.microsoft.com/it-it/dotnet/api/system.net.sockets.tcpclient.connected?f1url=https%3A%2F%2Fmsdn.microsoft.com%2Fquery%2Fdev16.query%3FappId%3DDev16IDEF1%26l%3DIT-IT%26k%3Dk(System.Net.Sockets.TcpClient.Connected);k(DevLang-csharp)%26rd%3Dtrue&view=netcore-3.1
            Client != null && Client.Connected && Channel.InternetAccess;
    }
}