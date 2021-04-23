using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JsonNetworking
{
    public class MessageEventArgs : EventArgs
    {
        NetworkMessage message;

        public MessageEventArgs(NetworkMessage networkMessage)
        {
            message = networkMessage;
        }
    }

    public static class NetworkDiscovery_Listener
    {
        public static bool listenForBroadcast = false;

        public static void StartListenForBroadcast(NetworkMessage serverData)
        {
            listenForBroadcast = true;
            Task.Run(() => ListenForBroadcast(serverData));
        }

        private static void ListenForBroadcast(NetworkMessage serverData)
        {
            UdpClient udp = new UdpClient(Constants.BROADCAST_PORT);

            while (listenForBroadcast)
            {
                IPEndPoint ip = null;
                byte[] bytes;
                string message = "";

                while (true)
                {
                    bytes = udp.Receive(ref ip);
                    message += Constants.MESSAGE_ENCODING.GetString(bytes);
                    if (message.IndexOf(Constants.EOF) > -1)
                    {
                        break;
                    }
                }
                message = message.Substring(0, message.LastIndexOf(Constants.EOF));

                if (NetworkMessage.Deserialize(message).IsMessageType(NetworkMessage.ServerSearch))
                {
                    bytes = serverData.ToBytes();
                    var client = new UdpClient();
                    IPEndPoint ep = new IPEndPoint(ip.Address, Constants.BROADCAST_RESPONSE_PORT);
                    client.Connect(ep);
                    client.Send(bytes, bytes.Length);
                }
            }
        }
    }

    public class NetworkDiscovery_Sender
    {
        public delegate void OnMessageReceived(NetworkDiscovery_Sender search, MessageEventArgs message);
        public event OnMessageReceived MessageReceived;

        public delegate void OnMessageSent(NetworkDiscovery_Sender search, MessageEventArgs args);
        public event OnMessageSent MessageSent;

        public bool sendBroadcast = true;

        public void StartBroadcastForServer(NetworkMessage serverData)
        {
            sendBroadcast = true;
            Task.Run(() => BroadcastForServer(serverData));
        }

        private void BroadcastForServer(NetworkMessage broadcastMessage)
        {
            while (sendBroadcast)
            {
                UdpClient udpClient = new UdpClient();
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, Constants.BROADCAST_PORT));
                var data = broadcastMessage.ToBytes();
                udpClient.Send(data, data.Length, IPAddress.Broadcast.ToString(), Constants.BROADCAST_PORT);

                MessageSent?.Invoke(this, new MessageEventArgs(broadcastMessage));

                Thread.Sleep(5000);
            }
        }
    }
}
