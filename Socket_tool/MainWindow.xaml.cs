using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

namespace Socket_tool
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        static Socket ser;
        public MainWindow()
        {
            InitializeComponent();
        }
        private void port_text_KeyDown(object sender, KeyEventArgs e)
        {
            TextBox txt = sender as TextBox;

            //屏蔽非法按键
            if ((e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) || e.Key == Key.Decimal || e.Key.ToString() == "Tab")
            {
                if (e.Key == Key.Decimal)
                {
                    e.Handled = true;
                    return;
                }
                e.Handled = false;
            }
            else if (((e.Key >= Key.D0 && e.Key <= Key.D9) || e.Key == Key.OemPeriod) && e.KeyboardDevice.Modifiers != ModifierKeys.Shift)
            {
                if (e.Key == Key.OemPeriod)
                {
                    e.Handled = true;
                    return;
                }
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
        }

        private void port_text_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            TextChange[] change = new TextChange[e.Changes.Count];
            e.Changes.CopyTo(change, 0);

            int offset = change[0].Offset;
            if (change[0].AddedLength > 0)
            {
                double num = 0;
                if (!Double.TryParse(textBox.Text, out num))
                {
                    textBox.Text = textBox.Text.Remove(offset, change[0].AddedLength);
                    textBox.Select(offset, 0);
                }
            }
        }

        private void start_server_Click(object sender, RoutedEventArgs e)
        {
            int port = 0;
            if (int.TryParse(port_text.Text, out port))
            {
                if (port == 0 || port > 65535)
                {
                    MessageBox.Show("错误", "端口数值错误!");
                }
                IPAddress ip = IPAddress.Parse("0.0.0.0");
                ser = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                ser.Bind(new IPEndPoint(ip, port));
                ser.Listen(10);
                Thread myThread = new Thread(listen_client);
                myThread.Start();
            }
        }
        private void listen_client()
        {
            byte[] res = new byte[1024];
            while (true)
            {
                Socket client = ser.Accept();
                this.Dispatcher.Invoke(new Action(() => {
                    client_list.Items.Add(client.RemoteEndPoint.ToString());
                }));
                int len = client.Receive(res);
                this.Dispatcher.Invoke(new Action(() => {
                    //recv_block.Text = Encoding.ASCII.GetString(res, 0, len);
                }));
            }
        }

        private void disconnect_Click(object sender, RoutedEventArgs e)
        {
            ser.Close();
        }
    }
   
}
