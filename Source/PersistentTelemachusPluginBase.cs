using System;
using System.Reflection;
using Telemachus;
using UnityEngine;

namespace TelemachusTelemetryExtended
{
    public abstract class PersistentTelemachusPluginBase : MonoBehaviour, IMinimalTelemachusPlugin, IDeregisterableTelemachusPlugin
    {
        private bool _registered;
        private float _nextRegisterAttemptAt;
        private Action _deregister;
        private bool _isQuitting;

        protected abstract string LogPrefix { get; }
        protected abstract string[] CommandList { get; }
        protected abstract Func<Vessel, string[], object> ResolveHandler(string api);

        public string[] Commands => CommandList;

        public Action Deregister
        {
            private get => _deregister;
            set => _deregister = value;
        }

        protected virtual void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            TryRegisterPlugin();
        }

        private void OnApplicationQuit()
        {
            _isQuitting = true;
        }

        private void OnDestroy()
        {
            OnBeforeDestroy();

            if (_isQuitting)
            {
                _deregister?.Invoke();
            }
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
            return ResolveHandler(api);
        }

        protected virtual void OnBeforeDestroy()
        {
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
    }
}
