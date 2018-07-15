using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using log4net;

namespace Socket_tool
{
    internal class MessageData
    {
        public byte[] Msg;
        public Socket SocketFd;
    }

    internal class TcpServer
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private readonly int _bufferSize;

        private readonly MainWindow _main;
        private readonly int _numConnections;
        private readonly BlockingCollection<MessageData> _sendingQueue;
        private readonly ObservableCollection<TreeNodeItem> _serverListTree;
        private readonly AutoResetEvent _waitSendEvent;

        private Socket _listenSocket;
        private int _numConnectedSockets;
        private int _serverIndex;
        public IoContextPool ReceiveContextPool;
        public IoContextPool SendContextPool;

        public delegate void MainDispatcherInvoke();

        public TcpServer(int numConnections, int bufferSize)
        {
            this._numConnectedSockets = 0;
            this._numConnections = numConnections;
            this._bufferSize = bufferSize;
            this.ReceiveContextPool = new IoContextPool(numConnections);
            this.SendContextPool = new IoContextPool(numConnections);
            this._serverListTree = new ObservableCollection<TreeNodeItem>();
            this._sendingQueue = new BlockingCollection<MessageData>();
            this._serverIndex = -1;
            this._main = MainWindow.Main;

            for (var i = 0; i < this._numConnections; i++)
            {
                var receiveContext = new SocketAsyncEventArgs();
                receiveContext.Completed += this.OnIoCompleted;
                receiveContext.SetBuffer(new byte[this._bufferSize], 0, this._bufferSize);
                this.ReceiveContextPool.Add(receiveContext);

                var sendContext = new SocketAsyncEventArgs();
                sendContext.Completed += this.OnIoCompleted;
                sendContext.SetBuffer(new byte[this._bufferSize], 0, this._bufferSize);
                this.SendContextPool.Add(sendContext);
            }

            this._waitSendEvent = new AutoResetEvent(false);
        }

