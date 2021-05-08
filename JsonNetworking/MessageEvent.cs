using System;
using System.Collections.Generic;
using System.Text;

namespace JsonNetworking
{
    public delegate void MessageDelegate(object search, MessageEventArgs message);

    public class MessageEventArgs : EventArgs
    {
        public NetworkMessage message;

        public MessageEventArgs(NetworkMessage networkMessage)
        {
            message = networkMessage;
        }
    }
}
