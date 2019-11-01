#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import json
import socket
import logging
import traceback

logging.basicConfig(format='%(asctime)s %(message)s', level=logging.DEBUG, filename='log.log')


class SocketClient(object):

    def __init__(self, name, host, port, callback=None):
        if callback is None:
            callback = {}
        self.m_client_socket = None
        self.m_name = name
        self.m_callback = callback
        try:
            self.m_client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.m_client_socket.connect((host, port))
            self.normal_log("连接服务器成功: " + self.receive_data())
        except Exception as err:
            self.error_log("连接服务器失败")
            self.m_client_socket = None

    def error_log(self, title):
        logging.error("[" + self.m_name + "]> " + title + ": " + traceback.format_exc())

    def normal_log(self, text):
        logging.info("[" + self.m_name + "]> " + text)

    def login(self):
        if self.m_client_socket is not None:
            try:
                data = {"name": self.m_name, "action": "login"}
                send_str = json.dumps(data)
                self.m_client_socket.send(bytes(send_str, encoding='utf8'))
                self.normal_log("登录")
                receive = self.receive_data()
                """对应请求的回调函数，如果有就执行一下"""
                if data["action"] in self.m_callback:
                    resultData = json.loads(str(receive, encoding="utf-8"))
                    self.m_callback[data["action"]](resultData)
                return True
            except:
                self.error_log("登录错误")
                self.m_client_socket.shutdown(socket.SHUT_RDWR)
                self.m_client_socket.close()
        return False

    def send_message(self, to_client, message_json):
        result = ""
        if self.m_client_socket is not None:
            try:
                data = {"name": self.m_name, "action": "send", "to": to_client, "data": message_json}
                send_str = json.dumps(data)
                self.m_client_socket.send(bytes(send_str, encoding='utf8'))
                self.normal_log("发送消息")
                result = self.receive_data()
            except:
                self.error_log("发送消息错误")
                self.m_client_socket.shutdown(socket.SHUT_RDWR)
                self.m_client_socket.close()
        return result

    def listen(self):
        if self.m_client_socket is not None:
            try:
                while True:
                    data = {"name": self.m_name, "action": "listen"}
                    send_str = json.dumps(data)
                    self.m_client_socket.send(bytes(send_str, encoding='utf8'))
                    self.normal_log("发出监听请求: " + send_str)
                    receive = self.receive_data()
                    """对应请求的回调函数，如果有就执行一下"""
                    if data["action"] in self.m_callback:
                        resultData = json.loads(str(receive, encoding="utf-8"))
                        self.normal_log("监听消息接收: " + receive)
                        self.m_callback[data["action"]](resultData)
            except:
                self.error_log("监听消息错误")
                self.m_client_socket.shutdown(socket.SHUT_RDWR)
                self.m_client_socket.close()

    def close_connection(self):
        if self.m_client_socket is not None:
            self.m_client_socket.close()
            self.m_client_socket = None
            self.normal_log("客户端主动断开连接")

    def receive_data(self):
        result = ""
        buffer = []
        try:
            buffer = self.m_client_socket.recv(2048)
            result = str(buffer, encoding='utf-8')
            self.normal_log("接收服务器消息: " + result)
        except:
            self.error_log("接收消息错误")
        return result
