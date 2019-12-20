using System.Collections.Generic;
using AElf.Contracts.MultiToken;
using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.SmartContract.ExecutionPluginForAcs1.FreeFeeTransactions;
using AElf.Kernel.Token;
using AElf.Types;

namespace AElf.Kernel.SmartContract.ExecutionPluginForAcs8.Tests
{
    public class TokenContractChargeFeeStrategy : IChargeFeeStrategy
    {
        private readonly ISmartContractAddressService _smartContractAddressService;

        public TokenContractChargeFeeStrategy(ISmartContractAddressService smartContractAddressService)
        {
            _smartContractAddressService = smartContractAddressService;
        }

        public Address ContractAddress =>
            _smartContractAddressService.GetAddressByContractName(TokenSmartContractAddressNameProvider.Name);

        public string MethodName => string.Empty;

        public bool IsFree(Transaction transaction)
        {
            return new List<string>
            {
                nameof(TokenContractContainer.TokenContractStub.Create),
                nameof(TokenContractContainer.TokenContractStub.Issue),
            }.Contains(transaction.MethodName);
        }
    }
}