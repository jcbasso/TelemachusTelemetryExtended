using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TelemachusTelemetryExtended
{
    internal sealed class ScienceTelemetryModule
    {
        private const string LogPrefix = "[TelemachusTelemetryExtended]";
        private static readonly string[] FlightScienceCommands =
        {
            "extended.science.current"
        };
        private static readonly string[] GlobalScienceCommands = { "extended.science.global" };

        public IEnumerable<string> FlightCommands => FlightScienceCommands;
        public IEnumerable<string> GlobalCommands => GlobalScienceCommands;

        public Func<Vessel, string[], object> TryGetFlightHandler(string api)
        {
            switch (api)
            {
                case "extended.science.current":
                    return (v, args) => GetXScienceCurrentJson();
                default:
                    return null;
            }
        }

        public Func<Vessel, string[], object> TryGetGlobalHandler(string api)
        {
            switch (api)
            {
                case "extended.science.global":
                    return (v, args) => GetXScienceGlobalJson();
                default:
                    return null;
            }
        }

        public object GetXScienceCurrentJson()
        {
            if (!TryGetXScienceInstances(out var context, out var globalInstances))
            {
                return new Dictionary<string, object> { { "available", false }, { "items", new List<object>() } };
            }

            var currentInstances = TryGetXScienceCurrentInstances(context) ?? globalInstances;
            return new Dictionary<string, object> { { "available", true }, { "items", ScienceInstancesToJson(currentInstances) } };
        }

        public object GetXScienceGlobalJson()
        {
            if (!TryGetXScienceInstances(out _, out var globalInstances))
            {
                return new Dictionary<string, object> { { "available", false }, { "items", new List<object>() } };
            }

            return new Dictionary<string, object> { { "available", true }, { "items", ScienceInstancesToJson(globalInstances) } };
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

            context = ReflectionHelpers.ReadProperty(addon, "Science", LogPrefix);
            if (context == null)
            {
                return false;
            }

            ReflectionHelpers.TryInvokeMethod(context, "UpdateOnboardScience", LogPrefix);
            ReflectionHelpers.TryInvokeMethod(context, "UpdateScienceSubjects", LogPrefix);
            ReflectionHelpers.TryInvokeMethod(context, "UpdateExperiments", LogPrefix);
            ReflectionHelpers.TryInvokeMethod(context, "UpdateAllScienceInstances", LogPrefix);

            var all = ReflectionHelpers.ReadProperty(context, "AllScienceInstances", LogPrefix) as IEnumerable;
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

            var checklistWindow = ReflectionHelpers.ReadField(addon, "_checklistWindow", LogPrefix);
            var filter = checklistWindow != null ? ReflectionHelpers.ReadField(checklistWindow, "_filter", LogPrefix) : null;
            if (filter == null)
            {
                return null;
            }

            TryRefreshXScienceFilter(context, filter);

            var display = ReflectionHelpers.ReadProperty(filter, "DisplayScienceInstances", LogPrefix) as IEnumerable;
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

            var experimentsObj = ReflectionHelpers.ReadProperty(context, "Experiments", LogPrefix);
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

            ReflectionHelpers.TryInvokeMethod(filter, "UpdateFilter", LogPrefix, listObj);
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
            var situation = ReflectionHelpers.ReadProperty(instance, "Situation", LogPrefix);
            var bodyObj = situation != null ? ReflectionHelpers.ReadProperty(situation, "Body", LogPrefix) : null;

            var completedScience = ReflectionHelpers.ReadFloatProperty(instance, "CompletedScience", LogPrefix);
            var totalScience = ReflectionHelpers.ReadFloatProperty(instance, "TotalScience", LogPrefix);
            var progress = totalScience > 0f ? Math.Min(1f, completedScience / totalScience) : (ReflectionHelpers.ReadBoolProperty(instance, "IsComplete", LogPrefix) ? 1f : 0f);

            return new Dictionary<string, object>
            {
                { "id", ReflectionHelpers.ReadStringProperty(instance, "Id", LogPrefix) },
                { "description", ReflectionHelpers.ReadStringProperty(instance, "Description", LogPrefix) },
                { "shortDescription", ReflectionHelpers.ReadStringProperty(instance, "ShortDescription", LogPrefix) },
                { "experimentSituation", ReflectionHelpers.ReadProperty(situation, "ExperimentSituation", LogPrefix)?.ToString() ?? string.Empty },
                { "situationDescription", ReflectionHelpers.ReadStringProperty(situation, "Description", LogPrefix) },
                { "body", ReflectionHelpers.ReadProperty(bodyObj, "Name", LogPrefix)?.ToString() ?? bodyObj?.ToString() ?? string.Empty },
                { "biome", ReflectionHelpers.ReadStringProperty(situation, "Biome", LogPrefix) },
                { "subBiome", ReflectionHelpers.ReadStringProperty(situation, "SubBiome", LogPrefix) },
                { "completedScience", completedScience },
                { "totalScience", totalScience },
                { "progress", progress },
                { "isComplete", ReflectionHelpers.ReadBoolProperty(instance, "IsComplete", LogPrefix) },
                { "isUnlocked", ReflectionHelpers.ReadBoolProperty(instance, "IsUnlocked", LogPrefix) },
                { "isCollected", ReflectionHelpers.ReadBoolProperty(instance, "IsCollected", LogPrefix) },
                { "onboardScience", ReflectionHelpers.ReadFloatProperty(instance, "OnboardScience", LogPrefix) },
                { "rerunnable", ReflectionHelpers.ReadBoolProperty(instance, "Rerunnable", LogPrefix) }
            };
        }
    }
}
