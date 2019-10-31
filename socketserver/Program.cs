using System;
using System.Threading;
using easysocket;

namespace socketserver
{
    class Program
    {
        static void Main(string[] args)
        {
            //var server = new SocketServer(10188);

            (new Thread(() =>
            {
                var client = new SocketClient("ClientSender", "127.0.0.1", 10188);
                if (client.Login())
                {
                    for (int i = 0; i < 10; i++)
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
                var client = new SocketClient("ClientListener", "127.0.0.1", 10188);
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
