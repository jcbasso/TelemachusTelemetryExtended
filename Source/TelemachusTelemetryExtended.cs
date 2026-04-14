using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Contracts;
using Telemachus;
using UnityEngine;

namespace TelemachusTelemetryExtended
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class TelemachusTelemetryExtendedPlugin : MonoBehaviour, IMinimalTelemachusPlugin, IDeregisterableTelemachusPlugin
    {
        private const string LogPrefix = "[TelemachusTelemetryExtended]";

        private readonly string[] _commands =
        {
            "extended.contracts.acceptedCount",
            "extended.contracts.accepted",
            "extended.contracts.byState",
            "extended.science.current",
            "extended.science.global",
            "extended.science.summary"
        };

        private bool _registered;
        private float _nextRegisterAttemptAt;
        private Action _deregister;

        public string[] Commands => _commands;

        public Action Deregister
        {
            private get => _deregister;
            set => _deregister = value;
        }

        private void Start()
        {
            TryRegisterPlugin();
        }

        private void OnDestroy()
        {
            _deregister?.Invoke();
        }

        private void Update()
        {
            if (!_registered && Time.unscaledTime >= _nextRegisterAttemptAt)
            {
                TryRegisterPlugin();
            }
        }

        public Func<Vessel, string[], object> GetAPIHandler(string api)
        {
            switch (api)
            {
                case "extended.contracts.acceptedCount":
                    return (v, args) => GetAcceptedContractsCount();
                case "extended.contracts.accepted":
                    return (v, args) => GetAcceptedContractsJsonOrByIndex(args);
                case "extended.contracts.byState":
                    return (v, args) => GetContractsByStateJson();
                case "extended.science.current":
                    return (v, args) => GetXScienceCurrentJson();
                case "extended.science.global":
                    return (v, args) => GetXScienceGlobalJson();
                case "extended.science.summary":
                    return (v, args) => GetXScienceSummaryJson();
                default:
                    return null;
            }
        }

        private void TryRegisterPlugin()
        {
            try
            {
                var telemachusAssembly = typeof(IMinimalTelemachusPlugin).Assembly;
                var registrationType = telemachusAssembly.GetType("Telemachus.PluginRegistration", false);
                var registerMethod = registrationType?.GetMethod("Register", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (registerMethod == null)
                {
                    throw new MissingMethodException("Telemachus.PluginRegistration.Register");
                }

                registerMethod.Invoke(null, new object[] { this });
                _registered = true;
                Debug.Log($"{LogPrefix} registered with Telemachus plugin API.");
            }
            catch (Exception ex)
            {
                _registered = false;
                _nextRegisterAttemptAt = Time.unscaledTime + 2f;
                Debug.LogWarning($"{LogPrefix} registration deferred: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private object GetAcceptedContractsCount()
        {
            return GetAcceptedContracts().Count;
        }

        private object GetAcceptedContractsJson()
        {
            var contracts = GetAcceptedContracts();
            var sb = new StringBuilder();
            sb.Append('[');
            for (var i = 0; i < contracts.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(ContractToJson(contracts[i]));
            }

            sb.Append(']');
            return sb.ToString();
        }

        private object GetAcceptedContractsJsonOrByIndex(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return GetAcceptedContractsJson();
            }

            if (!int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                return GetAcceptedContractsJson();
            }

            var contracts = GetAcceptedContracts();
            if (index < 0 || index >= contracts.Count)
            {
                return "{}";
            }

            return ContractToJson(contracts[index]);
        }

        private static List<Contract> GetAcceptedContracts()
        {
            var result = new List<Contract>();
            var system = ContractSystem.Instance;
            if (system == null || system.Contracts == null)
            {
                return result;
            }

            foreach (var contract in system.Contracts)
            {
                if (contract == null)
                {
                    continue;
                }

                // "Accepted" contracts are those that have started (date accepted set),
                // regardless of whether they are currently active or already completed/failed.
                if (contract.DateAccepted > 0.0)
                {
                    result.Add(contract);
                }
            }

            return result;
        }

        private static List<Contract> GetAllCurrentContracts()
        {
            var result = new List<Contract>();
            var system = ContractSystem.Instance;
            if (system == null || system.Contracts == null)
            {
                return result;
            }

            foreach (var contract in system.Contracts)
            {
                if (contract != null)
                {
                    result.Add(contract);
                }
            }

            return result;
        }

        private object GetContractsByStateJson()
        {
            var grouped = new Dictionary<string, List<Contract>>
            {
                { "generated", new List<Contract>() },
                { "offered", new List<Contract>() },
                { "offerExpired", new List<Contract>() },
                { "declined", new List<Contract>() },
                { "cancelled", new List<Contract>() },
                { "active", new List<Contract>() },
                { "completed", new List<Contract>() },
                { "deadlineExpired", new List<Contract>() },
                { "failed", new List<Contract>() },
                { "withdrawn", new List<Contract>() },
                { "other", new List<Contract>() }
            };

            foreach (var contract in GetAllCurrentContracts())
            {
                var stateKey = StateBucket(contract.ContractState);
                if (!grouped.TryGetValue(stateKey, out var list))
                {
                    list = grouped["other"];
                }

                list.Add(contract);
            }

            var keyOrder = new[]
            {
                "generated",
                "offered",
                "offerExpired",
                "declined",
                "cancelled",
                "active",
                "completed",
                "deadlineExpired",
                "failed",
                "withdrawn",
                "other"
            };

            var sb = new StringBuilder();
            sb.Append('{');
            var firstGroup = true;
            foreach (var key in keyOrder)
            {
                if (!firstGroup)
                {
                    sb.Append(',');
                }

                firstGroup = false;
                sb.Append('"').Append(key).Append('"').Append(':').Append('[');

                var items = grouped[key];
                for (var i = 0; i < items.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append(ContractToJson(items[i]));
                }

                sb.Append(']');
            }

            sb.Append('}');
            return sb.ToString();
        }

        private static string StateBucket(Contract.State state)
        {
            switch (state)
            {
                case Contract.State.Generated:
                    return "generated";
                case Contract.State.Offered:
                    return "offered";
                case Contract.State.OfferExpired:
                    return "offerExpired";
                case Contract.State.Declined:
                    return "declined";
                case Contract.State.Cancelled:
                    return "cancelled";
                case Contract.State.Active:
                    return "active";
                case Contract.State.Completed:
                    return "completed";
                case Contract.State.DeadlineExpired:
                    return "deadlineExpired";
                case Contract.State.Failed:
                    return "failed";
                case Contract.State.Withdrawn:
                    return "withdrawn";
                default:
                    return "other";
            }
        }

        private static string ContractToJson(Contract contract)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"id\":").Append(contract.ContractID.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"guid\":\"").Append(Escape(contract.ContractGuid.ToString("N"))).Append("\",");
            sb.Append("\"title\":\"").Append(Escape(contract.Title)).Append("\",");
            sb.Append("\"synopsys\":\"").Append(Escape(contract.Synopsys)).Append("\",");
            sb.Append("\"notes\":\"").Append(Escape(contract.Notes)).Append("\",");
            sb.Append("\"state\":\"").Append(Escape(contract.ContractState.ToString())).Append("\",");
            sb.Append("\"localizedState\":\"").Append(Escape(contract.LocalizedContractState)).Append("\",");
            sb.Append("\"prestige\":\"").Append(Escape(contract.Prestige.ToString())).Append("\",");
            sb.Append("\"dateAccepted\":").Append(contract.DateAccepted.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"dateDeadline\":").Append(contract.DateDeadline.ToString("0.###", CultureInfo.InvariantCulture)).Append(',');
            sb.Append("\"parameters\":[");

            var firstParam = true;
            foreach (var parameter in contract.AllParameters)
            {
                if (parameter == null || !parameter.Enabled)
                {
                    continue;
                }

                if (!firstParam)
                {
                    sb.Append(',');
                }

                firstParam = false;
                sb.Append('{');
                sb.Append("\"id\":\"").Append(Escape(parameter.ID)).Append("\",");
                sb.Append("\"title\":\"").Append(Escape(parameter.Title)).Append("\",");
                sb.Append("\"notes\":\"").Append(Escape(parameter.Notes)).Append("\",");
                sb.Append("\"state\":\"").Append(Escape(parameter.State.ToString())).Append("\",");
                sb.Append("\"optional\":").Append(parameter.Optional ? "true" : "false");
                sb.Append('}');
            }

            sb.Append(']');
            sb.Append('}');
            return sb.ToString();
        }

        private object GetXScienceCurrentJson()
        {
            if (!TryGetXScienceInstances(out var context, out var globalInstances))
            {
                return "{\"available\":false,\"items\":[]}";
            }

            var currentInstances = TryGetXScienceCurrentInstances(context) ?? globalInstances;
            return "{\"available\":true,\"items\":" + ScienceInstancesToJson(currentInstances) + "}";
        }

        private object GetXScienceGlobalJson()
        {
            if (!TryGetXScienceInstances(out _, out var globalInstances))
            {
                return "{\"available\":false,\"items\":[]}";
            }

            return "{\"available\":true,\"items\":" + ScienceInstancesToJson(globalInstances) + "}";
        }

        private object GetXScienceSummaryJson()
        {
            if (!TryGetXScienceInstances(out var context, out var globalInstances))
            {
                return "{\"available\":false,\"globalCount\":0,\"globalComplete\":0,\"currentCount\":0,\"currentComplete\":0,\"globalProgress\":0,\"currentProgress\":0}";
            }

            var currentInstances = TryGetXScienceCurrentInstances(context) ?? globalInstances;
            BuildScienceStats(globalInstances, out var globalComplete, out var globalProgress);
            BuildScienceStats(currentInstances, out var currentComplete, out var currentProgress);

            return "{"
                + "\"available\":true,"
                + "\"globalCount\":" + globalInstances.Count.ToString(CultureInfo.InvariantCulture) + ","
                + "\"globalComplete\":" + globalComplete.ToString(CultureInfo.InvariantCulture) + ","
                + "\"currentCount\":" + currentInstances.Count.ToString(CultureInfo.InvariantCulture) + ","
                + "\"currentComplete\":" + currentComplete.ToString(CultureInfo.InvariantCulture) + ","
                + "\"globalProgress\":" + globalProgress.ToString("0.###", CultureInfo.InvariantCulture) + ","
                + "\"currentProgress\":" + currentProgress.ToString("0.###", CultureInfo.InvariantCulture)
                + "}";
        }

        private static void BuildScienceStats(List<object> instances, out int completeCount, out double progress)
        {
            completeCount = 0;
            if (instances == null || instances.Count == 0)
            {
                progress = 0;
                return;
            }

            var totalProgress = 0.0;
            foreach (var instance in instances)
            {
                if (instance == null)
                {
                    continue;
                }

                var isComplete = ReadBoolProperty(instance, "IsComplete");
                if (isComplete)
                {
                    completeCount++;
                }

                var completedScience = ReadFloatProperty(instance, "CompletedScience");
                var totalScience = ReadFloatProperty(instance, "TotalScience");
                if (totalScience > 0f)
                {
                    totalProgress += Math.Min(1.0, completedScience / totalScience);
                }
                else
                {
                    totalProgress += isComplete ? 1.0 : 0.0;
                }
            }

            progress = totalProgress / instances.Count;
        }

        private static bool TryGetXScienceInstances(out object context, out List<object> globalInstances)
        {
            context = null;
            globalInstances = new List<object>();

            var addon = FindXScienceAddonInstance();
            if (addon == null)
            {
                return false;
            }

            context = ReadProperty(addon, "Science");
            if (context == null)
            {
                return false;
            }

            TryInvokeMethod(context, "UpdateOnboardScience");
            TryInvokeMethod(context, "UpdateScienceSubjects");
            TryInvokeMethod(context, "UpdateExperiments");
            TryInvokeMethod(context, "UpdateAllScienceInstances");

            var all = ReadProperty(context, "AllScienceInstances") as IEnumerable;
            if (all == null)
            {
                return false;
            }

            foreach (var item in all)
            {
                if (item != null)
                {
                    globalInstances.Add(item);
                }
            }

            return true;
        }

        private static List<object> TryGetXScienceCurrentInstances(object context)
        {
            var addon = FindXScienceAddonInstance();
            if (addon == null || context == null)
            {
                return null;
            }

            var checklistWindow = ReadField(addon, "_checklistWindow");
            var filter = checklistWindow != null ? ReadField(checklistWindow, "_filter") : null;
            if (filter == null)
            {
                return null;
            }

            TryRefreshXScienceFilter(context, filter);

            var display = ReadProperty(filter, "DisplayScienceInstances") as IEnumerable;
            if (display == null)
            {
                return null;
            }

            var result = new List<object>();
            foreach (var item in display)
            {
                if (item != null)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private static void TryRefreshXScienceFilter(object context, object filter)
        {
            if (context == null || filter == null)
            {
                return;
            }

            var experimentsObj = ReadProperty(context, "Experiments");
            if (!(experimentsObj is IDictionary dict))
            {
                return;
            }

            Type moduleScienceExperimentType = null;
            foreach (DictionaryEntry entry in dict)
            {
                if (entry.Value != null)
                {
                    moduleScienceExperimentType = entry.Value.GetType();
                    break;
                }
            }

            if (moduleScienceExperimentType == null)
            {
                return;
            }

            var listType = typeof(List<>).MakeGenericType(moduleScienceExperimentType);
            var listObj = Activator.CreateInstance(listType) as IList;
            if (listObj == null)
            {
                return;
            }

            foreach (DictionaryEntry entry in dict)
            {
                if (entry.Value != null)
                {
                    listObj.Add(entry.Value);
                }
            }

            TryInvokeMethod(filter, "UpdateFilter", listObj);
        }

        private static object FindXScienceAddonInstance()
        {
            var type = FindTypeInLoadedAssemblies("ScienceChecklist.ScienceChecklistAddon");
            if (type == null)
            {
                return null;
            }

            var objects = UnityEngine.Object.FindObjectsOfType(type);
            if (objects != null && objects.Length > 0)
            {
                return objects[0];
            }

            var allObjects = Resources.FindObjectsOfTypeAll(type);
            if (allObjects != null && allObjects.Length > 0)
            {
                return allObjects[0];
            }

            return null;
        }

        private static Type FindTypeInLoadedAssemblies(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var type = assemblies[i].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static string ScienceInstancesToJson(List<object> instances)
        {
            var sb = new StringBuilder();
            sb.Append('[');
            if (instances != null)
            {
                for (var i = 0; i < instances.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    sb.Append(ScienceInstanceToJson(instances[i]));
                }
            }

            sb.Append(']');
            return sb.ToString();
        }

        private static string ScienceInstanceToJson(object instance)
        {
            var situation = ReadProperty(instance, "Situation");
            var bodyObj = situation != null ? ReadProperty(situation, "Body") : null;

            var completedScience = ReadFloatProperty(instance, "CompletedScience");
            var totalScience = ReadFloatProperty(instance, "TotalScience");
            var progress = totalScience > 0f ? Math.Min(1f, completedScience / totalScience) : (ReadBoolProperty(instance, "IsComplete") ? 1f : 0f);

            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"id\":\"").Append(Escape(ReadStringProperty(instance, "Id"))).Append("\",");
            sb.Append("\"description\":\"").Append(Escape(ReadStringProperty(instance, "Description"))).Append("\",");
            sb.Append("\"shortDescription\":\"").Append(Escape(ReadStringProperty(instance, "ShortDescription"))).Append("\",");
            sb.Append("\"experimentSituation\":\"").Append(Escape(ReadProperty(situation, "ExperimentSituation")?.ToString())).Append("\",");
            sb.Append("\"situationDescription\":\"").Append(Escape(ReadStringProperty(situation, "Description"))).Append("\",");
            sb.Append("\"body\":\"").Append(Escape(ReadProperty(bodyObj, "Name")?.ToString() ?? bodyObj?.ToString())).Append("\",");
            sb.Append("\"biome\":\"").Append(Escape(ReadStringProperty(situation, "Biome"))).Append("\",");
            sb.Append("\"subBiome\":\"").Append(Escape(ReadStringProperty(situation, "SubBiome"))).Append("\",");
            sb.Append("\"completedScience\":").Append(completedScience.ToString("0.###", CultureInfo.InvariantCulture)).Append(",");
            sb.Append("\"totalScience\":").Append(totalScience.ToString("0.###", CultureInfo.InvariantCulture)).Append(",");
            sb.Append("\"progress\":").Append(progress.ToString("0.###", CultureInfo.InvariantCulture)).Append(",");
            sb.Append("\"isComplete\":").Append(ReadBoolProperty(instance, "IsComplete") ? "true" : "false").Append(",");
            sb.Append("\"isUnlocked\":").Append(ReadBoolProperty(instance, "IsUnlocked") ? "true" : "false").Append(",");
            sb.Append("\"isCollected\":").Append(ReadBoolProperty(instance, "IsCollected") ? "true" : "false").Append(",");
            sb.Append("\"onboardScience\":").Append(ReadFloatProperty(instance, "OnboardScience").ToString("0.###", CultureInfo.InvariantCulture)).Append(",");
            sb.Append("\"rerunnable\":").Append(ReadBoolProperty(instance, "Rerunnable") ? "true" : "false");
            sb.Append('}');
            return sb.ToString();
        }

        private static object ReadProperty(object target, string propertyName)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            var prop = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null)
            {
                return null;
            }

            try
            {
                return prop.GetValue(target, null);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} xScience read property '{propertyName}' failed: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private static object ReadField(object target, string fieldName)
        {
            if (target == null || string.IsNullOrEmpty(fieldName))
            {
                return null;
            }

            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return null;
            }

            try
            {
                return field.GetValue(target);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} xScience read field '{fieldName}' failed: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private static bool TryInvokeMethod(object target, string methodName, params object[] args)
        {
            if (target == null || string.IsNullOrEmpty(methodName))
            {
                return false;
            }

            var methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (var i = 0; i < methods.Length; i++)
            {
                if (!string.Equals(methods[i].Name, methodName, StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = methods[i].GetParameters();
                if ((args == null ? 0 : args.Length) != parameters.Length)
                {
                    continue;
                }

                try
                {
                    methods[i].Invoke(target, args);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"{LogPrefix} xScience invoke '{methodName}' failed: {ex.GetType().Name}: {ex.Message}");
                    return false;
                }
            }

            return false;
        }

        private static string ReadStringProperty(object target, string name)
        {
            return ReadProperty(target, name)?.ToString() ?? string.Empty;
        }

        private static bool ReadBoolProperty(object target, string name)
        {
            var value = ReadProperty(target, name);
            if (value is bool b)
            {
                return b;
            }

            return false;
        }

        private static float ReadFloatProperty(object target, string name)
        {
            var value = ReadProperty(target, name);
            if (value == null)
            {
                return 0f;
            }

            if (value is float f)
            {
                return f;
            }

            if (value is double d)
            {
                return (float)d;
            }

            if (value is decimal m)
            {
                return (float)m;
            }

            if (value is int i)
            {
                return i;
            }

            if (value is long l)
            {
                return l;
            }

            if (value is short s)
            {
                return s;
            }

            if (value is byte b)
            {
                return b;
            }

            if (value is string str && float.TryParse(str, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return 0f;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
