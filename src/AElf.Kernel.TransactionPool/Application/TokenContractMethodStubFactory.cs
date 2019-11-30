using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using AElf.CSharp.Core;
using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.Token;
using AElf.Types;
using Google.Protobuf;
using Volo.Abp.DependencyInjection;

namespace AElf.Kernel.TransactionPool.Application
{
    public class TokenContractMethodStubFactory : IMethodStubFactory, ITransientDependency
    {
        private readonly ITransactionReadOnlyExecutionService _transactionReadOnlyExecutionService;
        private readonly ISmartContractAddressService _smartContractAddressService;
        private readonly IChainContext _chainContext;

        private Address FromAddress { get; } = Address.FromBytes(new byte[] { }.ComputeHash());

        private Address TokenContractAddress =>
            _smartContractAddressService.GetAddressByContractName(TokenSmartContractAddressNameProvider.Name);

        public TokenContractMethodStubFactory(ITransactionReadOnlyExecutionService transactionReadOnlyExecutionService,
            ISmartContractAddressService smartContractAddressService, IChainContext chainContext)
        {
            _transactionReadOnlyExecutionService = transactionReadOnlyExecutionService;
            _smartContractAddressService = smartContractAddressService;
            _chainContext = chainContext;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public IMethodStub<TInput, TOutput> Create<TInput, TOutput>(Method<TInput, TOutput> method)
            where TInput : IMessage<TInput>, new() where TOutput : IMessage<TOutput>, new()
        {
            Task<IExecutionResult<TOutput>> SendAsync(TInput input)
            {
                throw new NotSupportedException();
            }

            async Task<TOutput> CallAsync(TInput input)
            {
                var chainContext = _chainContext;
                if (TokenContractAddress == null)
                {
                    // Which means token contract hasn't deployed yet.
                    return default;
                }

                var transaction = new Transaction
                {
                    From = FromAddress,
                    To = TokenContractAddress,
                    MethodName = method.Name,
                    Params = ByteString.CopyFrom(method.RequestMarshaller.Serializer(input))
                };
                try
                {
                    var trace =
                        await _transactionReadOnlyExecutionService.ExecuteAsync(chainContext, transaction,
                            TimestampHelper.GetUtcNow());

                    return trace.IsSuccessful()
                        ? method.ResponseMarshaller.Deserializer(trace.ReturnValue.ToByteArray())
                        : default;
                }
                catch (SmartContractFindRegistrationException)
                {
                    // Which means token contract hasn't deployed yet.
                    return default;
                }
            }

            return new MethodStub<TInput, TOutput>(method, SendAsync, CallAsync);
        }
    }
}