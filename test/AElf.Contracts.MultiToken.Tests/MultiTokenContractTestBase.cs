using System;
using System.Collections.Generic;
using System.Linq;
using Acs2;
using System.Threading.Tasks;
using Acs3;
using Acs7;
using AElf.Contracts.Consensus.AEDPoS;
using AElf.Contracts.CrossChain;
using AElf.Contracts.Profit;
using AElf.Contracts.TestContract.BasicFunction;
using AElf.Contracts.ParliamentAuth;
using AElf.Contracts.TestBase;
using AElf.CrossChain;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Kernel.Token;
using AElf.Sdk.CSharp;
using AElf.Types;
using AElf.Contracts.Treasury;
using AElf.Contracts.TokenConverter;
using AElf.Kernel.Consensus;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Volo.Abp.Threading;
using InitializeInput = AElf.Contracts.CrossChain.InitializeInput;
using SampleAddress = AElf.Contracts.TestKit.SampleAddress;
using SampleECKeyPairs = AElf.Contracts.TestKit.SampleECKeyPairs;

namespace AElf.Contracts.MultiToken
{
    public class MultiTokenContractTestBase : TestKit.ContractTestBase<MultiTokenContractTestAElfModule>
    {
        public byte[] TokenContractCode => Codes.Single(kv => kv.Key.Contains("MultiToken")).Value;
        protected long AliceCoinTotalAmount => 1_000_000_000_0000000L;
        protected long BobCoinTotalAmount => 1_000_000_000_0000L;
        protected Address TokenContractAddress { get; set; }
        internal TokenContractContainer.TokenContractStub TokenContractStub;
        protected ECKeyPair DefaultKeyPair => SampleECKeyPairs.KeyPairs[0];
        protected Address DefaultAddress => Address.FromPublicKey(DefaultKeyPair.PublicKey);
        protected ECKeyPair User1KeyPair { get; } = SampleECKeyPairs.KeyPairs[10];
        protected Address User1Address => Address.FromPublicKey(User1KeyPair.PublicKey);
        protected ECKeyPair User2KeyPair { get; } = SampleECKeyPairs.KeyPairs[11];
        protected ECKeyPair ManagerKeyPair { get; } = SampleECKeyPairs.KeyPairs[12];
        protected Address ManagerAddress => Address.FromPublicKey(ManagerKeyPair.PublicKey);
        protected Address User2Address => Address.FromPublicKey(User2KeyPair.PublicKey);
        protected const string DefaultSymbol = "ELF";
        public byte[] TreasuryContractCode => Codes.Single(kv => kv.Key.Contains("Treasury")).Value;
        protected Address TreasuryContractAddress { get; set; }

        internal TreasuryContractContainer.TreasuryContractStub TreasuryContractStub;
        public byte[] ProfitContractCode => Codes.Single(kv => kv.Key.Contains("Profit")).Value;
        protected Address ProfitContractAddress { get; set; }

        internal ProfitContractContainer.ProfitContractStub ProfitContractStub;
        public byte[] TokenConverterContractCode => Codes.Single(kv => kv.Key.Contains("TokenConverter")).Value;
        protected Address TokenConverterContractAddress { get; set; }

        internal TokenConverterContractContainer.TokenConverterContractStub TokenConverterContractStub;

        internal ACS2BaseContainer.ACS2BaseStub Acs2BaseStub;

        protected Address BasicFunctionContractAddress { get; set; }

        protected Address OtherBasicFunctionContractAddress { get; set; }

        internal BasicFunctionContractContainer.BasicFunctionContractStub BasicFunctionContractStub { get; set; }

        internal BasicFunctionContractContainer.BasicFunctionContractStub OtherBasicFunctionContractStub { get; set; }
        protected byte[] BasicFunctionContractCode => Codes.Single(kv => kv.Key.EndsWith("BasicFunction")).Value;

        protected byte[] OtherBasicFunctionContractCode =>
            Codes.Single(kv => kv.Key.Contains("BasicFunctionWithParallel")).Value;

        protected Hash BasicFunctionContractName => Hash.FromString("AElf.TestContractNames.BasicFunction");
        protected Hash OtherBasicFunctionContractName => Hash.FromString("AElf.TestContractNames.OtherBasicFunction");

