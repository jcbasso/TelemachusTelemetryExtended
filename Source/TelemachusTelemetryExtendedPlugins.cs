using System;
using System.Collections.Generic;
using Telemachus;
using UnityEngine;

namespace TelemachusTelemetryExtended
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public sealed class TelemachusTelemetryExtendedPlugin : PersistentTelemachusPluginBase
    {
        private const string PluginLogPrefix = "[TelemachusTelemetryExtended]";

        private static TelemachusTelemetryExtendedPlugin _instance;
        private readonly ContractsTelemetryModule _contracts = new ContractsTelemetryModule();
        private readonly ScienceTelemetryModule _science = new ScienceTelemetryModule();
        private readonly string[] _commands;

        public TelemachusTelemetryExtendedPlugin()
        {
            var commands = new List<string>(_contracts.FlightCommands);
            commands.AddRange(_science.FlightCommands);
            _commands = commands.ToArray();
        }

        protected override string LogPrefix => PluginLogPrefix;
        protected override string[] CommandList => _commands;

        protected override void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            base.Awake();
        }

        protected override void OnBeforeDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        protected override Func<Vessel, string[], object> ResolveHandler(string api)
        {
            var contractsHandler = _contracts.TryGetFlightHandler(api);
            if (contractsHandler != null)
            {
                return contractsHandler;
            }

            var scienceHandler = _science.TryGetFlightHandler(api);
            if (scienceHandler != null)
            {
                return scienceHandler;
            }
            
            return null;
        }
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public sealed class TelemachusTelemetryExtendedGlobalPlugin : PersistentTelemachusPluginBase, IMinimalGlobalTelemachusPlugin
    {
        private const string PluginLogPrefix = "[TelemachusTelemetryExtendedGlobal]";

        private static TelemachusTelemetryExtendedGlobalPlugin _instance;
        private readonly PluginIntrospectionModule _plugins = new PluginIntrospectionModule();
        private readonly ScienceTelemetryModule _science = new ScienceTelemetryModule();
        private readonly VesselsOrbitsTelemetryModule _vessels = new VesselsOrbitsTelemetryModule();
        private readonly string[] _commands;

        public TelemachusTelemetryExtendedGlobalPlugin()
        {
            var commands = new List<string>(_plugins.GlobalCommands);
            commands.AddRange(_science.GlobalCommands);
            commands.AddRange(_vessels.GlobalCommands);
            _commands = commands.ToArray();
        }

        protected override string LogPrefix => PluginLogPrefix;
        protected override string[] CommandList => _commands;

        protected override void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            base.Awake();
        }

        protected override void OnBeforeDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        protected override Func<Vessel, string[], object> ResolveHandler(string api)
        {
            var pluginHandler = _plugins.TryGetGlobalHandler(api);
            if (pluginHandler != null)
            {
                return pluginHandler;
            }

            var scienceHandler = _science.TryGetGlobalHandler(api);
            if (scienceHandler != null)
            {
                return scienceHandler;
            }

            var vesselsHandler = _vessels.TryGetGlobalHandler(api);
            if (vesselsHandler != null)
            {
                return vesselsHandler;
            }
            
            return null;
        }
    }
}
