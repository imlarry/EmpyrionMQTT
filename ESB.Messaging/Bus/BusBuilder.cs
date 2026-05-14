using System;

namespace ESB.Messaging
{
    public class BusBuilder
    {
        private IMessenger _messenger;
        private string _participantType;
        private string _host       = "localhost";
        private int    _port       = 1883;
        private string _username;
        private string _password;
        private string _caFilePath;
        private int    _compressionThreshold = -1;

        // -- Configuration -------------------------------------------------------

        public BusBuilder WithMessenger(IMessenger messenger)
            { _messenger = messenger; return this; }

        public BusBuilder WithParticipantType(string participantType)
            { _participantType = participantType; return this; }

        public BusBuilder WithConnection(string host = "localhost", int port = 1883)
            { _host = host; _port = port; return this; }

        public BusBuilder WithCredentials(string username, string password)
            { _username = username; _password = password; return this; }

        public BusBuilder WithCertificate(string caFilePath)
            { _caFilePath = caFilePath; return this; }

        public BusBuilder WithCompressionThreshold(int bytes)
            { _compressionThreshold = bytes; return this; }

        // -- Build ---------------------------------------------------------------

        public IMessageBus Build()
        {
            if (_messenger == null)
                throw new InvalidOperationException("WithMessenger() is required.");

            if (_compressionThreshold >= 0)
                _messenger.CompressionThreshold = _compressionThreshold;

            return new MessageBus(_messenger, _participantType,
                _host, _port, _username, _password, _caFilePath);
        }

        // -- Utility -------------------------------------------------------------

        internal static string NormalizeScope(string scope)
        {
            if (string.IsNullOrEmpty(scope)) return scope ?? string.Empty;
            if (char.IsUpper(scope[0])) return scope;
            return char.ToUpper(scope[0]) + scope.Substring(1);
        }
    }
}
