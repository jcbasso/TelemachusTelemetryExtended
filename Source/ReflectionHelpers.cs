using System;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace TelemachusTelemetryExtended
{
    internal static class ReflectionHelpers
    {
        public static object ReadProperty(object target, string propertyName, string logPrefix)
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
                Debug.LogWarning($"{logPrefix} read property '{propertyName}' failed: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        public static object ReadField(object target, string fieldName, string logPrefix)
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
                Debug.LogWarning($"{logPrefix} read field '{fieldName}' failed: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        public static bool TryInvokeMethod(object target, string methodName, string logPrefix, params object[] args)
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
                    Debug.LogWarning($"{logPrefix} invoke '{methodName}' failed: {ex.GetType().Name}: {ex.Message}");
                    return false;
                }
            }

            return false;
        }

        public static string ReadStringProperty(object target, string name, string logPrefix)
        {
            return ReadProperty(target, name, logPrefix)?.ToString() ?? string.Empty;
        }

        public static bool ReadBoolProperty(object target, string name, string logPrefix)
        {
            var value = ReadProperty(target, name, logPrefix);
            return value is bool b && b;
        }

        public static float ReadFloatProperty(object target, string name, string logPrefix)
        {
            var value = ReadProperty(target, name, logPrefix);
            if (value == null)
            {
                return 0f;
            }

            if (value is float f) return f;
            if (value is double d) return (float)d;
            if (value is decimal m) return (float)m;
            if (value is int i) return i;
            if (value is long l) return l;
            if (value is short s) return s;
            if (value is byte b) return b;
            if (value is string str && float.TryParse(str, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }

            return 0f;
        }
    }
}
