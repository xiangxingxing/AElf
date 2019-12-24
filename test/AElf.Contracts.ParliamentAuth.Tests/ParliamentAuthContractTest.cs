using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Acs3;
using AElf.Contracts.MultiToken;
using AElf.Contracts.TestKit;
using AElf.Cryptography.ECDSA;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;
using ApproveInput = Acs3.ApproveInput;

namespace AElf.Contracts.ParliamentAuth
{
    public class ParliamentAuthContractTest : ParliamentAuthContractTestBase
    {
        public ParliamentAuthContractTest()
        {
            InitializeContracts();
        }

        [Fact]
        public async Task Get_DefaultOrganizationAddressFailed_Test()
        {
            var transactionResult =
                await ParliamentAuthContractStub.GetDefaultOrganizationAddress.SendWithExceptionAsync(new Empty());
            transactionResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
            transactionResult.TransactionResult.Error.Contains("Not initialized.").ShouldBeTrue();
        }

        [Fact]
        public async Task ParliamentAuthContract_Initialize_Test()
        {
            var result = await ParliamentAuthContractStub.Initialize.SendAsync(
                new InitializeInput {GenesisOwnerReleaseThreshold = 6666});
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
        }

        [Fact]
        public async Task ParliamentAuthContract_InitializeTwice_Test()
        {
            await ParliamentAuthContract_Initialize_Test();

            var result = await ParliamentAuthContractStub.Initialize.SendWithExceptionAsync(
                new InitializeInput {GenesisOwnerReleaseThreshold = 6666});
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
            result.TransactionResult.Error.Contains("Already initialized.").ShouldBeTrue();
        }

        [Fact]
        public async Task Get_Organization_Test()
        {
            var createOrganizationInput = new CreateOrganizationInput
            {
                ReleaseThreshold = 10000 / MinersCount
            };
            var transactionResult =
                await ParliamentAuthContractStub.CreateOrganization.SendAsync(createOrganizationInput);
            var organizationAddress = transactionResult.Output;
            var getOrganization = await ParliamentAuthContractStub.GetOrganization.CallAsync(organizationAddress);


            getOrganization.OrganizationAddress.ShouldBe(organizationAddress);
            getOrganization.ReleaseThreshold.ShouldBe(10000 / MinersCount);
            getOrganization.OrganizationHash.ShouldBe(Hash.FromTwoHashes(
                Hash.FromMessage(ParliamentAuthContractAddress), Hash.FromMessage(createOrganizationInput)));
        }

        [Fact]
        public async Task Get_OrganizationFailed_Test()
        {
            var organization =
                await ParliamentAuthContractStub.GetOrganization.CallAsync(SampleAddress.AddressList[0]);
            organization.ShouldBe(new Organization());
        }

        [Fact]
        public async Task Get_Proposal_Test()
        {
            var organizationAddress = await CreateOrganizationAsync();
            var transferInput = new TransferInput()
            {
                Symbol = "ELF",
                Amount = 100,
                To = Tester,
                Memo = "Transfer"
            };
            var proposalId = await CreateProposalAsync(DefaultSenderKeyPair, organizationAddress);
            var getProposal = await ParliamentAuthContractStub.GetProposal.SendAsync(proposalId);

            getProposal.Output.Proposer.ShouldBe(DefaultSender);
            getProposal.Output.ContractMethodName.ShouldBe(nameof(TokenContractStub.Transfer));
            getProposal.Output.ProposalId.ShouldBe(proposalId);
            getProposal.Output.OrganizationAddress.ShouldBe(organizationAddress);
            getProposal.Output.ToAddress.ShouldBe(TokenContractAddress);
            getProposal.Output.Params.ShouldBe(transferInput.ToByteString());
        }

        [Fact]
        public async Task Get_ProposalFailed_Test()
        {
            var proposalOutput = await ParliamentAuthContractStub.GetProposal.CallAsync(Hash.FromString("Test"));
            proposalOutput.ShouldBe(new ProposalOutput());
        }

        [Fact]
        public async Task Create_OrganizationFailed_Test()
        {
            var createOrganizationInput = new CreateOrganizationInput
            {
                ReleaseThreshold = 0
            };
            {
                var transactionResult =
                    await ParliamentAuthContractStub.CreateOrganization.SendWithExceptionAsync(createOrganizationInput);
                transactionResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
                transactionResult.TransactionResult.Error.Contains("Invalid organization.").ShouldBeTrue();
            }
            {
                createOrganizationInput.ReleaseThreshold = 100000;
                var transactionResult =
                    await ParliamentAuthContractStub.CreateOrganization.SendWithExceptionAsync(createOrganizationInput);
                transactionResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
                transactionResult.TransactionResult.Error.Contains("Invalid organization.").ShouldBeTrue();
            }
        }

