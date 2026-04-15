using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Telemachus;
using UnityEngine;

namespace TelemachusTelemetryExtended
{
    internal sealed class PluginIntrospectionModule
    {
        private const string LogPrefix = "[TelemachusTelemetryExtended]";
        private static readonly string[] GlobalPluginCommands =
        {
            "extended.plugins.registered",
            "extended.plugins.loaded"
        };

        public IEnumerable<string> GlobalCommands => GlobalPluginCommands;

        public Func<Vessel, string[], object> TryGetGlobalHandler(string api)
        {
            switch (api)
            {
                case "extended.plugins.registered":
                    return (v, args) => GetRegisteredTelemachusPluginsPayload();
                case "extended.plugins.loaded":
                    return (v, args) => GetLoadedAssembliesPayload();
                default:
                    return null;
            }
        }

        public object GetLoadedAssembliesPayload()
        {
            var result = new List<object>();
            var seen = new Dictionary<string, bool>(StringComparer.Ordinal);

            foreach (var loaded in AssemblyLoader.loadedAssemblies)
            {
                try
                {
                    var asm = loaded.assembly;
                    if (asm == null)
                    {
                        continue;
                    }

                    var name = asm.GetName();
                    var fullName = name.FullName ?? string.Empty;
                    if (seen.ContainsKey(fullName))
                    {
                        continue;
                    }

                    seen[fullName] = true;
                    result.Add(new Dictionary<string, object>
                    {
                        { "name", name.Name ?? string.Empty },
                        { "version", name.Version?.ToString() ?? string.Empty },
                        { "fullName", name.FullName ?? string.Empty },
                        { "location", asm.Location ?? string.Empty }
                    });
                }
                catch
                {
                }
            }

            return result;
        }

        public object GetRegisteredTelemachusPluginsPayload()
        {
            var result = new List<Dictionary<string, object>>();

            try
            {
                var telemachusAssembly = typeof(IMinimalTelemachusPlugin).Assembly;
                var registrationType = telemachusAssembly.GetType("Telemachus.PluginRegistration", false);
                var managerProperty = registrationType?.GetProperty("Manager", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var manager = managerProperty?.GetValue(null, null);
                if (manager == null)
                {
                    return result;
                }

                var handlersField = manager.GetType().GetField("registeredPlugins", BindingFlags.Instance | BindingFlags.NonPublic);
                var handlers = handlersField?.GetValue(manager) as IEnumerable;
                if (handlers == null)
                {
                    return result;
                }

                foreach (var handler in handlers)
                {
                    if (handler == null)
                    {
                        continue;
                    }

                    var instanceField = handler.GetType().GetField("instance", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var instance = instanceField?.GetValue(handler);
                    if (instance == null)
                    {
                        continue;
                    }

                    var pluginType = instance.GetType();
                    var pluginAssembly = pluginType.Assembly.GetName();
                    var commands = new List<string>();
                    var commandsProp = pluginType.GetProperty("Commands", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var rawCommands = commandsProp?.GetValue(instance, null) as IEnumerable;
                    if (rawCommands != null)
                    {
                        foreach (var cmd in rawCommands)
                        {
                            if (cmd == null)
                            {
                                continue;
                            }

                            commands.Add(cmd.ToString());
                        }
                    }

                    commands.Sort(StringComparer.Ordinal);
                    result.Add(new Dictionary<string, object>
                    {
                        { "typeName", pluginType.FullName ?? pluginType.Name },
                        { "assembly", pluginAssembly.Name ?? string.Empty },
                        { "assemblyVersion", pluginAssembly.Version?.ToString() ?? string.Empty },
                        { "origin", string.Equals(pluginAssembly.Name, telemachusAssembly.GetName().Name, StringComparison.Ordinal) ? "internal" : "external" },
                        { "commands", commands }
                    });
                }

                result.Sort((a, b) =>
                    string.Compare(
                        a.ContainsKey("typeName") ? a["typeName"] as string : string.Empty,
                        b.ContainsKey("typeName") ? b["typeName"] as string : string.Empty,
                        StringComparison.Ordinal));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} could not read Telemachus registered plugins: {ex.GetType().Name}: {ex.Message}");
            }

            return result;
        }
    }
}
