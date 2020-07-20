using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json.Linq;

namespace easysocket
{
    public class SocketClient
    {
        public SocketClient(string name, string serverIPAddress, int port)
        {
            Name = name;
            IPAddress ip = IPAddress.Parse(serverIPAddress);
            m_clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                m_clientSocket.Connect(new IPEndPoint(ip, port)); //配置服务器IP与端口
                Console.WriteLine("[{0}]> 连接服务器成功: {1}", Name, ReceiveData());
            }
            catch(Exception error)
            {
                Console.WriteLine("[{0}]> 连接服务器失败，{1}", Name, error.Message);
                m_clientSocket = null;
            }
        }

        public bool Login()
        {
            if (m_clientSocket != null)
            {
                try
                {
                    string result = "";
                    JObject json = new JObject();
                    json["name"] = Name;
                    json["action"] = "login";
                    m_clientSocket.Send(Encoding.UTF8.GetBytes(json.ToString()));
                    Console.WriteLine("[{0}]> 登录", Name);
                    result = ReceiveData();
                    dynamic resultJson = JObject.Parse(result);
                    return resultJson["result"].Value == 0;
                }
                catch (Exception err)
                {
                    Console.WriteLine("[{0}]> 登录错误：{1}", Name, err.Message);
                    m_clientSocket.Shutdown(SocketShutdown.Both);
                    m_clientSocket.Close();
                }
            }
            return false;
        }

        public string SendMessage(string toClient, dynamic message)
        {
            string result = "";
            if (m_clientSocket != null)
            {
                try
                {
                    JObject json = new JObject();
                    json["name"] = Name;
                    json["action"] = "send";
                    json["to"] = toClient;
                    json["data"] = message;
                    m_clientSocket.Send(Encoding.UTF8.GetBytes(json.ToString()));
                    Console.WriteLine("[{0}]> 发送消息：{1}", Name, message);
                    result = ReceiveData();
                }
                catch (Exception err)
                {
                    Console.WriteLine("[{0}]> 发送消息错误：{1}", Name, err.Message);
                    m_clientSocket.Shutdown(SocketShutdown.Both);
                    m_clientSocket.Close();
                }
            }
            return result;
        }

        public bool HeartBeat()
        {
            bool alive = false;
            string result = "";
            if (m_clientSocket != null)
            {
                try
                {
                    JObject json = new JObject();
                    json["name"] = Name;
                    json["action"] = "echo";
                    json["data"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    m_clientSocket.Send(Encoding.UTF8.GetBytes(json.ToString()));
                    result = ReceiveData();
                    alive = true;
                }
                catch (Exception err)
                {
                    Console.WriteLine("[{0}]> 发送心跳错误：{1}", Name, err.Message);
                    m_clientSocket.Shutdown(SocketShutdown.Both);
                    m_clientSocket.Close();
                }
            }
            return alive;
        }

        public void Listen()
        {
            if (m_clientSocket != null)
            {
                try
                {
                    while (true)
                    {
                        JObject json = new JObject();
                        json["name"] = Name;
                        json["action"] = "listen";
                        m_clientSocket.Send(Encoding.UTF8.GetBytes(json.ToString()));
                        Console.WriteLine("[{0}]> 发出监听请求（{1}）", Name, json.ToString());
                        var data = ReceiveData();
                        JObject jsonRecv = JObject.Parse(data);
                        Console.WriteLine("[{0}]> 监听消息接收（{1}）", Name, jsonRecv["data"].ToString());
                    }
                }
                catch (Exception err)
                {
                    Console.WriteLine("[{0}]> 监听消息错误：{1}", Name, err.Message);
                    m_clientSocket.Shutdown(SocketShutdown.Both);
                    m_clientSocket.Close();
                }
            }
        }

        public string SingleListen()
        {
            string result = null;
            if (m_clientSocket != null)
            {
                try
                {
                    JObject json = new JObject();
                    json["name"] = Name;
                    json["action"] = "listen";
                    m_clientSocket.Send(Encoding.UTF8.GetBytes(json.ToString()));
                    Console.WriteLine("[{0}]> 发出监听请求（{1}）", Name, json.ToString());
                    result = ReceiveData();
                    Console.WriteLine("[{0}]> 监听消息接收（{1}）", Name, result);
                }
                catch(Exception err)
                {
                    Console.WriteLine("[{0}]> 监听消息错误：{1}", Name, err.Message);
                    m_clientSocket.Shutdown(SocketShutdown.Both);
                    m_clientSocket.Close();
                }
            }
            return result;
        }

        public void CloseConnection()
        {
            if (m_clientSocket != null)
            {
                m_clientSocket.Close();
                m_clientSocket = null;
            }
        }

        internal string ReceiveData()
        {
            string result = "";
            try
            {
                byte[] buffer = new byte[100 * 1024];
                int receiveLength = m_clientSocket.Receive(buffer);
                result = Encoding.UTF8.GetString(buffer, 0, receiveLength);
                Console.WriteLine("[{0}]> 接收服务器消息：{1}", Name, result);
            }
            catch (Exception err)
            {
                Console.WriteLine("[{0}]> 接收消息错误：{1}", Name, err.Message);
            }
            return result;
        }

        public string Name { get; internal set; }
        internal Socket m_clientSocket;
    }
}
