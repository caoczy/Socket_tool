using log4net;
using log4net.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApplication1
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        TcpServer server;
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public MainWindow()
        {
            InitializeComponent();
            server = new TcpServer(1024, 2048);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // serverList.Items.Add("hello");
            Task.Factory.StartNew(() =>
            {
                // Thread.Sleep(5000);
                int i = 0;
                while (true)
                {
                    this.Dispatcher.BeginInvoke(
                        (Action)(() =>
                        {
                            serverList.Items.Add(i.ToString());
                        }));
                    Thread.Sleep(5000);
                    i++;
                }
            });
        }

        private void StartServer(object sender, RoutedEventArgs e)
        {

            server.Start(8000);
            
        }

        private void StopServer(object sender, RoutedEventArgs e)
        {
            server.Stop();
        }

        private void SendMessage(object sender, RoutedEventArgs e)
        {
            log.Info("send message.");
            if (server.receiveContextPool.boundary > 0)
            {
                SocketAsyncEventArgs arg = server.receiveContextPool.Get(server.receiveContextPool.boundary);
                server.Send(arg, input.Text);
            }
        }
    }
}
