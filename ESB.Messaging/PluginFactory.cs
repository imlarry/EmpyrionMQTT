using System;
using System.Collections.Generic;
using System.Reflection;

namespace ESB.Messaging
{
    // PluginFactory .. used to load and cache delegate api lookup info for a plugin dll and it's methods
    public class PluginFactory
    {
        private readonly BaseContextData _ctx;

        public PluginFactory(BaseContextData ctx)
        {
            _ctx = ctx;
        }

        public Dictionary<string, IPluginAction> CreatePluginActions(string assemblyPath)
        {
            var asm = Assembly.LoadFrom(assemblyPath);
            var pluginTypes = asm.GetTypes();
            var actions = new Dictionary<string, IPluginAction>();

            foreach (var pluginType in pluginTypes)
            {
                var methods = pluginType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                foreach (var method in methods)
                {
                    var parameters = method.GetParameters();
                    if (method.Name == "Execute" || parameters.Length != 2 || parameters[0].ParameterType != typeof(string) || parameters[1].ParameterType != typeof(string)) continue;
                    var instance = (IPluginAction)Activator.CreateInstance(pluginType, _ctx);
                    var subjectId = pluginType.Name + "." + method.Name;
                    actions.Add(subjectId, instance);
                }
            }
            return actions;
        }
    }
}
