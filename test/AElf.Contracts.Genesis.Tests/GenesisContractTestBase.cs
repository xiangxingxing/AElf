using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Acs0;
using Acs3;
using AElf.Contracts.ParliamentAuth;
using AElf.Contracts.TestBase;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.Token;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Threading;
using SampleECKeyPairs = AElf.Contracts.TestKit.SampleECKeyPairs;

namespace AElf.Contracts.Genesis
{
    public class
        AuthorityNotRequiredBasicContractZeroTestBase : TestKit.ContractTestBase<
            AuthorityNotRequiredBasicContractZeroTestModule>
    {
        protected new ISmartContractAddressService ContractAddressService =>
            Application.ServiceProvider.GetRequiredService<ISmartContractAddressService>();

        protected new Address ContractZeroAddress => ContractAddressService.GetZeroSmartContractAddress();

        internal ACS0Container.ACS0Stub DefaultTester =>
            GetTester<ACS0Container.ACS0Stub>(ContractZeroAddress, DefaultSenderKeyPair);

        internal BasicContractZeroContainer.BasicContractZeroStub ZeroTester =>
            GetTester<BasicContractZeroContainer.BasicContractZeroStub>(ContractZeroAddress, DefaultSenderKeyPair);

        
        protected ECKeyPair DefaultSenderKeyPair => SampleECKeyPairs.KeyPairs.First();
        protected Address DefaultSender => Address.FromPublicKey(DefaultSenderKeyPair.PublicKey);
        protected ECKeyPair AnotherUserKeyPair => SampleECKeyPairs.KeyPairs.Last();
        protected Address AnotherUser => Address.FromPublicKey(AnotherUserKeyPair.PublicKey);

        internal ACS0Container.ACS0Stub AnotherTester =>
            GetTester<ACS0Container.ACS0Stub>(ContractZeroAddress, AnotherUserKeyPair);
    }

    public class BasicContractZeroTestBase : ContractTestBase<BasicContractZeroTestAElfModule>
    {
        protected Address ParliamentAddress;
        protected Address BasicContractZeroAddress;
        protected Address TokenContractAddress;

        protected Address SideBasicContractZeroAddress;
        protected Address SideTokenContractAddress;
        protected Address SideParliamentAddress;

        protected long TotalSupply;
        protected long BalanceOfStarter;

        protected ContractTester<BasicContractZeroTestAElfModule> SideChainTester;
        protected ContractTester<BasicContractZeroTestAElfModule> SideChainMiner;

        protected readonly ECKeyPair TesterKeyPair;
        
        protected readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;

        protected ECKeyPair AnotherUserKeyPair => SampleECKeyPairs.KeyPairs.Last();
        protected ECKeyPair CreatorKeyPair => SampleECKeyPairs.KeyPairs[10];

        protected ECKeyPair AnotherMinerKeyPair => SampleECKeyPairs.KeyPairs[2];

        protected Address AnotherMinerAddress => Address.FromPublicKey(AnotherMinerKeyPair.PublicKey);
        
        public BasicContractZeroTestBase()
        {
            TesterKeyPair = Tester.KeyPair;
            AsyncHelper.RunSync(() =>
                Tester.InitialChainAsyncWithAuthAsync(Tester.GetDefaultContractTypes(Tester.GetCallOwnerAddress(),
                    out TotalSupply,
                    out _,
                    out BalanceOfStarter)));
            BasicContractZeroAddress = Tester.GetZeroContractAddress();
            ParliamentAddress = Tester.GetContractAddress(ParliamentAuthSmartContractAddressNameProvider.Name);
            TokenContractAddress = Tester.GetContractAddress(TokenSmartContractAddressNameProvider.Name);
        }

