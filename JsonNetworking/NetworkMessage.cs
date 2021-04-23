using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonNetworking
{
    public class NetworkMessage
    {
        [JsonIgnore] public string Serialize { get => JsonConvert.SerializeObject(this); }

        public static NetworkMessage ClientInfo { get => new NetworkMessage() { MessageType = "Client Info" }; }
        public static NetworkMessage ClientMessage { get => new NetworkMessage() { MessageType = "Client Message" }; }
        public static NetworkMessage InvalidMessage { get => new NetworkMessage() { MessageType = "Invalid Message" }; }
        public static NetworkMessage ServerSearch { get => new NetworkMessage() { MessageType = "Server Search" }; }
        public static NetworkMessage ServerInfo { get => new NetworkMessage() { MessageType = "Server Info" }; }
        public static NetworkMessage ServerMessage { get => new NetworkMessage() { MessageType = "Server Message" }; }

        public string Detail { get; set; }
        public string MessageType { get; set; }
        public string SenderName { get; set; }
        public string SenderIp { get; set; }
        public string SenderGuid { get; set; }
        public DateTime ReceivedTime { get; private set; }

        public static NetworkMessage Deserialize(string serializedString)
        {
            if (serializedString.Contains(Constants.EOF))
            {
                serializedString = serializedString.Substring(0, serializedString.LastIndexOf(Constants.EOF));
            }
            try
            {
                NetworkMessage message = JsonConvert.DeserializeObject<NetworkMessage>(serializedString);
                message.ReceivedTime = DateTime.Now;
                return message;
            }
            catch (JsonReaderException)
            {
                return InvalidMessage;
            }
        }

        public bool IsMessageType(NetworkMessage otherMessage)
        {
            return MessageType == otherMessage.MessageType;
        }

        public byte[] ToBytes()
        {
            return Constants.MESSAGE_ENCODING.GetBytes(Serialize + Constants.EOF);
        }

        public static NetworkMessage FromBytes(byte[] bytes)
        {
            string serializedString = Constants.MESSAGE_ENCODING.GetString(bytes);
            serializedString = serializedString.Substring(0, serializedString.LastIndexOf(Constants.EOF));
            return Deserialize(serializedString);
        }
    }
}