        [Fact]
        public async Task Create_ProposalFailed_Test()
        {
            var organizationAddress = await CreateOrganizationAsync();
            var blockTime = BlockTimeProvider.GetBlockTime();
            var createProposalInput = new CreateProposalInput
            {
                ToAddress = SampleAddress.AddressList[0],
                Params = ByteString.CopyFromUtf8("Test"),
                ExpiredTime = blockTime.AddDays(1),
                OrganizationAddress = organizationAddress
            };
            //"Invalid proposal."
            //ContractMethodName is null or white space
            {
                var transactionResult = await ParliamentAuthContractStub.CreateProposal.SendWithExceptionAsync(createProposalInput);
                transactionResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
                transactionResult.TransactionResult.Error.Contains("Invalid proposal.").ShouldBeTrue();
            }
            //ToAddress is null
            {
                createProposalInput.ContractMethodName = "Test";
                createProposalInput.ToAddress = null;

                var transactionResult = await ParliamentAuthContractStub.CreateProposal.SendWithExceptionAsync(createProposalInput);
                transactionResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
                transactionResult.TransactionResult.Error.Contains("Invalid proposal.").ShouldBeTrue();
            }
            //ExpiredTime is null
            {
                createProposalInput.ExpiredTime = null;
                createProposalInput.ToAddress = SampleAddress.AddressList[0];

                var transactionResult = await ParliamentAuthContractStub.CreateProposal.SendWithExceptionAsync(createProposalInput);
                transactionResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
                transactionResult.TransactionResult.Error.Contains("Invalid proposal.").ShouldBeTrue();
            }
            //"Expired proposal."
            {
                createProposalInput.ExpiredTime = blockTime.AddMilliseconds(5);
                Thread.Sleep(10);

                var transactionResult = await ParliamentAuthContractStub.CreateProposal.SendWithExceptionAsync(createProposalInput);
                transactionResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
            }
            //"No registered organization."
            {
                createProposalInput.ExpiredTime = BlockTimeProvider.GetBlockTime().AddDays(1);
                createProposalInput.OrganizationAddress = SampleAddress.AddressList[1];

                var transactionResult = await ParliamentAuthContractStub.CreateProposal.SendWithExceptionAsync(createProposalInput);
                transactionResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
                transactionResult.TransactionResult.Error.Contains("No registered organization.").ShouldBeTrue();
            }
            //"Proposal with same input."
            {
                createProposalInput.OrganizationAddress = organizationAddress;
                var transactionResult1 = await ParliamentAuthContractStub.CreateProposal.SendAsync(createProposalInput);
                transactionResult1.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

                var transactionResult2 = await ParliamentAuthContractStub.CreateProposal.SendAsync(createProposalInput);
                transactionResult2.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            }
        }

        [Fact]
        public async Task Approve_Proposal_NotFoundProposal_Test()
        {
            var transactionResult = await ParliamentAuthContractStub.Approve.SendWithExceptionAsync(new ApproveInput
            {
                ProposalId = Hash.FromString("Test")
            });
            transactionResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
        }

        [Fact]
        public async Task Approve_Proposal_NotAuthorizedApproval_Test()
        {
            var organizationAddress = await CreateOrganizationAsync();
            var proposalId = await CreateProposalAsync(DefaultSenderKeyPair, organizationAddress);

            ParliamentAuthContractStub = GetParliamentAuthContractTester(TesterKeyPair);
            var transactionResult = await ParliamentAuthContractStub.Approve.SendWithExceptionAsync(new ApproveInput
            {
                ProposalId = proposalId
            });
            transactionResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
            transactionResult.TransactionResult.Error.Contains("Not authorized approval.").ShouldBeTrue();
        }

        [Fact]
        public async Task Approve_Proposal_ExpiredTime_Test()
        {
            var organizationAddress = await CreateOrganizationAsync();
            var proposalId = await CreateProposalAsync(DefaultSenderKeyPair, organizationAddress);

            ParliamentAuthContractStub = GetParliamentAuthContractTester(InitialMinersKeyPairs[0]);
            BlockTimeProvider.SetBlockTime(BlockTimeProvider.GetBlockTime().AddDays(5));
            var error = await ParliamentAuthContractStub.Approve.CallWithExceptionAsync(new ApproveInput
            {
                ProposalId = proposalId
            });
            error.Value.ShouldContain("Invalid proposal.");
        }