        protected readonly Address Address = SampleAddress.AddressList[0];

        protected const string SymbolForTest = "ELF";

        protected const long Amount = 100;

        protected void CheckResult(TransactionResult result)
        {
            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new Exception(result.Error);
            }
        }
    }

    public class
        MultiTokenContractCrossChainTestBase : TestBase.ContractTestBase<MultiTokenContractCrossChainTestAElfModule>
    {
        protected Address BasicContractZeroAddress;
        protected Address CrossChainContractAddress;
        protected Address TokenContractAddress;
        protected Address ParliamentAddress;
        protected Address ConsensusAddress;

        protected Address SideBasicContractZeroAddress;
        protected Address SideCrossChainContractAddress;
        protected Address SideTokenContractAddress;
        protected Address SideParliamentAddress;
        protected Address SideConsensusAddress;

        protected Address Side2BasicContractZeroAddress;
        protected Address Side2CrossChainContractAddress;
        protected Address Side2TokenContractAddress;
        protected Address Side2ParliamentAddress;
        protected Address Side2ConsensusAddress;

        protected long TotalSupply;
        protected long BalanceOfStarter;
        protected Timestamp BlockchainStartTimestamp => TimestampHelper.GetUtcNow();

        protected ContractTester<MultiTokenContractCrossChainTestAElfModule> MainChainTester;
        protected ContractTester<MultiTokenContractCrossChainTestAElfModule> SideChainTester;
        protected ContractTester<MultiTokenContractCrossChainTestAElfModule> SideChain2Tester;

        protected int MainChainId;

        public MultiTokenContractCrossChainTestBase()
        {
            MainChainId = ChainHelper.ConvertBase58ToChainId("AELF");
            MainChainTester =
                new ContractTester<MultiTokenContractCrossChainTestAElfModule>(MainChainId,
                    SampleECKeyPairs.KeyPairs[0]);
            AsyncHelper.RunSync(() =>
                MainChainTester.InitialChainAsyncWithAuthAsync(MainChainTester.GetDefaultContractTypes(
                    MainChainTester.GetCallOwnerAddress(), out TotalSupply,
                    out _,
                    out BalanceOfStarter)));
            BasicContractZeroAddress = MainChainTester.GetZeroContractAddress();
            CrossChainContractAddress =
                MainChainTester.GetContractAddress(CrossChainSmartContractAddressNameProvider.Name);
            TokenContractAddress = MainChainTester.GetContractAddress(TokenSmartContractAddressNameProvider.Name);
            ParliamentAddress = MainChainTester.GetContractAddress(ParliamentAuthSmartContractAddressNameProvider.Name);
            ConsensusAddress = MainChainTester.GetContractAddress(ConsensusSmartContractAddressNameProvider.Name);
        }

        protected void StartSideChain(int chainId, long height, string symbol)
        {
            SideChainTester =
                new ContractTester<MultiTokenContractCrossChainTestAElfModule>(chainId, SampleECKeyPairs.KeyPairs[0]);
            AsyncHelper.RunSync(() =>
                SideChainTester.InitialCustomizedChainAsync(chainId,
                    configureSmartContract: SideChainTester.GetSideChainSystemContract(
                        SideChainTester.GetCallOwnerAddress(), MainChainId, symbol, out TotalSupply,
                        SideChainTester.GetCallOwnerAddress(), height)));
            SideBasicContractZeroAddress = SideChainTester.GetZeroContractAddress();
            SideCrossChainContractAddress =
                SideChainTester.GetContractAddress(CrossChainSmartContractAddressNameProvider.Name);
            SideTokenContractAddress = SideChainTester.GetContractAddress(TokenSmartContractAddressNameProvider.Name);
            SideParliamentAddress =
                SideChainTester.GetContractAddress(ParliamentAuthSmartContractAddressNameProvider.Name);
            SideConsensusAddress = SideChainTester.GetContractAddress(ConsensusSmartContractAddressNameProvider.Name);
        }

        protected void StartSideChain2(int chainId, long height, string symbol)
        {
            SideChain2Tester =
                new ContractTester<MultiTokenContractCrossChainTestAElfModule>(chainId, SampleECKeyPairs.KeyPairs[0]);
            AsyncHelper.RunSync(() =>
                SideChain2Tester.InitialCustomizedChainAsync(chainId,
                    configureSmartContract: SideChain2Tester.GetSideChainSystemContract(
                        SideChain2Tester.GetCallOwnerAddress(), MainChainId, symbol, out TotalSupply,
                        SideChain2Tester.GetCallOwnerAddress(), height)));
            Side2BasicContractZeroAddress = SideChain2Tester.GetZeroContractAddress();
            Side2CrossChainContractAddress =
                SideChain2Tester.GetContractAddress(CrossChainSmartContractAddressNameProvider.Name);
            Side2TokenContractAddress = SideChain2Tester.GetContractAddress(TokenSmartContractAddressNameProvider.Name);
            Side2ParliamentAddress =
                SideChain2Tester.GetContractAddress(ParliamentAuthSmartContractAddressNameProvider.Name);
            Side2ConsensusAddress = SideChain2Tester.GetContractAddress(ConsensusSmartContractAddressNameProvider.Name);
        }

        protected async Task<int> InitAndCreateSideChainAsync(string symbol, long parentChainHeightOfCreation = 0,
            int parentChainId = 0, long lockedTokenAmount = 10)
        {
            await InitializeCrossChainContractAsync(parentChainHeightOfCreation, parentChainId);
            await ApproveBalanceAsync(lockedTokenAmount);
            var proposalId = await CreateSideChainProposalAsync(1, lockedTokenAmount, symbol);
            await ApproveWithMinersAsync(proposalId, ParliamentAddress, MainChainTester);

            var releaseTxResult =
                await MainChainTester.ExecuteContractWithMiningAsync(CrossChainContractAddress,
                    nameof(CrossChainContractContainer.CrossChainContractStub.ReleaseSideChainCreation),
                    new ReleaseSideChainCreationInput {ProposalId = proposalId});
            var sideChainCreatedEvent = SideChainCreatedEvent.Parser
                .ParseFrom(releaseTxResult.Logs.First(l => l.Name.Contains(nameof(SideChainCreatedEvent)))
                    .NonIndexed);
            var chainId = sideChainCreatedEvent.ChainId;

            return chainId;
        }

        protected async Task<Transaction> GenerateTransactionAsync(Address contractAddress, string methodName,
            ECKeyPair ecKeyPair, IMessage input, bool isMainChain)
        {
            if (!isMainChain)
            {
                return ecKeyPair == null
                    ? await SideChainTester.GenerateTransactionAsync(contractAddress, methodName, input)
                    : await SideChainTester.GenerateTransactionAsync(contractAddress, methodName, ecKeyPair, input);
            }

            return ecKeyPair == null
                ? await MainChainTester.GenerateTransactionAsync(contractAddress, methodName, input)
                : await MainChainTester.GenerateTransactionAsync(contractAddress, methodName, ecKeyPair, input);
        }

        internal async Task<CrossChainMerkleProofContext> GetBoundParentChainHeightAndMerklePathByHeight(long height)
        {
            var result = await SideChainTester.ExecuteContractWithMiningAsync(SideCrossChainContractAddress,
                nameof(CrossChainContractContainer.CrossChainContractStub
                    .GetBoundParentChainHeightAndMerklePathByHeight), new SInt64Value
                {
                    Value = height
                });

            var crossChainMerkleProofContext = CrossChainMerkleProofContext.Parser.ParseFrom(result.ReturnValue);
            return crossChainMerkleProofContext;
        }

        internal async Task<long> GetSideChainHeight(int chainId)
        {
            var result = await MainChainTester.CallContractMethodAsync(CrossChainContractAddress,
                nameof(CrossChainContractContainer.CrossChainContractStub
                    .GetSideChainHeight), new SInt32Value
                {
                    Value = chainId
                });

            var height = SInt64Value.Parser.ParseFrom(result);
            return height.Value;
        }

        internal async Task<long> GetParentChainHeight(
            ContractTester<MultiTokenContractCrossChainTestAElfModule> tester, Address sideCrossChainContract)
        {
            var result = await tester.CallContractMethodAsync(sideCrossChainContract,
                nameof(CrossChainContractContainer.CrossChainContractStub
                    .GetParentChainHeight), new Empty());

            var height = SInt64Value.Parser.ParseFrom(result);
            return height.Value;
        }

        private SideChainCreationRequest CreateSideChainCreationRequest(long indexingPrice, long lockedTokenAmount,
            string symbol,
            IEnumerable<ResourceTypeBalancePair> resourceTypeBalancePairs = null)
        {
            var res = new SideChainCreationRequest
            {
                IndexingPrice = indexingPrice,
                LockedTokenAmount = lockedTokenAmount,
                SideChainTokenDecimals = 2,
                IsSideChainTokenBurnable = true,
                SideChainTokenTotalSupply = 1_000_000_000,
                SideChainTokenSymbol = symbol,
                SideChainTokenName = "TEST",
            };
//            if (resourceTypeBalancePairs != null)
//                res.ResourceBalances.AddRange(resourceTypeBalancePairs.Select(x =>
//                    ResourceTypeBalancePair.Parser.ParseFrom(x.ToByteString())));
            return res;
        }

        private async Task<Hash> CreateSideChainProposalAsync(long indexingPrice, long lockedTokenAmount, string symbol)
        {
            var createProposalInput = CreateSideChainCreationRequest(indexingPrice, lockedTokenAmount, symbol);
            var requestSideChainCreationResult =
                await MainChainTester.ExecuteContractWithMiningAsync(CrossChainContractAddress,
                    nameof(CrossChainContractContainer.CrossChainContractStub.RequestSideChainCreation),
                    createProposalInput);

            var proposalId = ProposalCreated.Parser.ParseFrom(requestSideChainCreationResult.Logs
                .First(l => l.Name.Contains(nameof(ProposalCreated))).NonIndexed).ProposalId;
            return proposalId;
        }

        protected async Task ApproveWithMinersAsync(Hash proposalId, Address parliament,
            ContractTester<MultiTokenContractCrossChainTestAElfModule> tester)
        {
            var approveTransaction1 = await tester.GenerateTransactionAsync(parliament,
                nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.Approve),
                tester.InitialMinerList[1],
                new Acs3.ApproveInput
                {
                    ProposalId = proposalId
                });
            var approveTransaction2 = await tester.GenerateTransactionAsync(parliament,
                nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.Approve),
                tester.InitialMinerList[2],
                new Acs3.ApproveInput
                {
                    ProposalId = proposalId
                });
            await tester.MineAsync(new List<Transaction> {approveTransaction1, approveTransaction2});
        }

        protected async Task<TransactionResult> ReleaseProposalAsync(Hash proposalId, Address parliamentAddress,
            ContractTester<MultiTokenContractCrossChainTestAElfModule> tester)
        {
            var transactionResult = await tester.ExecuteContractWithMiningAsync(parliamentAddress,
                nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.Release), proposalId);
            return transactionResult;
        }

        protected async Task<Hash> CreateProposalAsync(
            ContractTester<MultiTokenContractCrossChainTestAElfModule> tester, Address parliamentAddress, string method,
            ByteString input,
            Address contractAddress)
        {
            var organizationAddress = Address.Parser.ParseFrom((await tester.ExecuteContractWithMiningAsync(
                    parliamentAddress,
                    nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.GetDefaultOrganizationAddress),
                    new Empty()))
                .ReturnValue);
            var proposal = await tester.ExecuteContractWithMiningAsync(parliamentAddress,
                nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.CreateProposal),
                new CreateProposalInput
                {
                    ContractMethodName = method,
                    ExpiredTime = TimestampHelper.GetUtcNow().AddDays(1),
                    Params = input,
                    ToAddress = contractAddress,
                    OrganizationAddress = organizationAddress
                });
            var proposalId = Hash.Parser.ParseFrom(proposal.ReturnValue);
            return proposalId;
        }

        protected async Task BootMinerChangeRoundAsync(
            ContractTester<MultiTokenContractCrossChainTestAElfModule> tester, Address consensusAddress,
            bool isMainChain, long nextRoundNumber = 2)
        {
            if (isMainChain)
            {
                var info = await tester.CallContractMethodAsync(consensusAddress,
                    nameof(AEDPoSContractContainer.AEDPoSContractStub.GetCurrentRoundInformation),
                    new Empty());
                var currentRound = Round.Parser.ParseFrom(info);
                var expectedStartTime = TimestampHelper.GetUtcNow();
                currentRound.GenerateNextRoundInformation(expectedStartTime, BlockchainStartTimestamp,
                    out var nextRound);
                nextRound.RealTimeMinersInformation[tester.InitialMinerList[0].PublicKey.ToHex()]
                    .ExpectedMiningTime = expectedStartTime;

                var txResult = await tester.ExecuteContractWithMiningAsync(consensusAddress,
                    nameof(AEDPoSContractContainer.AEDPoSContractStub.NextRound),
                    nextRound);
                txResult.Status.ShouldBe(TransactionResultStatus.Mined);
            }

            if (!isMainChain)
            {
                var info = await tester.CallContractMethodAsync(consensusAddress,
                    nameof(AEDPoSContractContainer.AEDPoSContractStub.GetCurrentRoundInformation),
                    new Empty());
                var currentRound = Round.Parser.ParseFrom(info);
                var expectedStartTime = BlockchainStartTimestamp.ToDateTime()
                    .AddMilliseconds(
                        ((long) currentRound.TotalMilliseconds(4000)).Mul(
                            nextRoundNumber.Sub(1)));
                currentRound.GenerateNextRoundInformation(expectedStartTime.ToTimestamp(), BlockchainStartTimestamp,
                    out var nextRound);

                if (currentRound.RoundNumber >= 3)
                {
                    nextRound.RealTimeMinersInformation[tester.InitialMinerList[0].PublicKey.ToHex()]
                        .ExpectedMiningTime -= new Duration {Seconds = 2400};
                    var res = await tester.ExecuteContractWithMiningAsync(consensusAddress,
                        nameof(AEDPoSContractContainer.AEDPoSContractStub.NextRound),
                        nextRound);
                    res.Status.ShouldBe(TransactionResultStatus.Mined);
                }
                else
                {
                    nextRound.RealTimeMinersInformation[tester.InitialMinerList[0].PublicKey.ToHex()]
                        .ExpectedMiningTime -= new Duration {Seconds = (currentRound.RoundNumber) * 20};

                    var txResult = await tester.ExecuteContractWithMiningAsync(consensusAddress,
                        nameof(AEDPoSContractContainer.AEDPoSContractStub.NextRound),
                        nextRound);
                    txResult.Status.ShouldBe(TransactionResultStatus.Mined);
                }
            }
        }

        private async Task ApproveBalanceAsync(long amount)
        {
            var callOwner = Address.FromPublicKey(MainChainTester.KeyPair.PublicKey);

            var approveResult = await MainChainTester.ExecuteContractWithMiningAsync(TokenContractAddress,
                nameof(TokenContractContainer.TokenContractStub.Approve), new ApproveInput
                {
                    Spender = CrossChainContractAddress,
                    Symbol = "ELF",
                    Amount = amount
                });
            approveResult.Status.ShouldBe(TransactionResultStatus.Mined);
            await MainChainTester.CallContractMethodAsync(TokenContractAddress,
                nameof(TokenContractContainer.TokenContractStub.GetAllowance),
                new GetAllowanceInput
                {
                    Symbol = "ELF",
                    Owner = callOwner,
                    Spender = CrossChainContractAddress
                });
        }

        private async Task InitializeCrossChainContractAsync(long parentChainHeightOfCreation = 0,
            int parentChainId = 0)
        {
            var crossChainInitializationTransaction = await MainChainTester.GenerateTransactionAsync(
                CrossChainContractAddress,
                nameof(CrossChainContractContainer.CrossChainContractStub.Initialize), new CrossChain.InitializeInput
                {
                    ParentChainId = parentChainId == 0 ? ChainHelper.ConvertBase58ToChainId("AELF") : parentChainId,
                    CreationHeightOnParentChain = parentChainHeightOfCreation
                });
            await MainChainTester.MineAsync(new List<Transaction> {crossChainInitializationTransaction});
        }
    }
}