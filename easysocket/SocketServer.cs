using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace easysocket
{
    public class SocketServer
    {
        public SocketServer(int port, string ipAdd = "127.0.0.1")
        {
            IPAddress ip = IPAddress.Parse(ipAdd);
            m_client_sockets = new Dictionary<string, ClientInfo>();
            m_serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            m_serverSocket.Bind(new IPEndPoint(ip, port));  //绑定IP地址：端口
            m_serverSocket.Listen(10);    //设定最多10个排队连接请求
            Console.WriteLine("S> 启动监听端口{0}成功", m_serverSocket.LocalEndPoint.ToString());
            //通过Clientsoket发送数据
            m_listen_thread = new Thread(ListenClientConnect);
            m_listen_thread.Start();
        }

        public void StopListening()
        {
            if (m_listen_thread != null)
            {
                m_listen_thread.Abort();
                m_listen_thread = null;
            }
        }

        public ClientInfo GetClient(string name)
        {
            return m_client_sockets.ContainsKey(name)
                ? m_client_sockets[name]
                : null;
        }

        public ClientInfo[] GetAllClients()
        {
            return m_client_sockets.Values.ToArray();
        }

        private void ListenClientConnect()
        {
            while (true)
            {
                Socket clientSocket = m_serverSocket.Accept();
                clientSocket.Send(Encoding.UTF8.GetBytes("Server Say Hello"));
                (new Thread(ReceiveMessage)).Start(clientSocket);
            }
        }

        private void ReceiveMessage(object clientSocket)
        {
            Socket myClientSocket = (Socket)clientSocket;
            while (true)
            {
                try
                {
                    //通过clientSocket接收数据
                    int receiveNumber = myClientSocket.Receive(buffer);
                    string recvString = Encoding.UTF8.GetString(buffer, 0, receiveNumber);
                    if (recvString != "")
                    {
                        OnProcessJsonQuery(myClientSocket, recvString);
                    }
                    else
                    {
                        Console.WriteLine("S> 接收中断： 客户端可能主动断开了连接");
                        myClientSocket.Shutdown(SocketShutdown.Both);
                        myClientSocket.Close();
                        break;
                    }
                }
                catch (Exception err)
                {
                    Console.WriteLine("S> 接收中断： {0}:\n {1}", err.Message, err.StackTrace);
                    myClientSocket.Shutdown(SocketShutdown.Both);
                    myClientSocket.Close();
                    break;
                }
            }
        }

        public bool ClientLogin(ClientInfo client)
        {
            lock (m_client_sockets)
            {
                //TODO: 释放掉可能的旧链接
                m_client_sockets[client.Name] = client;
            }
            return true;
        }

        private bool OnProcessJsonQuery(Socket clientSocket, string recvString)
        {
            dynamic json = JObject.Parse(recvString);
            string clientName = json["name"].Value;
            try
            {
                //解析json并确定请求内容及请求者名称
                ClientInfo clientInfo = null;
                clientInfo = GetClient(clientName);
                if (clientInfo == null) { clientInfo = new ClientInfo(clientName, clientSocket, this); }
                clientInfo.Process(json);
                return true;
            }
            catch (Exception err)
            {
                Console.WriteLine("S> [{0}]处理失败： {1}: \n {2}", clientName, err.Message, err.StackTrace);
                clientSocket.Shutdown(SocketShutdown.Both);
                clientSocket.Close();
                return false;
            }
        }

        private void RemoveSocketFromClientSockets(Socket socket)
        {
            string removeKey = null;
            foreach(var pair in m_client_sockets)
            {
                if (pair.Value.Socket == socket)
                {
                    removeKey = pair.Key;
                    break;
                }
            }
            if (removeKey != null && m_client_sockets.ContainsKey(removeKey))
            {
                m_client_sockets.Remove(removeKey);
            }
        }

        public class ClientInfo
        {
            string m_name;
            SocketServer m_server;
            Socket m_socket;
            List<Message> m_send_data_wait;   //发送缓冲区

            public Socket Socket { get { return m_socket; } }

            public struct Message
            {
                public string From { get; }
                public string To { get; }
                public dynamic Data { get; }
                public Message(string from, string to, dynamic data)
                {
                    From = from;
                    To = to;
                    Data = data;
                }
            }

            public ClientInfo(string name, Socket socket, SocketServer server)
            {
                m_name = name;
                m_socket = socket;
                m_server = server;
                m_send_data_wait = new List<Message>();
            }
            public string Name { get { return m_name; } }
            public void SetData(Message msg)
            {
                lock (m_send_data_wait)
                {
                    m_send_data_wait.Add(msg);
                }
            }
            public void Process(dynamic queryJson)
            {
                JObject respondJson = new JObject();
                respondJson["name"] = "Server";
                respondJson["result"] = 1;
                string action = queryJson["action"].Value;
                if (action == "login")
                {
                    string message;
                    if (m_server.ClientLogin(this))
                    {
                        message = "登录成功";
                        respondJson["result"] = 0;
                    }
                    else
                    {
                        message = "登录失败";
                    }
                    respondJson["data"] = message;
                    Console.WriteLine("S> [{0}]{1}", Name, message);
                }
                else if (action == "send")
                {
                    string toClientName = queryJson["to"].Value;
                    dynamic sendData = queryJson["data"];
                    ClientInfo targetClient = m_server.GetClient(toClientName);
                    string message = "";
                    if (targetClient != null)
                    {
                        targetClient.SetData(new Message(Name, toClientName, sendData));
                        respondJson["result"] = 0;
                        message = "消息已转发";
                    }
                    else
                    {
                        message = "消息转发失败，目标客户端[" + toClientName + "]未发现";
                        respondJson["action"] = "error";
                    }
                    respondJson["data"] = message;
                    Console.WriteLine("S> {0}", message);
                }
                else if (action == "listen")
                {
                    while (m_send_data_wait.Count == 0) { Thread.Sleep(TimeSpan.FromSeconds(0.001)); }
                    var sendData = m_send_data_wait[0];
                    respondJson["from"] = sendData.From;
                    respondJson["data"] = sendData.Data;
                    respondJson["result"] = 0;
                    Console.WriteLine("S> 消息转发 [{0}] -> [{1}]", sendData.From, sendData.To);
                    m_send_data_wait.RemoveAt(0);
                }
                else if (action == "echo")
                {
                    respondJson["result"] = 0;
                    Console.WriteLine("S> 接收客户端[{0}]消息({1})", Name, queryJson.ToString());
                }
                else
                {
                    string message = "无法识别的请求：" + action;
                    respondJson["action"] = "error";
                    respondJson["data"] = message;
                    Console.WriteLine("S> 请求处理失败（" + respondJson.ToString() + "）");
                }
                m_socket.Send(Encoding.UTF8.GetBytes(respondJson.ToString()));
            }
        }

        private static byte[] buffer = new byte[100 * 1024];
        internal Socket m_serverSocket;
        internal Dictionary<string, ClientInfo> m_client_sockets;
        internal Thread m_listen_thread;
    }
}