        [Fact]
        public async Task Approve_Proposal_ApprovalAlreadyExists_Test()
        {
            var organizationAddress = await CreateOrganizationAsync();
            var proposalId = await CreateProposalAsync(DefaultSenderKeyPair, organizationAddress);

            ParliamentAuthContractStub = GetParliamentAuthContractTester(InitialMinersKeyPairs[0]);
            var transactionResult1 =
                await ParliamentAuthContractStub.Approve.SendAsync(new ApproveInput {ProposalId = proposalId});
            transactionResult1.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            transactionResult1.Output.Value.ShouldBe(true);

            var transactionResult2 =
                await ParliamentAuthContractStub.Approve.SendWithExceptionAsync(new ApproveInput {ProposalId = proposalId});
            transactionResult2.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
            transactionResult2.TransactionResult.Error.Contains("Already approved").ShouldBeTrue();
        }

        [Fact]
        public async Task Release_NotEnoughWeight_Test()
        {
            var organizationAddress = await CreateOrganizationAsync();
            var proposalId = await CreateProposalAsync(DefaultSenderKeyPair, organizationAddress);
            await TransferToOrganizationAddressAsync(organizationAddress);
            await ApproveAsync(InitialMinersKeyPairs[0], proposalId);

            ParliamentAuthContractStub = GetParliamentAuthContractTester(DefaultSenderKeyPair);
            var result = await ParliamentAuthContractStub.Release.SendWithExceptionAsync(proposalId);
            //Reviewer Shares < ReleaseThreshold, release failed
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
            result.TransactionResult.Error.Contains("Not approved.").ShouldBeTrue();
        }

        [Fact]
        public async Task Release_NotFound_Test()
        {
            var proposalId = Hash.FromString("test");
            var result = await ParliamentAuthContractStub.Release.SendWithExceptionAsync(proposalId);
            //Proposal not found
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
            result.TransactionResult.Error.Contains("Proposal not found.").ShouldBeTrue();
        }

        [Fact]
        public async Task Release_WrongSender_Test()
        {
            var organizationAddress = await CreateOrganizationAsync();
            var proposalId = await CreateProposalAsync(DefaultSenderKeyPair, organizationAddress);
            await TransferToOrganizationAddressAsync(organizationAddress);
            await ApproveAsync(InitialMinersKeyPairs[0], proposalId);
            await ApproveAsync(InitialMinersKeyPairs[1], proposalId);

            ParliamentAuthContractStub = GetParliamentAuthContractTester(TesterKeyPair);
            var result = await ParliamentAuthContractStub.Release.SendWithExceptionAsync(proposalId);
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
            result.TransactionResult.Error.Contains("Unable to release this proposal.").ShouldBeTrue();
        }

