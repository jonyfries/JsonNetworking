using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsonNetworking
{
    class Constants
    {
        public const int BROADCAST_PORT = 23938;
        public const int BROADCAST_RESPONSE_PORT = 23939;
        public const int CONNECTION_PORT = 23940;

        public const string EOF = "<~END OF NETWORK MESSAGE~>";
        public static readonly Encoding MESSAGE_ENCODING = Encoding.UTF32;
    }
}
