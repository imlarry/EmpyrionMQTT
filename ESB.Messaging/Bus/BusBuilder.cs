using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
        private IServiceProvider _provider;

        private readonly List<Action<IMessenger, List<SubscriptionSpec>>> _registrations
            = new List<Action<IMessenger, List<SubscriptionSpec>>>();
        private readonly List<Type>     _handlerTypes    = new List<Type>();
        private readonly List<Assembly> _scanAssemblies  = new List<Assembly>();

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

        public BusBuilder WithServiceProvider(IServiceProvider provider)
            { _provider = provider; return this; }

        // -- Lambda handler registration -----------------------------------------

        public BusBuilder OnEvent<T>(string scope, string operation,
            Func<MessageEnvelope<T>, Task> handler)
        {
            _registrations.Add((m, subs) => {
                var key = NormalizeScope(scope) + "/evt/" + operation;
                m.RegisterHandler(key, ctx => handler(MessageEnvelope<T>.From(ctx)));
                subs.Add(new SubscriptionSpec(NormalizeScope(scope), MessageType.Evt, operation));
            });
            return this;
        }

        public BusBuilder OnEvent(string scope, string operation,
            Func<MessageEnvelope, Task> handler)
        {
            _registrations.Add((m, subs) => {
                var key = NormalizeScope(scope) + "/evt/" + operation;
                m.RegisterHandler(key, ctx => handler(MessageEnvelope.From(ctx)));
                subs.Add(new SubscriptionSpec(NormalizeScope(scope), MessageType.Evt, operation));
            });
            return this;
        }

        public BusBuilder OnRequest<TReq, TRes>(string scope, string operation,
            Func<MessageEnvelope<TReq>, Task<TRes>> handler)
        {
            _registrations.Add((m, subs) => {
                var key = NormalizeScope(scope) + "/req/" + operation;
                m.RegisterHandler(key, async ctx => {
                    var envelope = MessageEnvelope<TReq>.From(ctx);
                    var response = await handler(envelope).ConfigureAwait(false);
                    if (ctx.ResponseTopic != null)
                    {
                        var json = JsonConvert.SerializeObject(response);
                        await m.ReplyAsync(ctx.ResponseTopic, ctx.CorrelationData, json)
                               .ConfigureAwait(false);
                    }
                });
                subs.Add(new SubscriptionSpec(NormalizeScope(scope), MessageType.Req, operation));
            });
            return this;
        }

        public BusBuilder OnRequest(string scope, string operation,
            Func<MessageEnvelope, Task<string>> handler)
        {
            _registrations.Add((m, subs) => {
                var key = NormalizeScope(scope) + "/req/" + operation;
                m.RegisterHandler(key, async ctx => {
                    var envelope = MessageEnvelope.From(ctx);
                    var json = await handler(envelope).ConfigureAwait(false);
                    if (ctx.ResponseTopic != null && json != null)
                    {
                        await m.ReplyAsync(ctx.ResponseTopic, ctx.CorrelationData, json)
                               .ConfigureAwait(false);
                    }
                });
                subs.Add(new SubscriptionSpec(NormalizeScope(scope), MessageType.Req, operation));
            });
            return this;
        }

        // -- Interface-based handler registration --------------------------------

        public BusBuilder AddHandler(Type handlerType)
            { _handlerTypes.Add(handlerType); return this; }

        public BusBuilder ScanAssembly(Assembly assembly)
            { _scanAssemblies.Add(assembly); return this; }

        // -- Build ---------------------------------------------------------------

        public IMessageBus Build()
        {
            if (_messenger == null)
                throw new InvalidOperationException("WithMessenger() is required.");

            if (_compressionThreshold >= 0)
                _messenger.CompressionThreshold = _compressionThreshold;

            var subscriptions = new List<SubscriptionSpec>();
            var provider = _provider;

            // Collect types from assembly scans
            foreach (var assembly in _scanAssemblies)
            {
                foreach (var type in assembly.GetTypes())
                {
                    var attr = (BusRouteAttribute)Attribute.GetCustomAttribute(
                        type, typeof(BusRouteAttribute));
                    if (attr == null) continue;
                    RegisterHandlerType(type, attr, _messenger, subscriptions, provider);
                }
            }

            // Explicit AddHandler() types
            foreach (var type in _handlerTypes)
            {
                var attr = (BusRouteAttribute)Attribute.GetCustomAttribute(
                    type, typeof(BusRouteAttribute));
                if (attr == null)
                    throw new InvalidOperationException(
                        type.Name + " is missing [BusRoute] attribute.");
                RegisterHandlerType(type, attr, _messenger, subscriptions, provider);
            }

            // Lambda registrations
            foreach (var reg in _registrations)
                reg(_messenger, subscriptions);

            return new MessageBus(_messenger, _participantType,
                _host, _port, _username, _password, _caFilePath, subscriptions);
        }

        // -- Reflection helpers --------------------------------------------------

        private static readonly MethodInfo _eventInvokerMethod =
            typeof(BusBuilder).GetMethod("InvokeEventHandler",
                BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly MethodInfo _requestInvokerMethod =
            typeof(BusBuilder).GetMethod("InvokeRequestHandler",
                BindingFlags.NonPublic | BindingFlags.Static);

        private static void RegisterHandlerType(Type type, BusRouteAttribute attr,
            IMessenger messenger, List<SubscriptionSpec> subscriptions, IServiceProvider provider)
        {
            var scope = NormalizeScope(attr.Scope);

            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType) continue;
                var def  = iface.GetGenericTypeDefinition();
                var args = iface.GetGenericArguments();

                if (def == typeof(IEventHandler<>) && args.Length == 1)
                {
                    var eventType = args[0];
                    var key       = scope + "/evt/" + attr.Operation;
                    var invoker   = _eventInvokerMethod.MakeGenericMethod(eventType);
                    messenger.RegisterHandler(key, ctx => {
                        var handler = ResolveHandler(provider, type);
                        return (Task)invoker.Invoke(null, new object[] { ctx, handler });
                    });
                    subscriptions.Add(new SubscriptionSpec(scope, MessageType.Evt, attr.Operation));
                    return;
                }

                if (def == typeof(IRequestHandler<,>) && args.Length == 2)
                {
                    var requestType  = args[0];
                    var responseType = args[1];
                    var key          = scope + "/req/" + attr.Operation;
                    var invoker      = _requestInvokerMethod.MakeGenericMethod(requestType, responseType);
                    messenger.RegisterHandler(key, ctx => {
                        var handler = ResolveHandler(provider, type);
                        return (Task)invoker.Invoke(null, new object[] { ctx, handler, messenger });
                    });
                    subscriptions.Add(new SubscriptionSpec(scope, MessageType.Req, attr.Operation));
                    return;
                }
            }
        }

        private static object ResolveHandler(IServiceProvider provider, Type type)
        {
            if (provider != null)
            {
                var instance = provider.GetService(type);
                if (instance != null) return instance;
            }
            return Activator.CreateInstance(type);
        }

        private static Task InvokeEventHandler<TEvent>(MessageContext ctx, object handler)
        {
            var envelope = MessageEnvelope<TEvent>.From(ctx);
            return ((IEventHandler<TEvent>)handler).HandleAsync(envelope);
        }

        private static async Task InvokeRequestHandler<TRequest, TResponse>(
            MessageContext ctx, object handler, IMessenger messenger)
        {
            var envelope = MessageEnvelope<TRequest>.From(ctx);
            var response = await ((IRequestHandler<TRequest, TResponse>)handler)
                .HandleAsync(envelope).ConfigureAwait(false);
            if (ctx.ResponseTopic != null)
            {
                var json = JsonConvert.SerializeObject(response);
                await messenger.ReplyAsync(ctx.ResponseTopic, ctx.CorrelationData, json)
                               .ConfigureAwait(false);
            }
        }

        internal static string NormalizeScope(string scope)
        {
            if (string.IsNullOrEmpty(scope)) return scope ?? string.Empty;
            if (char.IsUpper(scope[0])) return scope;
            return char.ToUpper(scope[0]) + scope.Substring(1);
        }
    }
}
