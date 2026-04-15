using System;
using System.Collections.Generic;

namespace TelemachusTelemetryExtended
{
    internal sealed class VesselsOrbitsTelemetryModule
    {
        private static readonly string[] GlobalVesselCommands = { "extended.vessels.orbits" };

        public IEnumerable<string> GlobalCommands => GlobalVesselCommands;

        public Func<Vessel, string[], object> TryGetGlobalHandler(string api)
        {
            switch (api)
            {
                case "extended.vessels.orbits":
                    return (v, args) => GetVesselsOrbitsPayload();
                default:
                    return null;
            }
        }

        public object GetVesselsOrbitsPayload()
        {
            var items = new List<Dictionary<string, object>>();
            var vessels = FlightGlobals.Vessels;
            if (vessels == null)
            {
                return new Dictionary<string, object>
                {
                    { "available", false },
                    { "ut", Planetarium.GetUniversalTime() },
                    { "vessels", items }
                };
            }

            var target = FlightGlobals.fetch?.VesselTarget as Vessel;
            foreach (var vessel in vessels)
            {
                if (vessel == null || vessel.orbit == null || vessel.mainBody == null)
                {
                    continue;
                }

                var orbit = vessel.orbit;
                var body = vessel.mainBody;

                items.Add(new Dictionary<string, object>
                {
                    { "id", vessel.id.ToString("N") },
                    { "name", vessel.vesselName ?? string.Empty },
                    { "type", vessel.vesselType.ToString() },
                    { "situation", vessel.situation.ToString() },
                    { "isActive", vessel.isActiveVessel },
                    { "isTarget", target != null && target == vessel },
                    { "body", body.bodyName ?? string.Empty },
                    { "bodyRadius", body.Radius },
                    { "latitude", vessel.latitude },
                    { "longitude", vessel.longitude },
                    { "altitude", vessel.altitude },
                    { "orbit", new Dictionary<string, object>
                        {
                            { "sma", orbit.semiMajorAxis },
                            { "eccentricity", orbit.eccentricity },
                            { "inclination", orbit.inclination },
                            { "lan", orbit.LAN },
                            { "argumentOfPeriapsis", orbit.argumentOfPeriapsis },
                            { "trueAnomaly", orbit.trueAnomaly },
                            { "meanAnomalyAtEpoch", orbit.meanAnomalyAtEpoch },
                            { "epoch", orbit.epoch },
                            { "period", orbit.period },
                            { "ApA", orbit.ApA },
                            { "PeA", orbit.PeA }
                        }
                    }
                });
            }

            items.Sort((a, b) =>
            {
                var aBody = a.ContainsKey("body") ? a["body"] as string : string.Empty;
                var bBody = b.ContainsKey("body") ? b["body"] as string : string.Empty;
                var byBody = string.Compare(aBody, bBody, StringComparison.Ordinal);
                if (byBody != 0)
                {
                    return byBody;
                }

                var aName = a.ContainsKey("name") ? a["name"] as string : string.Empty;
                var bName = b.ContainsKey("name") ? b["name"] as string : string.Empty;
                return string.Compare(aName, bName, StringComparison.Ordinal);
            });

            return new Dictionary<string, object>
            {
                { "available", true },
                { "ut", Planetarium.GetUniversalTime() },
                { "vessels", items }
            };
        }
    }
}
