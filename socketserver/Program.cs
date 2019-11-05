using System;
using System.Threading;
using easysocket;

namespace socketserver
{
    class Program
    {

        const string IP_ADDR = "127.0.0.1"; //47.100.223.124
        static void Main(string[] args)
        {
            //var server = new SocketServer(10188);

            (new Thread(() =>
            {
                var client = new SocketClient("ClientSender", IP_ADDR, 10188);
                if (client.Login())
                {
                    for (int i = 0; i < 2; i++)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                        var recv = client.SendMessage("ClientListener", "请求," + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    }
                }
                client.CloseConnection();
            })).Start();

            Thread.Sleep(TimeSpan.FromSeconds(0.5));

            (new Thread(() =>
            {
                var client = new SocketClient("ClientListener", IP_ADDR, 10188);
                if (client.Login())
                {
                    client.Listen();
                }
                client.CloseConnection();
            })).Start();


            Thread.Sleep(TimeSpan.FromSeconds(1000));

        }
    }
}
