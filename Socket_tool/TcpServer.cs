using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using System.Reflection;
using log4net.Config;
using System.Collections.Concurrent;

namespace WpfApplication1
{
    class MessageData
    {
        public Socket socket;
        public byte[] msg;
    }
    class TcpServer
    {
        private Socket listenSocket;
        private Int32 bufferSize;
        private Int32 numConnectedSockets;
        private Int32 numConnections;
        public IOContextPool receiveContextPool;
        public IOContextPool sendContextPool;
        private BlockingCollection<MessageData> sendingQueue;
        // private Thread sendMessageWorker;
        public List<Socket> serverList;
        private AutoResetEvent waitSendEvent;

        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public TcpServer(Int32 numConnections, Int32 bufferSize)
        {

            this.numConnectedSockets = 0;
            this.numConnections = numConnections;
            this.bufferSize = bufferSize;
            this.receiveContextPool = new IOContextPool(numConnections);
            this.sendContextPool = new IOContextPool(numConnections);
            this.serverList = new List<Socket>(this.numConnections);

            sendingQueue = new BlockingCollection<MessageData>();
            // sendMessageWorker = new Thread(new ThreadStart(SendQueueMessage));

            for (Int32 i = 0; i < this.numConnections; i++)
            {
                SocketAsyncEventArgs receiveContext = new SocketAsyncEventArgs();
                receiveContext.Completed += new EventHandler<SocketAsyncEventArgs>(OnIOCompleted);
                receiveContext.SetBuffer(new Byte[this.bufferSize], 0, this.bufferSize);
                this.receiveContextPool.Add(receiveContext);
            }
            for (Int32 i = 0; i < this.numConnections; i++)
            {
                SocketAsyncEventArgs sendContext = new SocketAsyncEventArgs();
                sendContext.Completed += new EventHandler<SocketAsyncEventArgs>(OnIOCompleted);
                sendContext.SetBuffer(new Byte[this.bufferSize], 0, this.bufferSize);
                this.sendContextPool.Add(sendContext);
            }

            waitSendEvent = new AutoResetEvent(false);
        }

        public void Start(Int32 port)
        {
            log.Info("start server.");

            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), port);

            this.listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.listenSocket.ReceiveBufferSize = this.bufferSize;
            this.listenSocket.SendBufferSize = this.bufferSize;

            this.listenSocket.Bind(localEndPoint);
            this.listenSocket.Listen(this.numConnections);

            // sendMessageWorker.Start();
            SendQueueMessage();

            this.StartAccept(null);
        }

        private void StartAccept(SocketAsyncEventArgs e)
        {
            if (e == null)
            {
                e = new SocketAsyncEventArgs();
                e.Completed += new EventHandler<SocketAsyncEventArgs>(OnAcceptCompleted);
            }
            else
            {
                e.AcceptSocket = null;
            }

            if (!this.listenSocket.AcceptAsync(e))
            {
                this.ProcessAccept(e);
            }
        }

        private void OnAcceptCompleted(object sender, SocketAsyncEventArgs e)
        {
            this.ProcessAccept(e);
        }

        private void ProcessAccept(SocketAsyncEventArgs e)
        {
            Socket s = e.AcceptSocket;

            if (s.Connected)
            {
                try
                {
                    SocketAsyncEventArgs ioContext = this.receiveContextPool.Pop();
                    if (ioContext != null)
                    {
                        ioContext.UserToken = s;
                        // 原子操作++
                        Interlocked.Increment(ref this.numConnectedSockets);
                        serverList.Add(s);
                        if (!s.ReceiveAsync(ioContext))
                        {
                            this.ProcessReceive(ioContext);
                        }
                    }
                    else
                    {
                        s.Send(Encoding.Default.GetBytes("连接已经达到最大数!"));
                        s.Close();
                    }
                }
                catch (SocketException ex)
                {

                }
                catch (Exception ex)
                {

                }

                this.StartAccept(e);
            }

        }

        public void Stop()
        {
            this.listenSocket.Close();
        }

        private void OnIOCompleted(object sender, SocketAsyncEventArgs e)
        {
            log.Info("OnIOCompleted!");
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    this.ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
                    this.ProcessSend(e);
                    break;
                default:
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send.");
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            log.Info("process receive!");
            if (e.BytesTransferred > 0)
            {
                if (e.SocketError == SocketError.Success)
                {
                    Socket s = (Socket)e.UserToken;

                    byte[] buf = e.Buffer.Take(e.BytesTransferred).ToArray();
                    log.Info(Encoding.Default.GetString(buf));

                    e.SetBuffer(e.Offset,e.BytesTransferred * 2);
                    if (!s.ReceiveAsync(e))
                    {
                        this.ProcessReceive(e);
                    }
                }
                else
                {
                    this.ProcessError(e);
                }
            }
            else
            {
                this.CloseClientSocket(e);
            }
        }

        private void ProcessSend(SocketAsyncEventArgs e)
        {
            log.Info("process send!");
            sendContextPool.Add(e);
            waitSendEvent.Set();
        }

        private void ProcessError(SocketAsyncEventArgs e)
        {
            Socket s = (Socket)e.UserToken;
            IPEndPoint localEp = (IPEndPoint)s.LocalEndPoint;

            this.CloseClientSocket(s, e);

        }

        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            Socket s = (Socket)e.UserToken;
            this.CloseClientSocket(s, e);
        }

        private void CloseClientSocket(Socket s, SocketAsyncEventArgs e)
        {
            // 原子操作--
            Interlocked.Decrement(ref this.numConnectedSockets);

            this.receiveContextPool.Push(e);
            try
            {
                s.Shutdown(SocketShutdown.Send);
            }
            finally
            {
                s.Close();
            }
        }

        private async void SendQueueMessage()
        {
            log.Info("send queue msg!");
            await Task.Run(() =>
            {

                while (true)
                {
                    var message = sendingQueue.Take();
                    if (message != null)
                    {
                        SendMessage(message);
                    }
                }
            });
        }

        private void SendMessage(MessageData message)
        {
            log.Info("SendMsg!");
            var e = sendContextPool.Pop();
            if(e != null)
            {
                log.Info("send: " + Encoding.Default.GetString(message.msg));
                e.SetBuffer(message.msg, 0, message.msg.Length);
                e.UserToken = message.socket;
                message.socket.SendAsync(e);
            }
            else
            {
                log.Info("sendpool else");
                waitSendEvent.WaitOne();
                SendMessage(message);
            }
        }

        private void ProcessMessage(byte[] msg,SocketAsyncEventArgs e)
        {
            log.Info("ProcessMessage!");
            Socket s = e.UserToken as Socket;
            sendingQueue.Add(new MessageData { msg = msg, socket = s });
            ProcessSend(e);
        }

        public void Send(SocketAsyncEventArgs e,string message)
        {
            log.Info("Send. " + message);
            if (e != null)
            {
                Socket s = e.UserToken as Socket;
                byte[] buf = Encoding.Default.GetBytes(message);
                log.Info(Encoding.Default.GetString(e.Buffer.Take(buf.Length).ToArray()));
                // e.SetBuffer(e.Offset, buf.Length * 2);
                log.Info(s.RemoteEndPoint);
                ProcessMessage(buf, e);
            }
        }
    }
}
