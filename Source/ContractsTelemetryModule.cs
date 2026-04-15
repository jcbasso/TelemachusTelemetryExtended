using System;
using System.Collections.Generic;
using System.Globalization;
using Contracts;

namespace TelemachusTelemetryExtended
{
    internal sealed class ContractsTelemetryModule
    {
        private static readonly string[] FlightContractCommands =
        {
            "extended.contracts.accepted",
            "extended.contracts.byState"
        };

        public IEnumerable<string> FlightCommands => FlightContractCommands;

        public Func<Vessel, string[], object> TryGetFlightHandler(string api)
        {
            switch (api)
            {
                case "extended.contracts.accepted":
                    return (v, args) => GetAcceptedContractsJsonOrByIndex(args);
                case "extended.contracts.byState":
                    return (v, args) => GetContractsByStateJson();
                default:
                    return null;
            }
        }

        public object GetAcceptedContractsJsonOrByIndex(string[] args)
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

        public object GetContractsByStateJson()
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

        private static object GetAcceptedContractsJson()
        {
            var contracts = GetAcceptedContracts();
            var result = new List<object>(contracts.Count);
            for (var i = 0; i < contracts.Count; i++)
            {
                result.Add(ContractToObject(contracts[i]));
            }

            return result;
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
    }
}