        protected void StartSideChain()
        {
            var chainId = ChainHelper.ConvertBase58ToChainId("Side");
            var mainChainId = Tester.GetChainAsync().Result.Id;
            SideChainTester =
                new ContractTester<BasicContractZeroTestAElfModule>(chainId, CreatorKeyPair);
            AsyncHelper.RunSync(() =>
                SideChainTester.InitialCustomizedChainAsync(chainId,
                    configureSmartContract: SideChainTester.GetSideChainSystemContract(
                        SideChainTester.GetCallOwnerAddress(), mainChainId,"STA",out TotalSupply,
                        SideChainTester.GetCallOwnerAddress())));
            SideBasicContractZeroAddress = SideChainTester.GetZeroContractAddress();
            SideTokenContractAddress = SideChainTester.GetContractAddress(TokenSmartContractAddressNameProvider.Name);
            SideParliamentAddress =
                SideChainTester.GetContractAddress(ParliamentAuthSmartContractAddressNameProvider.Name);

            SideChainMiner = SideChainTester.CreateNewContractTester(SideChainTester.InitialMinerList.First());
        }

        protected async Task<TransactionResult> ApproveWithMinersAsync(
            ContractTester<BasicContractZeroTestAElfModule> tester, Address parliamentContract, Hash proposalId)
        {
            var tester0 = tester.CreateNewContractTester(Tester.InitialMinerList[0]);
            await tester0.ExecuteContractWithMiningAsync(parliamentContract,
                nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.Approve), new ApproveInput
                {
                    ProposalId = proposalId
                });

            var tester1 = tester.CreateNewContractTester(Tester.InitialMinerList[1]);
            var txResult2 = await tester1.ExecuteContractWithMiningAsync(parliamentContract,
                nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.Approve), new ApproveInput
                {
                    ProposalId = proposalId
                });

