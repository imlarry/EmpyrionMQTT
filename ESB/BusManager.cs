using System;
using System.IO;
using Eleon.Modding;
using Newtonsoft.Json.Linq;
using YamlDotNet.RepresentationModel;

namespace ESBGameMod
{
    public class BusManager
    {

        public async void Initialize(ContextData ctx)
        {
            // read yaml config file (which needs to exist to get this far)
            var filepath = ctx.ModApi.Application.GetPathFor(AppFolder.Mod) + "\\ESB";
            var reader = new StreamReader(filepath + "\\ESB_Info.yaml");
            var yaml = new YamlStream();
            yaml.Load(reader);
            var rootNode = (YamlMappingNode)yaml.Documents[0].RootNode;

            // subscribe to topics specified in ESB_Info
            var subscribeToTopics = ctx.ModApi.Application.Mode.ToString() + "Subscribe";
            var subscribeNode = (YamlSequenceNode)rootNode.Children[new YamlScalarNode(subscribeToTopics)];
            foreach (var topicNode in subscribeNode.Children)
            {
                var topic = (YamlScalarNode)topicNode["Topic"];
                await ctx.Messenger.Subscribe(topic.Value);
            }

            // register plugin dlls
            filepath += "\\Plugins\\";
            var pluginLoading = ctx.ModApi.Application.Mode.ToString() + "Plugins";
            var pluginNode = (YamlSequenceNode)rootNode.Children[new YamlScalarNode(pluginLoading)];
            foreach (var dllNode in pluginNode.Children)
            {
                var plugin = (YamlScalarNode)dllNode["Filename"];
                await ctx.Messenger.RegisterPlugin(filepath + plugin.Value);
            }

            // send out a hello
            var now = DateTime.Today.ToString("s");
            JObject json = new JObject(
                    new JProperty("Mode", ctx.ModApi.Application.Mode.ToString()),
                    new JProperty("ClientId", ctx.Messenger.ClientId()),
                    new JProperty("ConnectedAt", now)
                    );
            await ctx.Messenger.SendAsync("ModApi.Init/E", json.ToString(Newtonsoft.Json.Formatting.None));
        }
    }


}
