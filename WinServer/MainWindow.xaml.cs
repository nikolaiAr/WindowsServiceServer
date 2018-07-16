using System;
using System.Collections.Generic;
using System.Linq;
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
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.ComponentModel;
using System.ServiceProcess;
using System.Collections;


namespace WinServer
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ArrayList array = new ArrayList();
        string str;
        ServiceController[] servc;
        bool flagStop = false;
        bool flagStart = false;
        bool flag;
        bool clientBeConnect = false;
        bool serverClosd = true;
        string nameServ;
        Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        Thread thread;
        Socket handler;

        public MainWindow()
        {
            InitializeComponent();
            stopServer.IsEnabled = false;

            IPAddress[] localIP = Dns.GetHostAddresses(Dns.GetHostName());

            foreach (IPAddress address in localIP)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    textIP.Text = address.ToString();
                }
            }

            servc = ServiceController.GetServices();
            int count = 0;
            foreach (ServiceController servc1 in servc)
            {
                array.Add(servc1.DisplayName);

                count++;
            }
            array.Sort();
            for (int i = 0; i < count; i++)
                str += array[i] + ",";
            str = str.Substring(0, str.Length - 1);

        }

        ServiceController[] GetServices()
        {
           return servc = ServiceController.GetServices();
        }

        public void SendMess(Socket handler,string str2)
        {
            try
            {
                byte[] msg = Encoding.UTF8.GetBytes(str2);
                handler.Send(msg);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public string GetMess(Socket socket)
        {
            byte[] bytes = new byte[1024];

            int bytesRec = socket.Receive(bytes);

            return Encoding.UTF8.GetString(bytes, 0, bytesRec);
        }

        private void clt()
        {

            Dispatcher.Invoke((Action)(() => { text.AppendText(Environment.NewLine + "ожидание подключения..." + Environment.NewLine); text.ScrollToEnd(); }));
            handler = listener.Accept();

            if (handler.Connected)
            {
                clientBeConnect = true;
                Dispatcher.Invoke((Action)(() => { text.AppendText("отправка клиенту списка служб" + Environment.NewLine); text.ScrollToEnd(); }));
                SendMess(handler, str);
                Dispatcher.Invoke((Action)(() => { text.AppendText("клиент подключен!" + Environment.NewLine); text.ScrollToEnd(); }));
                while (flag)
                {
                    try
                    {
                        if (!handler.Connected) break;
                        Dispatcher.Invoke((Action)(() => { text.AppendText("ожидание запроса от клиента..." + Environment.NewLine); text.ScrollToEnd(); }));
                        string srv = GetMess(handler);  //получение имени службы
                        if (srv == "stop")
                        {
                            Dispatcher.Invoke((Action)(() => { text.AppendText("клиент просит остановить службу" + nameServ + Environment.NewLine); text.ScrollToEnd(); }));
                            flagStop = true;
                            srv = nameServ;
                        }
                        if (srv == "start")
                        {
                            Dispatcher.Invoke((Action)(() => { text.AppendText("клиент просит запустить службу" + nameServ + Environment.NewLine); text.ScrollToEnd(); }));
                            flagStart = true;
                            srv = nameServ;
                        }
                        string service = null;
                        foreach (ServiceController servc1 in servc)
                        {
                            if (servc1.DisplayName == srv)
                            {
                                if (flagStart) { flagStart = false; servc1.Start(); /*while (servc1.Status.ToString() == "StartPending") Thread.Sleep(10);*/ }
                                if (flagStop) { flagStop = false; servc1.Stop(); /*while (servc1.Status.ToString() == "StopPending") Thread.Sleep(10);*/ }
                                nameServ = srv;
                                service = servc1.ServiceName;
                                break;
                            }
                        }
                        if (service != null) 
                        {
                            //Thread.Sleep(500);
                            servc = ServiceController.GetServices();
                            foreach (ServiceController servc1 in servc)
                            {
                                if (servc1.ServiceName == service)
                                {
                                    if ((servc1.Status.ToString() == "StartPending") || (servc1.Status.ToString() == "StopPending")) Thread.Sleep(500); // { servc = ServiceController.GetServices(); SendMess(handler, ""); break; }
                                    Dispatcher.Invoke((Action)(() => { text.AppendText("клиент просит информацию о службе " + servc1.DisplayName + Environment.NewLine); text.ScrollToEnd(); }));
                                    string temp = "имя: " + servc1.ServiceName + "   " + "состояние: " + servc1.Status;
                                    SendMess(handler, temp);
                                    break;
                                }
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke((Action)(() => { text.AppendText(ex.Message + Environment.NewLine); text.ScrollToEnd(); }));
                        if (handler.Connected)
                            SendMess(handler, ex.Message);
                        else
                        {
                            Dispatcher.Invoke((Action)(() => { text.AppendText("соединение будет разорвано... " + Environment.NewLine);
                            text.ScrollToEnd();
                            
                            flag = false;
                            serverClosd = true;
                            startServer.IsEnabled = true;
                            stopServer.IsEnabled = false;}));
                            SendMess(handler, "esc");
                        }
                            //stopServer_Click(this, EventArgs.Empty);
                        //MessageBox.Show("Выполните новое подключение!");
                    }
                }
            }
        }

        private void startServer_Click(object sender, RoutedEventArgs e)
        {
            serverClosd = false;
            try
            {
                IPAddress localAddress = IPAddress.Parse(textIP.Text);

                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                IPEndPoint ipEndpoint = new IPEndPoint(localAddress, int.Parse(textPort.Text));

                listener.Bind(ipEndpoint);

                listener.Listen(10);

                /*Thread*/
                thread = new Thread(clt);
                thread.Start();
                startServer.IsEnabled = false;
                stopServer.IsEnabled = true;
                flag = true;

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void stopServer_Click(object sender, EventArgs e)
        {
            serverClosd = true;
            text.AppendText("соединение будет разорвано... " + Environment.NewLine);
            text.ScrollToEnd();
            flag = false;
            if (!clientBeConnect)
                thread.IsBackground = true;
            SendMess(handler, "esc");
            //thread.Join();
            //thread.Abort();
            //thread.Join ();
            /**if (flag == false)
            {
                Thread trEnd = new Thread(EndThread);
                trEnd.Start();
            }*/
            startServer.IsEnabled = true;
            stopServer.IsEnabled = false;
        }

        

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!serverClosd)
            {MessageBox.Show("Завершите подключение"); e.Cancel = true;}

        }  
    }
}
