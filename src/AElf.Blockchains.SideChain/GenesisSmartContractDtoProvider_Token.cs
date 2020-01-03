using Acs0;
using Acs7;
using AElf.Contracts.MultiToken;
using AElf.OS.Node.Application;
using InitializeInput = AElf.Contracts.MultiToken.InitializeInput;

namespace AElf.Blockchains.SideChain
{
    public partial class GenesisSmartContractDtoProvider
    {
        private SystemContractDeploymentInput.Types.SystemTransactionMethodCallList GenerateTokenInitializationCallList(
            ChainInitializationData chainInitializationData)
        {
            var nativeTokenInfo = TokenInfo.Parser.ParseFrom(chainInitializationData.ExtraInformation[1]);
            var resourceTokenList = TokenInfoList.Parser.ParseFrom(chainInitializationData.ExtraInformation[2]);
            var chainPrimaryTokenInfo = TokenInfo.Parser.ParseFrom(chainInitializationData.ExtraInformation[3]);
            var tokenInitializationCallList = new SystemContractDeploymentInput.Types.SystemTransactionMethodCallList();
            tokenInitializationCallList.Add(
                nameof(TokenContractContainer.TokenContractStub.RegisterNativeAndResourceTokenInfo),
                new RegisterNativeAndResourceTokenInfoInput
                {
                    NativeTokenInfo =
                        new RegisterNativeTokenInfoInput
                        {
                            Decimals = nativeTokenInfo.Decimals,
                            IssueChainId = nativeTokenInfo.IssueChainId,
                            Issuer = nativeTokenInfo.Issuer,
                            IsBurnable = nativeTokenInfo.IsBurnable,
                            Symbol = nativeTokenInfo.Symbol,
                            TokenName = nativeTokenInfo.TokenName,
                            TotalSupply = nativeTokenInfo.TotalSupply
                        },
                    ResourceTokenList = resourceTokenList,
                    ChainPrimaryToken = chainPrimaryTokenInfo
                });

            tokenInitializationCallList.Add(nameof(TokenContractContainer.TokenContractStub.Issue), new IssueInput
            {
                Symbol = chainPrimaryTokenInfo.Symbol,
                Amount = chainPrimaryTokenInfo.TotalSupply /
                         SideChainStartupConstants.SideChainPrimaryTokenInitialIssueRatio,
                Memo = "Initial issue",
                To = chainPrimaryTokenInfo.Issuer
            });

            tokenInitializationCallList.Add(nameof(TokenContractContainer.TokenContractStub.Initialize),
                new InitializeInput
                {
                    ResourceAmount =
                    {
                        {nameof(_economicOptions.Cpu).ToUpper(), _economicOptions.Cpu},
                        {nameof(_economicOptions.Ram).ToUpper(), _economicOptions.Ram},
                        {nameof(_economicOptions.Disk).ToUpper(), _economicOptions.Disk},
                    }
                });

            tokenInitializationCallList.Add(nameof(TokenContractContainer.TokenContractStub.SetSideChainCreator),
                chainInitializationData.Creator);

            return tokenInitializationCallList;
        }
    }
}