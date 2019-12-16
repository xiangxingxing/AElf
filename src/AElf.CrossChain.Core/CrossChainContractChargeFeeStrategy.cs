using System.Linq;
using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.SmartContract.ExecutionPluginForAcs1.FreeFeeTransactions;
using AElf.Types;

namespace AElf.CrossChain
{
    public class CrossChainContractChargeFeeStrategy : IChargeFeeStrategy
    {
        private readonly ISmartContractAddressService _smartContractAddressService;

        public CrossChainContractChargeFeeStrategy(ISmartContractAddressService smartContractAddressService)
        {
            _smartContractAddressService = smartContractAddressService;
        }

        public Address ContractAddress =>
            _smartContractAddressService.GetAddressByContractName(CrossChainSmartContractAddressNameProvider.Name);

        public string MethodName => string.Empty;

        public bool IsFree(Transaction transaction)
        {
            return CrossChainContractPrivilegeMethodNameProvider.PrivilegeMethodNames.Any(methodName =>
                methodName == transaction.MethodName);
        }
    }
}