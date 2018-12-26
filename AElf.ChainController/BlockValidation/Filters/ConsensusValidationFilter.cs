using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Common.Attributes;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Common;
using AElf.Configuration;
using AElf.Kernel.Consensus;
using AElf.SmartContract;
using AElf.Types.CSharp;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using NLog;
using ServiceStack;

// ReSharper disable once CheckNamespace
namespace AElf.ChainController
{
    // ReSharper disable InconsistentNaming
    public class ConsensusBlockValidationFilter
    {
        private readonly ISmartContractService _smartContractService;
        private readonly ILogger _logger;
        
        private readonly Address _nodeAddress;
        private readonly ECKeyPair _nodeKeyPair;

        public ConsensusBlockValidationFilter(ISmartContractService smartContractService)
        {
            _smartContractService = smartContractService;
            
            _nodeAddress = Address.Parse(NodeConfig.Instance.NodeAccount);
            _nodeKeyPair = NodeConfig.Instance.ECKeyPair;
            
            _logger = LogManager.GetLogger(nameof(ConsensusBlockValidationFilter));
        }

        public async Task<BlockValidationResult> ValidateBlockAsync(IBlock block, IChainContext context)
        {
            if (NodeConfig.Instance.ConsensusKind == ConsensusKind.AElfDPoS)
            {
                return await DPoSValidation(block, context);
            }

            if (NodeConfig.Instance.ConsensusKind == ConsensusKind.AElfPoW)
            {
                return await PoWValidation(block, context);
            }

            return BlockValidationResult.NotImplementConsensus;
        }

        private async Task<BlockValidationResult> DPoSValidation(IBlock block, IChainContext context)
        {
            // If the height of chain is 1, no need to check consensus validation
            if (block.Header.Index < GlobalConfig.GenesisBlockHeight + 2)
            {
                return BlockValidationResult.Success;
            }

            // Get BP address
            // todo temp solution
            var uncompressedPrivateKey = block.Header.P.ToByteArray();
            var address = Address.FromPublicKey(uncompressedPrivateKey);

            // Get the address of consensus contract
            var contractAccountHash = ContractHelpers.GetConsensusContractAddress(context.ChainId);
            var timestampOfBlock = block.Header.Time;

            long roundId = 1;
            var updateTx =
                block.Body.TransactionList.Where(t => t.MethodName == ConsensusBehavior.NextRound.ToString())
                    .ToList();
            if (updateTx.Count > 0)
            {
                if (updateTx.Count > 1)
                {
                    return BlockValidationResult.IncorrectDPoSTxInBlock;
                }

                roundId = ((Round) ParamsPacker.Unpack(updateTx[0].Params.ToByteArray(),
                    new[] {typeof(Round), typeof(Round), typeof(StringValue)})[1]).RoundId;
            }

            //Formulate an Executive and execute a transaction of checking time slot of this block producer
            TransactionTrace trace;
            var executive = await _smartContractService.GetExecutiveAsync(contractAccountHash, context.ChainId);
            try
            {
                var tx = GetTxToVerifyBlockProducer(contractAccountHash, NodeConfig.Instance.ECKeyPair, address,
                    timestampOfBlock, roundId);
                if (tx == null)
                {
                    return BlockValidationResult.FailedToCheckConsensusInvalidation;
                }

                var tc = new TransactionContext
                {
                    Transaction = tx
                };
                await executive.SetTransactionContext(tc).Apply();
                trace = tc.Trace;
            }
            finally
            {
                _smartContractService.PutExecutiveAsync(contractAccountHash, executive).Wait();
            }
            
            //If failed to execute the transaction of checking time slot
            if (!trace.StdErr.IsNullOrEmpty())
            {
                _logger.Trace("Failed to execute tx Validation: " + trace.StdErr);
                return BlockValidationResult.FailedToCheckConsensusInvalidation;
            }

            var result = Int32Value.Parser.ParseFrom(trace.RetVal.ToByteArray()).Value;

            switch (result)
            {
                case 1:
                    return BlockValidationResult.NotBP;
                case 2:
                    return BlockValidationResult.InvalidTimeSlot;
                case 3:
                    return BlockValidationResult.SameWithCurrentRound;
                case 11:
                    return BlockValidationResult.ParseProblem;
                default:
                    return BlockValidationResult.Success;
            }
        }

        private async Task<BlockValidationResult> PoWValidation(IBlock block, IChainContext context)
        {
            return await Task.FromResult(BlockValidationResult.IncorrectPoWResult);
        }

        private Transaction GetTxToVerifyBlockProducer(Address contractAccountHash, ECKeyPair keyPair,
            Address recipientAddress, Timestamp timestamp, long roundId)
        {
            if (contractAccountHash == null || keyPair == null || recipientAddress == null)
            {
                _logger?.Error("Something wrong happened to consensus verification filter.");
                return null;
            }

            var tx = new Transaction
            {
                From = contractAccountHash,
                To = contractAccountHash,
                IncrementId = 0,
                MethodName = "Validation",
                Params = ByteString.CopyFrom(ParamsPacker.Pack(
                    new StringValue {Value = recipientAddress.GetFormatted()}.ToByteArray(),
                    timestamp.ToByteArray(),
                    new Int64Value {Value = roundId}))
            };

            // todo review
            var signer = new ECSigner();
            var signature = signer.Sign(keyPair, tx.GetHash().DumpByteArray());
            tx.Sigs.Add(ByteString.CopyFrom(signature.SigBytes)); 

            return tx;
        }
    }
}