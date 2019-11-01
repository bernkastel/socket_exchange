import json
import socket
import logging
import traceback
import threading
import time

logging.basicConfig(format='%(asctime)s %(message)s', level=logging.DEBUG, filename='log.log')


class SocketServer(object):

    def __init__(self, ip_address, port):
        self.m_client_sockets = {}
        self.m_client_lock = threading.Lock()
        self.m_name = "服务器"
        self.m_server_socket = socket.socket()
        self.m_server_socket.bind((ip_address, port))
        """设定最多10个排队连接请求"""
        self.m_server_socket.listen(10)
        try:
            self.m_listen_thread = threading.Thread(target=self.listen_client_connect)
            self.m_listen_thread.start()
            self.m_listen_thread_run = 1
            self.normal_log("服务器监听线程开启: " + ip_address + ":" + str(port))
        except:
            self.m_listen_thread_run = 0
            self.error_log("开启监听线程失败")

    def error_log(self, title):
        logging.error("[" + self.m_name + "]> " + title + ": " + traceback.format_exc())

    def normal_log(self, text):
        logging.info("[" + self.m_name + "]> " + text)

    def stop_listening(self):
        if self.m_listen_thread is not None:
            self.m_listen_thread_run = 0
            self.m_listen_thread = None

    def get_client(self, name):
        return self.m_client_sockets[name] if name in self.m_client_sockets else None

    def get_all_clients(self):
        return self.m_client_sockets.values()

    def get_client_name(self, connection):
        for client_name in self.m_client_sockets.keys():
            if self.m_client_sockets[client_name].Socket == connection:
                return client_name
        return "未记录"

    def listen_client_connect(self):
        while True:
            connection, addr = self.m_server_socket.accept()
            respond_data = {"result": 0, "message": "连接成功"}
            connection.send(bytes(json.dumps(respond_data), encoding="utf8"))
            self.normal_log("接收连接请求：" + addr[0] + ":" + str(addr[1]))
            receive_thread = threading.Thread(target=self.receive_message, args=(connection,))
            receive_thread.start()

    def receive_message(self, connection: socket.socket):
        while True:
            try:
                receive_data = connection.recv(2048)
                if len(receive_data) == 0:
                    self.normal_log("接收中断： 客户端["+self.get_client_name(connection)+"]可能主动断开了连接")
                    self.remove_socket_from_client_sockets(connection)
                    connection.shutdown(socket.SHUT_RDWR)
                    connection.close()
                    break
                else:
                    if not self.on_process_json_query(connection, receive_data):
                        break
            except:
                self.error_log("接收中断")
                self.remove_socket_from_client_sockets(connection)
                break

    def client_login(self, client):
        self.m_client_lock.acquire()
        self.m_client_sockets[client.Name] = client
        self.m_client_lock.release()
        return True

    def on_process_json_query(self, client_socket, receive_data):
        try:
            data = json.loads(str(receive_data, encoding="utf-8"))
            client_name = data["name"] if "name" in data else ""
            if client_name != "":
                client_info = self.get_client(client_name)
                if client_info is None:
                    client_info = ClientInfo(client_name, client_socket, self)
                client_info.process(data)
                return True
            else:
                return False
        except:
            self.error_log("处理失败: " + str(receive_data, encoding="utf-8"))
            self.remove_socket_from_client_sockets(client_socket)
            client_socket.shutdown(socket.SHUT_RDWR)
            client_socket.close()
            return False

    def remove_socket_from_client_sockets(self, client_socket):
        remove_key = None
        for key in self.m_client_sockets:
            if self.m_client_sockets[key].Socket == client_socket:
                remove_key = key
                break
        if remove_key is not None and remove_key in self.m_client_sockets:
            self.m_client_sockets.pop(remove_key, None)
            self.normal_log("清理客户端数据: " + remove_key)


class Message(object):

    def __init__(self, from_client, to_client, data):
        self.m_from_client = from_client
        self.m_to_client = to_client
        self.m_data = data

    @property
    def From(self):
        return self.m_from_client

    @property
    def To(self):
        return self.m_to_client

    @property
    def Data(self):
        return self.m_data


class ClientInfo(object):

    def __init__(self, name, client_socket, server: SocketServer):
        self.m_name = name
        self.m_socket = client_socket
        self.m_server = server
        self.m_send_data_wait = []
        self.m_send_data_lock = threading.Lock()
        self.m_on_callbacks = {}
        self.regist_callback("login", self.on_login)
        self.regist_callback("send", self.on_send)
        self.regist_callback("listen", self.on_listen)
        self.regist_callback("echo", self.on_echo)

    def regist_callback(self, action, callback):
        self.m_on_callbacks[action] = callback

    def error_log(self, title):
        logging.error("[" + self.m_name + "]> " + title + ": " + traceback.format_exc())

    def normal_log(self, text):
        logging.info("[" + self.m_name + "]> " + text)

    @property
    def Name(self):
        return self.m_name

    @property
    def Socket(self):
        return self.m_socket

    def set_data(self, msg):
        self.m_send_data_lock.acquire()
        self.m_send_data_wait.append(msg)
        self.m_send_data_lock.release()

    def on_login(self, query_json, respond_json):
        if self.m_server.client_login(self):
            message = "登陆成功"
            respond_json["result"] = 0
        else:
            message = "登录失败"
        respond_json["data"] = message
        self.normal_log(message)
        return respond_json

    def on_send(self, query_json, respond_json):
        to_client_name = query_json["to"]
        send_data = query_json["data"]
        target_client = self.m_server.get_client(to_client_name)
        if target_client is not None:
            target_client.set_data(Message(self.Name, to_client_name, send_data))
            respond_json["result"] = 0
            message = "消息已转发"
        else:
            message = "消息转发失败，目标客户端[" + to_client_name + "]未发现"
            respond_json["action"] = "error"
        respond_json["data"] = message
        self.normal_log(message)
        return respond_json

    def on_listen(self, query_json, respond_json):
        while (len(self.m_send_data_wait) == 0):
            time.sleep(0.001)
        send_data = self.m_send_data_wait[0]
        respond_json["from"] = send_data.From
        respond_json["data"] = send_data.Data
        respond_json["result"] = 0
        message = "消息转发 [{0}] -> [{1}]"
        message = message.format(send_data.From, send_data.To)
        self.normal_log(message)
        self.m_send_data_lock.acquire()
        self.m_send_data_wait.pop(0)
        self.m_send_data_lock.release()
        return respond_json

    def on_echo(self, query_json, respond_json):
        respond_json["result"] = 0
        message = "接收客户端[{0}]消息({1})"
        message = message.format(self.Name, json.dumps(query_json))
        self.normal_log(message)
        return respond_json

    def process(self, query_json):
        respond_json = {"name": "Server", "result": 1}
        action = query_json["action"]
        if action in self.m_on_callbacks:
            respond_json = self.m_on_callbacks[action](query_json, respond_json)
        else:
            message = "无法识别的请求：" + action
            respond_json["action"] = "error"
            respond_json["data"] = message
            self.normal_log("请求处理失败(" + json.dumps(respond_json) + ")")
        self.m_socket.send(bytes(json.dumps(respond_json), encoding='utf8'))
