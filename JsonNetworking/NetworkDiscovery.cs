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
    public class NetworkDiscovery_Listener
    {
        public event MessageDelegate MessageSent;
        public event MessageDelegate MessageReceived;

        public bool listenForBroadcast = false;

        public void StartListenForBroadcast(NetworkMessage serverData)
        {
            listenForBroadcast = true;
            Task.Run(() => ListenForBroadcast(serverData));
        }

        private void ListenForBroadcast(NetworkMessage serverData)
        {
            UdpClient udp = new UdpClient(Constants.BROADCAST_PORT);

            while (listenForBroadcast)
            {
                IPEndPoint ip = null;
                byte[] bytes;
                string messageJson = "";

                while (true)
                {
                    bytes = udp.Receive(ref ip);
                    messageJson += Constants.MESSAGE_ENCODING.GetString(bytes);
                    if (messageJson.IndexOf(Constants.EOF) > -1)
                    {
                        break;
                    }
                }
                messageJson = messageJson.Substring(0, messageJson.LastIndexOf(Constants.EOF));

                NetworkMessage clientMessage = NetworkMessage.Deserialize(messageJson);
                MessageReceived?.Invoke(this, new MessageEventArgs(clientMessage));

                if (clientMessage.IsMessageType(NetworkMessage.ServerSearch))
                {
                    bytes = serverData.ToBytes();
                    var client = new UdpClient();
                    IPEndPoint ep = new IPEndPoint(ip.Address, Constants.BROADCAST_RESPONSE_PORT);
                    client.Connect(ep);
                    client.Send(bytes, bytes.Length);

                    MessageSent?.Invoke(this, new MessageEventArgs(serverData));
                }
            }
        }
    }

    public class NetworkDiscovery_Sender
    {
        public event MessageDelegate MessageSent;
        public event MessageDelegate MessageReceived;

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
