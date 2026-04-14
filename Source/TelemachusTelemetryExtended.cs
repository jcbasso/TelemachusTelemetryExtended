using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
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
            var result = new List<object>(contracts.Count);
            for (var i = 0; i < contracts.Count; i++)
            {
                result.Add(ContractToObject(contracts[i]));
            }

            return result;
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
                return new Dictionary<string, object>();
            }

            return ContractToObject(contracts[index]);
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

            var result = new Dictionary<string, object>();
            foreach (var key in keyOrder)
            {
                var items = grouped[key];
                var serializedItems = new List<object>(items.Count);
                for (var i = 0; i < items.Count; i++)
                {
                    serializedItems.Add(ContractToObject(items[i]));
                }

                result[key] = serializedItems;
            }

            return result;
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

        private static Dictionary<string, object> ContractToObject(Contract contract)
        {
            var parameters = new List<object>();
            foreach (var parameter in contract.AllParameters)
            {
                if (parameter == null || !parameter.Enabled)
                {
                    continue;
                }

                parameters.Add(new Dictionary<string, object>
                {
                    { "id", parameter.ID ?? string.Empty },
                    { "title", parameter.Title ?? string.Empty },
                    { "notes", parameter.Notes ?? string.Empty },
                    { "state", parameter.State.ToString() },
                    { "optional", parameter.Optional }
                });
            }

            return new Dictionary<string, object>
            {
                { "id", contract.ContractID },
                { "guid", contract.ContractGuid.ToString("N") },
                { "title", contract.Title ?? string.Empty },
                { "synopsys", contract.Synopsys ?? string.Empty },
                { "notes", contract.Notes ?? string.Empty },
                { "state", contract.ContractState.ToString() },
                { "localizedState", contract.LocalizedContractState ?? string.Empty },
                { "prestige", contract.Prestige.ToString() },
                { "dateAccepted", contract.DateAccepted },
                { "dateDeadline", contract.DateDeadline },
                { "parameters", parameters }
            };
        }

        private object GetXScienceCurrentJson()
        {
            if (!TryGetXScienceInstances(out var context, out var globalInstances))
            {
                return new Dictionary<string, object> { { "available", false }, { "items", new List<object>() } };
            }

            var currentInstances = TryGetXScienceCurrentInstances(context) ?? globalInstances;
            return new Dictionary<string, object> { { "available", true }, { "items", ScienceInstancesToJson(currentInstances) } };
        }

        private object GetXScienceGlobalJson()
        {
            if (!TryGetXScienceInstances(out _, out var globalInstances))
            {
                return new Dictionary<string, object> { { "available", false }, { "items", new List<object>() } };
            }

            return new Dictionary<string, object> { { "available", true }, { "items", ScienceInstancesToJson(globalInstances) } };
        }

        private object GetXScienceSummaryJson()
        {
            if (!TryGetXScienceInstances(out var context, out var globalInstances))
            {
                return new Dictionary<string, object>
                {
                    { "available", false },
                    { "globalCount", 0 },
                    { "globalComplete", 0 },
                    { "currentCount", 0 },
                    { "currentComplete", 0 },
                    { "globalProgress", 0.0 },
                    { "currentProgress", 0.0 }
                };
            }

            var currentInstances = TryGetXScienceCurrentInstances(context) ?? globalInstances;
            BuildScienceStats(globalInstances, out var globalComplete, out var globalProgress);
            BuildScienceStats(currentInstances, out var currentComplete, out var currentProgress);

            return new Dictionary<string, object>
            {
                { "available", true },
                { "globalCount", globalInstances.Count },
                { "globalComplete", globalComplete },
                { "currentCount", currentInstances.Count },
                { "currentComplete", currentComplete },
                { "globalProgress", globalProgress },
                { "currentProgress", currentProgress }
            };
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

        private static List<object> ScienceInstancesToJson(List<object> instances)
        {
            var result = new List<object>();
            if (instances != null)
            {
                for (var i = 0; i < instances.Count; i++)
                {
                    result.Add(ScienceInstanceToJson(instances[i]));
                }
            }

            return result;
        }

        private static Dictionary<string, object> ScienceInstanceToJson(object instance)
        {
            var situation = ReadProperty(instance, "Situation");
            var bodyObj = situation != null ? ReadProperty(situation, "Body") : null;

            var completedScience = ReadFloatProperty(instance, "CompletedScience");
            var totalScience = ReadFloatProperty(instance, "TotalScience");
            var progress = totalScience > 0f ? Math.Min(1f, completedScience / totalScience) : (ReadBoolProperty(instance, "IsComplete") ? 1f : 0f);

            return new Dictionary<string, object>
            {
                { "id", ReadStringProperty(instance, "Id") },
                { "description", ReadStringProperty(instance, "Description") },
                { "shortDescription", ReadStringProperty(instance, "ShortDescription") },
                { "experimentSituation", ReadProperty(situation, "ExperimentSituation")?.ToString() ?? string.Empty },
                { "situationDescription", ReadStringProperty(situation, "Description") },
                { "body", ReadProperty(bodyObj, "Name")?.ToString() ?? bodyObj?.ToString() ?? string.Empty },
                { "biome", ReadStringProperty(situation, "Biome") },
                { "subBiome", ReadStringProperty(situation, "SubBiome") },
                { "completedScience", completedScience },
                { "totalScience", totalScience },
                { "progress", progress },
                { "isComplete", ReadBoolProperty(instance, "IsComplete") },
                { "isUnlocked", ReadBoolProperty(instance, "IsUnlocked") },
                { "isCollected", ReadBoolProperty(instance, "IsCollected") },
                { "onboardScience", ReadFloatProperty(instance, "OnboardScience") },
                { "rerunnable", ReadBoolProperty(instance, "Rerunnable") }
            };
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

    }
}
