using System.Net.Sockets;
using System.Reflection;
using System.Windows;
using log4net;

namespace Socket_tool
{
    /// <summary>
    ///     MainWindow.xaml 的交互逻辑
    /// </summary>
    /// <inheritdoc>
    /// </inheritdoc>
    public partial class MainWindow : Window
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        internal static MainWindow Main;
        private readonly TcpServer _server;
        private Socket _curSocket;

        public MainWindow()
        {
            this.InitializeComponent();
            Main = this;
            this._server = new TcpServer(1024, 2048);
            this._curSocket = null;
        }

        private void StartServer(object sender, RoutedEventArgs e)
        {
            this._server.Start(8000);
        }

        private void StopServer(object sender, RoutedEventArgs e)
        {
            this._server.Stop();
        }

        private void SendMessage(object sender, RoutedEventArgs e)
        {
            Log.Info("send message.");
            if (this._curSocket == null)
            {
                return;
            }
            this._server.Send(this._curSocket, this.Input.Text);
            this.Input.Text = "";
        }

        private void ServerTree_SelectedItemChanged(object sender, RoutedEventArgs e)
        {
            var node = this.ServerTree.SelectedItem as TreeNodeItem;
            if (node == null)
            {
                Log.Info("node is null!");
                return;
            }

            Log.Info(node.DisplayName);
            this._curSocket = node.Client;
        }
    }
}