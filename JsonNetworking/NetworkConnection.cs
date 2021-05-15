using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JsonNetworking
{
    public class JsonConnection
    {
        public event MessageDelegate OnMessageSent;
        public event MessageDelegate OnMessageReceived;

        public event NetworkDelegate OnNewConnection;
        public event NetworkDelegate OnLostConnection;

        protected readonly List<Socket> connectedSockets = new List<Socket>();
        protected object lockObject = new object();
        protected CancellationTokenSource tokenSource = new CancellationTokenSource();

        protected JsonConnection()
        {
            OnNewConnection += JsonConnection_OnNewConnection;
            OnLostConnection += JsonConnection_OnLostConnection;
        }

        protected void DisconnectAll()
        {
            Socket[] sockets;
            lock (lockObject) sockets = connectedSockets.ToArray();
            foreach (Socket socket in sockets)
            {
                DisconnectSocket(socket);
            }
        }

        protected void DisconnectSocket(Socket socket)
        {
            OnLostConnection?.Invoke(this, new NetworkEventArgs(socket));
        }

        protected void HandleConnection(Socket socket)
        {
            string socketClosedReason = "";

            OnNewConnection?.Invoke(this, new NetworkEventArgs(socket));

            try
            {
                while (socket.Connected && !tokenSource.Token.IsCancellationRequested)
                {
                    ReceiveMessage(socket);
                }
            }
            catch (Exception socketException)
            {
                socketClosedReason = socketException.Message;
            }

            try
            {
                OnLostConnection?.Invoke(this, new NetworkEventArgs(socket, socketClosedReason));
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            } catch (ObjectDisposedException) { }
        }

        private void JsonConnection_OnLostConnection(object sender, NetworkEventArgs eventArgs)
        {
            lock (lockObject) connectedSockets.Remove(eventArgs.socket);
            eventArgs.socket.Shutdown(SocketShutdown.Both);
            eventArgs.socket.Close();
        }

        private void JsonConnection_OnNewConnection(object sender, NetworkEventArgs eventArgs)
        {
            lock (lockObject) connectedSockets.Add(eventArgs.socket);
        }

        private void ReceiveMessage(Socket socket)
        {
            string messageString = "";
            byte[] bytes;

            while (messageString.IndexOf(Constants.EOF) == -1)
            {
                tokenSource.Token.ThrowIfCancellationRequested();
                bytes = new byte[1024];
                socket.Receive(bytes);
                messageString += Constants.MESSAGE_ENCODING.GetString(bytes);
            }

            NetworkMessage message = NetworkMessage.Deserialize(messageString);

            OnMessageReceived?.Invoke(this, new MessageEventArgs(message));
        }

        public void SendMessage(NetworkMessage message)
        {
            Socket[] sockets;
            lock (lockObject) sockets = connectedSockets.ToArray();

            if (sockets.Length == 0) throw new NoConnectionException();

            byte[] bytes = message.ToBytes();

            foreach (Socket socket in sockets)
            {
                socket.Send(bytes);
            }

            OnMessageSent?.Invoke(this, new MessageEventArgs(message));
        }
    }

    public class JsonServer : JsonConnection
    {
        public Timer acceptConnectionTimer;
        public NetworkDiscovery_Listener BroadcastListener { get; private set; }
        public NetworkMessage ServerInfo { get; set; }
        
        public JsonServer(NetworkMessage serverInfo)
        {
            ServerInfo = serverInfo;
        }

        public void AcceptConnections(object listenerObject)
        {
            TcpListener listener = (TcpListener)listenerObject;

            while (listener.Pending())
            {
                Socket socket = listener.AcceptSocket();
                socket.ReceiveTimeout = Constants.SOCKET_TIMEOUT;
                socket.SendTimeout = Constants.SOCKET_TIMEOUT;
                Task.Run(() => HandleConnection(socket), tokenSource.Token);
            }
        }

        private void ListenForConnections(int maxConnections)
        {
            tokenSource = new CancellationTokenSource();
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = IPAddress.None;
            foreach (IPAddress address in host.AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipAddress = address;
                    break;
                }
            }
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, Constants.CONNECTION_PORT);

            TcpListener listener = new TcpListener(localEndPoint);
            listener.Start(maxConnections);

            acceptConnectionTimer = new Timer(AcceptConnections, listener, 0, 250);
        }

        public void StartServer(int maxConnections, bool listenForBroadcasts)
        {
            ListenForConnections(maxConnections);
        
            if (listenForBroadcasts)
            {
                BroadcastListener = new NetworkDiscovery_Listener();
                BroadcastListener.StartListenForBroadcast(ServerInfo);
            }
        }

        public void ShutDown()
        {
            tokenSource?.Cancel();
            acceptConnectionTimer?.Dispose();
        }
    }

    public class JsonClient : JsonConnection
    {
        public NetworkMessage ClientInfo { get; set; }

        public JsonClient(NetworkMessage clientInfo)
        {
            ClientInfo = clientInfo;
        }

        public void ConnectToServer(string serverIpAddress)
        {
            if (IPAddress.TryParse(serverIpAddress, out IPAddress serverIp))
            {
                ConnectToServer(serverIp);
            }
            else
            {
                throw new ArgumentException("String provided is not a valid IP Address.", "serverIpAddress");
            }
        }

        public void ConnectToServer(IPAddress serverIp)
        {
            IPEndPoint remoteEP = new IPEndPoint(serverIp, Constants.CONNECTION_PORT);
            Socket connection = new Socket(serverIp.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            connection.Connect(remoteEP);
            Task.Run(() => HandleConnection(connection));
        }

        public void Disconnect()
        {
            DisconnectAll();
        }
    }

    public class ConnectedComputer
    {
        public static Dictionary<Guid, ConnectedComputer> dictionary = new Dictionary<Guid, ConnectedComputer>();

        public Guid guid;
        public IPAddress ipAddress;
        public Socket socket;
    }
}