            return txResult2;
        }

        protected async Task<Hash> CreateProposalAsync(ContractTester<BasicContractZeroTestAElfModule> tester,
            Address parliamentContract, string methodName, IMessage input)
        {
            var basicContract = tester.GetZeroContractAddress();
            var organizationAddress = await GetGenesisAddressAsync(tester, parliamentContract);
            var proposal = await tester.ExecuteContractWithMiningAsync(parliamentContract,
                nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.CreateProposal),
                new CreateProposalInput
                {
                    ContractMethodName = methodName,
                    ExpiredTime = DateTime.UtcNow.AddDays(1).ToTimestamp(),
                    Params = input.ToByteString(),
                    ToAddress = basicContract,
                    OrganizationAddress = organizationAddress
                });
            var proposalId = Hash.Parser.ParseFrom(proposal.ReturnValue);
            return proposalId;
        }

        protected async Task<TransactionResult> ReleaseProposalAsync(
            ContractTester<BasicContractZeroTestAElfModule> tester, Address parliamentContract, Hash proposalId)
        {
            var transactionResult = await tester.ExecuteContractWithMiningAsync(parliamentContract,
                nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.Release), proposalId);
            return transactionResult;
        }

        internal async Task<ReleaseContractInput> ProposeContractAsync(
            ContractTester<BasicContractZeroTestAElfModule> tester, Address parliamentContract, string methodName,
            IMessage input)
        {
            var proposalId = await CreateProposalAsync(tester, parliamentContract, methodName, input);
            await ApproveWithMinersAsync(tester, parliamentContract, proposalId);
            var releaseResult = await ReleaseProposalAsync(tester, parliamentContract, proposalId);
            var proposedContractInputHash = CodeCheckRequired.Parser
                .ParseFrom(releaseResult.Logs.First(l => l.Name.Contains(nameof(CodeCheckRequired))).NonIndexed)
                .ProposedContractInputHash;
            var codeCheckProposalId = ProposalCreated.Parser
                .ParseFrom(releaseResult.Logs.First(l => l.Name.Contains(nameof(ProposalCreated))).NonIndexed)
                .ProposalId;

            return new ReleaseContractInput
            {
                ProposedContractInputHash = proposedContractInputHash,
                ProposalId = codeCheckProposalId
            };
        }

        internal async Task<TransactionResult> ApproveWithKeyPairAsync(
            ContractTester<BasicContractZeroTestAElfModule> tester, Address parliamentContract, Hash proposalId,
            ECKeyPair ecKeyPair)
        {
            var testerWithMiner = tester.CreateNewContractTester(ecKeyPair);
            return await testerWithMiner.ExecuteContractWithMiningAsync(parliamentContract,
                nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.Approve), new ApproveInput
                {
                    ProposalId = proposalId
                });
        }

        internal async Task<Address> DeployAsync(ContractTester<BasicContractZeroTestAElfModule> tester,
            Address parliamentContract, ContractDeploymentInput contractDeploymentInput)
        {
            var proposingTxResult = await tester.ExecuteContractWithMiningAsync(BasicContractZeroAddress,
                nameof(BasicContractZero.ProposeNewContract), contractDeploymentInput);

            var proposalId = ProposalCreated.Parser
                .ParseFrom(proposingTxResult.Logs.First(l => l.Name.Contains(nameof(ProposalCreated))).NonIndexed)
                .ProposalId;
            var proposedContractInputHash = ContractProposed.Parser
                .ParseFrom(proposingTxResult.Logs.First(l => l.Name.Contains(nameof(ContractProposed))).NonIndexed)
                .ProposedContractInputHash;
            await ApproveWithMinersAsync(Tester, ParliamentAddress, proposalId);

            // release contract code and trigger code check proposal
            var releaseApprovedContractTxResult = await tester.ExecuteContractWithMiningAsync(BasicContractZeroAddress,
                nameof(BasicContractZero.ReleaseApprovedContract), new ReleaseContractInput
                {
                    ProposalId = proposalId,
                    ProposedContractInputHash = proposedContractInputHash
                });
            var codeCheckProposalId = ProposalCreated.Parser
                .ParseFrom(releaseApprovedContractTxResult.Logs.First(l => l.Name.Contains(nameof(ProposalCreated)))
                    .NonIndexed).ProposalId;

            await ApproveWithMinersAsync(Tester, ParliamentAddress, codeCheckProposalId);

            // release code check proposal and deployment completes
            var deploymentResult = await tester.ExecuteContractWithMiningAsync(BasicContractZeroAddress,
                nameof(BasicContractZeroContainer.BasicContractZeroStub.ReleaseCodeCheckedContract),
                new ReleaseContractInput
                    {ProposedContractInputHash = proposedContractInputHash, ProposalId = codeCheckProposalId});
            var address = ContractDeployed.Parser
                .ParseFrom(deploymentResult.Logs.First(l => l.Name.Contains(nameof(ContractDeployed))).NonIndexed)
                .Address;
            return address;
        }

        protected async Task<Address> CreateOrganizationAsync(ContractTester<BasicContractZeroTestAElfModule> tester,
            Address parliamentContract)
        {
            var createOrganizationInput = new CreateOrganizationInput
            {
                ReleaseThreshold = 20000 / tester.InitialMinerList.Count
            };
            var transactionResult =
                await tester.ExecuteContractWithMiningAsync(parliamentContract,
                    nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.CreateOrganization),
                    createOrganizationInput);
            return Address.Parser.ParseFrom(transactionResult.ReturnValue);
        }

        protected async Task<Address> GetGenesisAddressAsync(ContractTester<BasicContractZeroTestAElfModule> tester,
            Address parliamentContract)
        {
            var organizationAddress = Address.Parser.ParseFrom(await tester.CallContractMethodAsync(
                parliamentContract,
                nameof(ParliamentAuthContractContainer.ParliamentAuthContractStub.GetDefaultOrganizationAddress),
                new Empty()));
            return organizationAddress;
        }
        
        protected byte[] ReadCode(string path)
        {
            return File.Exists(path) ? File.ReadAllBytes(path) : throw new FileNotFoundException("Contract DLL cannot be found. " + path);
        }
    }
}