using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace JsonNetworking
{
    public delegate void MessageDelegate(object sender, MessageEventArgs messageArgs);

    public class MessageEventArgs : EventArgs
    {
        public NetworkMessage message;

        public MessageEventArgs(NetworkMessage networkMessage)
        {
            message = networkMessage;
        }
    }

    public delegate void NetworkDelegate(object sender, NetworkEventArgs eventArgs);

    public class NetworkEventArgs : EventArgs
    {
        public Socket socket;
        public string disconnectReason;
        public IPAddress ipAddress;

        public NetworkEventArgs(Socket eventSocket)
        {
            socket = eventSocket;
            ipAddress = ((IPEndPoint)eventSocket.RemoteEndPoint).Address;
        }

        public NetworkEventArgs(Socket eventSocket, string reason)
        {
            socket = eventSocket;
            ipAddress = ((IPEndPoint)eventSocket.RemoteEndPoint).Address;
            disconnectReason = reason;
        }
    }
}