        [Fact]
        public async Task Release_Proposal_Test()
        {
            var organizationAddress = await CreateOrganizationAsync();
            var proposalId = await CreateProposalAsync(DefaultSenderKeyPair, organizationAddress);
            await TransferToOrganizationAddressAsync(organizationAddress);
            await ApproveAsync(InitialMinersKeyPairs[0], proposalId);
            await ApproveAsync(InitialMinersKeyPairs[1], proposalId);
            
            ParliamentAuthContractStub = GetParliamentAuthContractTester(DefaultSenderKeyPair);
            var result = await ParliamentAuthContractStub.Release.SendAsync(proposalId);
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            //After release,the proposal will be deleted
            //var getProposal = await AssociationAuthContractStub.GetProposal.SendAsync(proposalId.Result);
            //getProposal.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
            //getProposal.TransactionResult.Error.Contains("Not found proposal.").ShouldBeTrue();

            // Check inline transaction result
            var getBalance = TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Symbol = "ELF",
                Owner = Tester
            }).Result.Balance;
            getBalance.ShouldBe(100);
        }

        [Fact]
        public async Task Release_Proposal_AlreadyReleased_Test()
        {
            var organizationAddress = await CreateOrganizationAsync();
            var proposalId = await CreateProposalAsync(DefaultSenderKeyPair, organizationAddress);
            await TransferToOrganizationAddressAsync(organizationAddress);
            await ApproveAsync(InitialMinersKeyPairs[0], proposalId);
            await ApproveAsync(InitialMinersKeyPairs[1], proposalId);

            ParliamentAuthContractStub = GetParliamentAuthContractTester(DefaultSenderKeyPair);
            var txResult1 = await ParliamentAuthContractStub.Release.SendAsync(proposalId);
            txResult1.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            ParliamentAuthContractStub = GetParliamentAuthContractTester(InitialMinersKeyPairs[2]);
            var transactionResult2 =
                await ParliamentAuthContractStub.Approve.SendWithExceptionAsync(new ApproveInput {ProposalId = proposalId});
            transactionResult2.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
            transactionResult2.TransactionResult.Error.Contains("Proposal not found.").ShouldBeTrue();

            ParliamentAuthContractStub = GetParliamentAuthContractTester(DefaultSenderKeyPair);
            var transactionResult3 =
                await ParliamentAuthContractStub.Release.SendWithExceptionAsync(proposalId);
            transactionResult3.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
            transactionResult3.TransactionResult.Error.Contains("Proposal not found.").ShouldBeTrue();
        }

        [Fact]
        public async Task Change_GenesisContractOwner_Test()
        {
            var callResult = await ParliamentAuthContractStub.GetDefaultOrganizationAddress.CallWithExceptionAsync(new Empty());
            callResult.Value.ShouldContain("Not initialized.");
            
            var initializeParliament = await ParliamentAuthContractStub.Initialize.SendAsync(new InitializeInput
            {
                GenesisOwnerReleaseThreshold = 1,
                ProposerAuthorityRequired = false
            });
            initializeParliament.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var contractOwner = await ParliamentAuthContractStub.GetDefaultOrganizationAddress.CallAsync(new Empty());
            contractOwner.ShouldNotBe(new Address());
            
            //no permission
            var transactionResult = await BasicContractStub.ChangeGenesisOwner.SendWithExceptionAsync(Tester);
            transactionResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
            transactionResult.TransactionResult.Error.ShouldContain("Unauthorized behavior");
        }

        [Fact]
        public async Task Check_ValidProposal_Test()
        {
            var organizationAddress = await CreateOrganizationAsync();
            var proposalId = await CreateProposalAsync(DefaultSenderKeyPair, organizationAddress);
            await TransferToOrganizationAddressAsync(organizationAddress);
            
            //Get valid Proposal
            GetParliamentAuthContractTester(InitialMinersKeyPairs.Last());
            var validProposals = await ParliamentAuthContractStub.GetValidProposals.CallAsync(new ProposalIdList
            {
                ProposalIds = {proposalId}
            });
            validProposals.ProposalIds.Count.ShouldBe(1);
            
            await ApproveAsync(InitialMinersKeyPairs[0], proposalId);
            validProposals = await ParliamentAuthContractStub.GetValidProposals.CallAsync(new ProposalIdList
            {
                ProposalIds = {proposalId}
            });
            validProposals.ProposalIds.Count.ShouldBe(0);
        }

        private async Task<Hash> CreateProposalAsync(ECKeyPair proposalKeyPair, Address organizationAddress)
        {
            var transferInput = new TransferInput()
            {
                Symbol = "ELF",
                Amount = 100,
                To = Tester,
                Memo = "Transfer"
            };
            var createProposalInput = new CreateProposalInput
            {
                ContractMethodName = nameof(TokenContractStub.Transfer),
                ToAddress = TokenContractAddress,
                Params = transferInput.ToByteString(),
                ExpiredTime = BlockTimeProvider.GetBlockTime().AddDays(2),
                OrganizationAddress = organizationAddress
            };
            ParliamentAuthContractStub = GetParliamentAuthContractTester(proposalKeyPair);
            var proposal = await ParliamentAuthContractStub.CreateProposal.SendAsync(createProposalInput);
            proposal.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var proposalCreated = ProposalCreated.Parser.ParseFrom(proposal.TransactionResult.Logs[0].NonIndexed).ProposalId;
            proposal.Output.ShouldBe(proposalCreated);
            
            return proposal.Output;
        }

        private async Task<Address> CreateOrganizationAsync()
        {
            var createOrganizationInput = new CreateOrganizationInput
            {
                ReleaseThreshold = 20000 / MinersCount
            };
            var transactionResult =
                await ParliamentAuthContractStub.CreateOrganization.SendAsync(createOrganizationInput);
            transactionResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            return transactionResult.Output;
        }

        private async Task TransferToOrganizationAddressAsync(Address to)
        {
            await TokenContractStub.Transfer.SendAsync(new TransferInput
            {
                Symbol = "ELF",
                Amount = 200,
                To = to,
                Memo = "transfer organization address"
            });
        }

        private async Task ApproveAsync(ECKeyPair reviewer, Hash proposalId)
        {
            ParliamentAuthContractStub = GetParliamentAuthContractTester(reviewer);
            var transactionResult =
                await ParliamentAuthContractStub.Approve.SendAsync(new ApproveInput {ProposalId = proposalId});
            transactionResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            transactionResult.Output.Value.ShouldBe(true);
        }
    }
}