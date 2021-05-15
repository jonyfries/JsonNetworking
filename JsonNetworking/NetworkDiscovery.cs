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
        public event MessageDelegate OnMessageSent;
        public event MessageDelegate OnMessageReceived;

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
                clientMessage.SenderIp = ip.Address.ToString();
                OnMessageReceived?.Invoke(this, new MessageEventArgs(clientMessage));

                if (clientMessage.IsMessageType(NetworkMessage.ServerSearch))
                {
                    bytes = serverData.ToBytes();
                    var client = new UdpClient();
                    IPEndPoint ep = new IPEndPoint(ip.Address, Constants.BROADCAST_RESPONSE_PORT);
                    client.Connect(ep);
                    client.Send(bytes, bytes.Length);

                    OnMessageSent?.Invoke(this, new MessageEventArgs(serverData));
                }
            }
        }
    }

    public class NetworkDiscovery_Sender
    {
        public event MessageDelegate OnMessageSent;
        public event MessageDelegate OnMessageReceived;

        UdpClient broadcastUdp;
        UdpClient listenUdp;
        private object lockObject = new object();

        private List<DiscoveredServer> activeServers = new List<DiscoveredServer>();
        public List<DiscoveredServer> ActiveServers
        {
            get
            {
                lock (lockObject) { return new List<DiscoveredServer>(activeServers); }
            }
        }
        public bool sendBroadcast = true;
        public TimeSpan BroadcastPause { get; set; } = new TimeSpan(0,0,1);

        public void StartBroadcastForServer(NetworkMessage serverData)
        {
            sendBroadcast = true;
            Task.Run(() => BroadcastForServer(serverData));
            Task.Run(() => ListenForResponse());
            Task.Run(() => MaintainServerList());
        }

        private void BroadcastForServer(NetworkMessage broadcastMessage)
        {
            broadcastUdp = new UdpClient();
            broadcastUdp.Client.Bind(new IPEndPoint(IPAddress.Any, Constants.BROADCAST_PORT));

            while (sendBroadcast)
            {
                var data = broadcastMessage.ToBytes();
                broadcastUdp.Send(data, data.Length, IPAddress.Broadcast.ToString(), Constants.BROADCAST_PORT);

                OnMessageSent?.Invoke(this, new MessageEventArgs(broadcastMessage));

                Thread.Sleep(BroadcastPause);
            }
        }

        private void ListenForResponse()
        {
            listenUdp = new UdpClient(Constants.BROADCAST_RESPONSE_PORT);

            while (sendBroadcast)
            {
                IPEndPoint ip = null;
                byte[] bytes;
                string messageJson = "";

                while (true)
                {
                    bytes = listenUdp.Receive(ref ip);
                    messageJson += Constants.MESSAGE_ENCODING.GetString(bytes);
                    if (messageJson.IndexOf(Constants.EOF) > -1)
                    {
                        break;
                    }
                }
                messageJson = messageJson.Substring(0, messageJson.LastIndexOf(Constants.EOF));

                NetworkMessage serverMessage = NetworkMessage.Deserialize(messageJson);
                serverMessage.SenderIp = ip.Address.ToString();
                OnMessageReceived?.Invoke(this, new MessageEventArgs(serverMessage));
            }
        }

        private void MaintainServerList()
        {
            OnMessageReceived += NetworkDiscovery_Sender_MessageReceived;
            while (sendBroadcast)
            {
                lock (lockObject)
                {
                    for (int i = 0; i < activeServers.Count; ++i)
                    {
                        if ((DateTime.Now - activeServers[i].LastActiveTime).TotalMilliseconds > BroadcastPause.TotalMilliseconds * 2)
                        {
                            activeServers.RemoveAt(i--);
                        }
                    }
                }

                Thread.Sleep((int)BroadcastPause.TotalMilliseconds / 2);
            }
            activeServers.Clear();
            OnMessageReceived -= NetworkDiscovery_Sender_MessageReceived;
        }

        private void NetworkDiscovery_Sender_MessageReceived(object search, MessageEventArgs message)
        {
            lock (lockObject)
            {
                for (int i = 0; i < activeServers.Count; ++i)
                {
                    if (activeServers[i].Ip == message.message.SenderIp)
                    {
                        activeServers[i].LastActiveTime = message.message.ReceivedTime;
                        return;
                    }
                }

                activeServers.Add(new DiscoveredServer(message));
            }
        }

        public void StopBroadcast()
        {
            sendBroadcast = false;
            broadcastUdp.Close();
            listenUdp.Close();
        }
    }

    public class DiscoveredServer
    {
        public string Detail { get; set; }
        public string Name { get; set; }
        public string Ip { get; set; }
        public string Guid { get; set; }
        public DateTime LastActiveTime { get; set; }

        internal DiscoveredServer(MessageEventArgs message)
        {
            Detail = message.message.Detail;
            Name = message.message.SenderName;
            Ip = message.message.SenderIp;
            Guid = message.message.SenderGuid;
            LastActiveTime = message.message.ReceivedTime;
        }
    }
}