        public void Start(int port)
        {
            // Log.Info("start server.");

            var localEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), port);
            try
            {
                this._listenSocket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            }
            catch (SocketException ex)
            {
                Log.Info(ex.Message);
                return;
            }

            this._listenSocket.ReceiveBufferSize = this._bufferSize;
            this._listenSocket.SendBufferSize = this._bufferSize;

            this._listenSocket.Bind(localEndPoint);
            this._listenSocket.Listen(this._numConnections);
            this.AddServerToTree(this._listenSocket);
            this.SendQueueMessage();

            this.StartAccept(null);
        }

        private void DispatcherInvokeFunc(MainDispatcherInvoke func)
        {
            this._main.Dispatcher.BeginInvoke(func);
        }

        private void AddServerToTree(Socket s)
        {
            Interlocked.Increment(ref this._serverIndex);
            this.DispatcherInvokeFunc(delegate
            {
                //var newChild = new TreeNodeItem
                //{
                //    DisplayName = s.LocalEndPoint.ToString(),
                //    Name = s.LocalEndPoint.ToString(),
                //    Server = s
                //};
                lock (this._serverListTree)
                {
                    this._serverListTree.Add(new TreeNodeItem
                    {
                        DisplayName = s.LocalEndPoint.ToString(),
                        Name = s.LocalEndPoint.ToString(),
                        Server = s,
                        IsServer = true
                    });
                    this._main.ServerTree.ItemsSource = this._serverListTree;
                }
            });

        }

        private void AddClientToTree(Socket client)
        {
            // Log.Info("Add client to tree!");
            this.DispatcherInvokeFunc(delegate
            {
                lock (this._serverListTree)
                {
                    this._serverListTree[this._serverIndex].Children.Add(new TreeNodeItem
                    {
                        DisplayName = client.RemoteEndPoint.ToString(),
                        Name = client.RemoteEndPoint.ToString(),
                        Client = client,
                        IsServer = false
                    });
                }
            });
        }

        private void RemoveClientFromTree(Socket s)
        {
            this.DispatcherInvokeFunc(delegate
            {
                lock (this._serverListTree)
                {
                    for (var i = 0; i < this._serverListTree[this._serverIndex].Children.Count; i++)
                    {
                        var node = this._serverListTree[this._serverIndex].Children[i];
                        if (node.Client == s)
                        {
                            this._serverListTree[this._serverIndex].Children.Remove(node);
                        }
                    }
                }
            });
        }

        private void AppendRecvToTextBlock(byte[] msg)
        {
            var text = Encoding.Default.GetString(msg);
            this.DispatcherInvokeFunc(delegate
            {
                this._main.Output.Text += text + "\n";
            });
        }

        private void StartAccept(SocketAsyncEventArgs e)
        {
            if (e == null)
            {
                e = new SocketAsyncEventArgs();
                e.Completed += this.OnAcceptCompleted;
            }
            else
            {
                e.AcceptSocket = null;
            }

            if (!this._listenSocket.AcceptAsync(e))
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
            var s = e.AcceptSocket;

            if (!s.Connected)
            {
                // Log.Info("AcceptSocket not connected!");
                return;
            }

            try
            {
                var ioContext = this.ReceiveContextPool.Pop();
                if (ioContext != null)
                {
                    ioContext.UserToken = s;
                    this.AddClientToTree(s);
                    // 原子操作++
                    Interlocked.Increment(ref this._numConnectedSockets);
                    if (!s.ReceiveAsync(ioContext))
                    {
                        this.ProcessReceive(ioContext);
                    }
                }
                else
                {
                    // Log.InfoFormat("error: 连接最大数 {0}",this._numConnectedSockets);
                    s.Send(Encoding.Default.GetBytes("连接已经达到最大数!"));
                    s.Close();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                return;
            }

            this.StartAccept(e);
        }

        public void Stop()
        {
            // Log.Info("Stop server!");
            this._listenSocket.Close();
        }

        private void OnIoCompleted(object sender, SocketAsyncEventArgs e)
        {
            // Log.Info("OnIOCompleted!");
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    this.ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
                    this.ProcessSend(e);
                    break;
                default:
                    throw new ArgumentException(
                        "The last operation completed on the socket was not a receive or send.");
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            while (e.BytesTransferred > 0)
            {
                if (e.SocketError == SocketError.Success)
                {
                    var s = (Socket) e.UserToken;

                    var buf = e.Buffer.Take(e.BytesTransferred).ToArray();
                    // Log.Info("receive: " + Encoding.Default.GetString(buf));
                    this.AppendRecvToTextBlock(buf);
                    e.SetBuffer(e.Offset, e.BytesTransferred * 2);
                    if (s.ReceiveAsync(e))
                    {
                        return;
                    }
                }
                else
                {
                    this.ProcessError(e);
                }
            }

            this.CloseClientSocket(e);
        }

        private void ProcessSend(SocketAsyncEventArgs e)
        {
            this.SendContextPool.Add(e);
            this._waitSendEvent.Set();
        }

        private void ProcessError(SocketAsyncEventArgs e)
        {
            var s = (Socket) e.UserToken;
            this.CloseClientSocket(s, e);
        }

        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            var s = (Socket) e.UserToken;
            this.RemoveClientFromTree(s);
            this.CloseClientSocket(s, e);
        }

        private void CloseClientSocket(Socket s, SocketAsyncEventArgs e)
        {
            // Log.Info("CloseClientSocket!");
            // 原子操作--
            Interlocked.Decrement(ref this._numConnectedSockets);

            this.ReceiveContextPool.Push(e);
            s.Close();
        }

        private async void SendQueueMessage()
        {
            await Task.Run(() =>
            {
                while (true)
                {
                    var message = this._sendingQueue.Take();
                    if (message != null)
                    {
                        this.SendMessage(message);
                    }
                }
            });
        }

        private void SendMessage(MessageData message)
        {
            while (true)
            {
                var e = this.SendContextPool.Pop();
                if (e != null)
                {
                    // Log.Info("send: " + Encoding.Default.GetString(message.Msg));
                    e.SetBuffer(message.Msg, 0, message.Msg.Length);
                    e.UserToken = message.SocketFd;
                    message.SocketFd.SendAsync(e);
                    break;
                }

                this._waitSendEvent.WaitOne();
            }
        }

        private void ProcessMessage(byte[] msg, Socket s)
        {
            this._sendingQueue.Add(new MessageData
            {
                Msg = msg,
                SocketFd = s
            });
        }

        public void Send(Socket s, string message)
        {
            if (s == null)
            {
                return;
            }

            var buf = Encoding.Default.GetBytes(message);

            // Log.Info(s.RemoteEndPoint);
            this.ProcessMessage(buf, s);
        }
    }
}