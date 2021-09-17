using System;
using System.Runtime.Serialization;

namespace SenseNet.IO.CLI
{
    [Serializable]
    public class ArgumentParserException : Exception
    {
        public ArgumentParserException() { }
        public ArgumentParserException(string message) : base(message) { }
        public ArgumentParserException(string message, Exception inner) : base(message, inner) { }
        protected ArgumentParserException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
