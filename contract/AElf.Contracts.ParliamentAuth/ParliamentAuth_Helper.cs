using System.Collections.Generic;
using System.Linq;
using Acs3;
using AElf.Types;
using AElf.Sdk.CSharp;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.ParliamentAuth
{
    public partial class ParliamentAuthContract
    {
        private List<Address> GetCurrentMinerList()
        {
            MaybeLoadConsensusContractAddress();
            var miner = State.ConsensusContract.GetCurrentMinerList.Call(new Empty());
            var members = miner.Pubkeys.Select(publicKey =>
                Address.FromPublicKey(publicKey.ToByteArray())).ToList();
            return members;
        }

        private void AssertAuthorizedProposer()
        {
            // It is a valid proposer if
            // authority check is disable,
            // or sender is in proposer white list,
            // or sender is one of miners.

            Assert(CheckProposerAuthorityIfNeeded(Context.Sender), "Not authorized to propose.");
        }

        private bool CheckProposerAuthorityIfNeeded(Address address)
        {
            var result = !State.ProposerAuthorityRequired.Value;
            return !State.ProposerAuthorityRequired.Value || ValidateProposerAuthority(address);
        }

        private bool IsReleaseThresholdReached(ProposalInfo proposal, Organization organization,
            IEnumerable<Address> currentRepresentatives)
        {
            var currentParliament = new HashSet<Address>(currentRepresentatives);
            var approvalsCollectedFromCurrentParliament =
                proposal.ApprovedRepresentatives.Count(a => currentParliament.Contains(a));
            // approved >= (threshold/max) * representativeCount
            return approvalsCollectedFromCurrentParliament * MaxThreshold >=
                   organization.ReleaseThreshold * currentParliament.Count;
        }

        private void MaybeLoadConsensusContractAddress()
        {
            if (State.ConsensusContract.Value != null)
                return;
            State.ConsensusContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.ConsensusContractSystemName);
        }

        private void AssertSenderIsParliamentMember()
        {
            var currentParliament = GetCurrentMinerList();
            Assert(CheckSenderIsParliamentMember(currentParliament), "Not authorized approval.");
        }

        private bool CheckSenderIsParliamentMember(IEnumerable<Address> currentParliament)
        {
            return currentParliament.Any(r => r.Equals(Context.Sender));
        }

        private const int MaxThreshold = 10000;

        private bool Validate(Organization organization)
        {
            return organization.ReleaseThreshold > 0 && organization.ReleaseThreshold <= MaxThreshold;
        }

        private bool Validate(ProposalInfo proposal)
        {
            var validDestinationAddress = proposal.ToAddress != null;
            var validDestinationMethodName = !string.IsNullOrWhiteSpace(proposal.ContractMethodName);
            var validExpiredTime = CheckProposalNotExpired(proposal);
            var hasOrganizationAddress = proposal.OrganizationAddress != null;
            return validDestinationAddress && validDestinationMethodName && validExpiredTime & hasOrganizationAddress;
        }

        private bool CheckProposalNotExpired(ProposalInfo proposal)
        {
            return proposal.ExpiredTime != null && Context.CurrentBlockTime < proposal.ExpiredTime;
        }

        private ProposalInfo GetValidProposal(Hash proposalId)
        {
            var proposal = State.Proposals[proposalId];
            Assert(proposal != null, "Proposal not found.");
            Assert(Validate(proposal), "Invalid proposal.");
            return proposal;
        }

        private void AssertProposalNotYetApprovedBySender(ProposalInfo proposal)
        {
            Assert(!CheckSenderAlreadyApproved(proposal, Context.Sender), "Already approved.");
        }

        private bool CheckSenderAlreadyApproved(ProposalInfo proposal, Address address)
        {
            return proposal.ApprovedRepresentatives.Contains(address);
        }

        private bool ValidateProposerAuthority(Address address)
        {
            return ValidateAddressInWhiteList(address) || ValidateParliamentMemberAuthority(address);
        }

        private bool ValidateAddressInWhiteList(Address address)
        {
            return State.ProposerWhiteList.Value.Proposers.Any(p => p == address);
        }

        private bool ValidateParliamentMemberAuthority(Address address)
        {
            var currentMinerList = GetCurrentMinerList();
            return currentMinerList.Any(m => m == address);
        }

        private void AssertCurrentMiner()
        {
            MaybeLoadConsensusContractAddress();
            var isCurrentMiner = State.ConsensusContract.IsCurrentMiner.Call(Context.Sender).Value;
            Context.LogDebug(() => $"Sender is currentMiner : {isCurrentMiner}.");
            Assert(isCurrentMiner, "No permission.");
        }

        private Hash CreateNewProposal(CreateProposalInput input)
        {
            var proposalId = Hash.FromTwoHashes(Hash.FromTwoHashes(Hash.FromMessage(input), Context.TransactionId),
                Hash.FromRawBytes(Context.CurrentBlockTime.ToByteArray()));
            var proposal = new ProposalInfo
            {
                ContractMethodName = input.ContractMethodName,
                ExpiredTime = input.ExpiredTime,
                Params = input.Params,
                ToAddress = input.ToAddress,
                OrganizationAddress = input.OrganizationAddress,
                ProposalId = proposalId,
                Proposer = Context.Sender
            };
            Assert(Validate(proposal), "Invalid proposal.");
            Assert(State.Proposals[proposalId] == null, "Proposal already exists.");
            State.Proposals[proposalId] = proposal;
            Context.Fire(new ProposalCreated {ProposalId = proposalId});
            return proposalId;
        }
    }
}