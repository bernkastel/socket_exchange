import logging
import threading
import time
import sys 

import socket_client
import socket_server


def start_client_send():
    client = socket_client.SocketClient(name="ClientSender", host="127.0.0.1", port=10188)
    if client.login():
        for i in range(0, 10):
            time.sleep(1)
            client.send_message("ClientListener", "测试请求" + str(i + 1))
    client.close_connection()


def start_client_listen():
    client = socket_client.SocketClient(name="ClientListener", host="127.0.0.1", port=10188)
    if client.login():
        client.listen()
    client.close_connection()


ip_add = sys.argv[1] if len(sys.argv) > 1 else "47.100.223.124"
port = int(sys.argv[2]) if len(sys.argv) > 2 else 10188
server = socket_server.SocketServer(ip_add, port)
#client_send_thread = threading.Thread(target=start_client_send)
#client_send_thread.start()
#client_listen_thread = threading.Thread(target=start_client_listen())
#client_listen_thread.start()
