﻿using System.Linq;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.MultiToken
{
    public partial class TokenContract
    {
        [View]
        public override TokenInfo GetTokenInfo(GetTokenInfoInput input)
        {
            return State.TokenInfos[input.Symbol];
        }

        public override TokenInfo GetNativeTokenInfo(Empty input)
        {
            return State.TokenInfos[State.NativeTokenSymbol.Value];
        }

        public override TokenInfoList GetResourceTokenInfo(Empty input)
        {
            return new TokenInfoList
            {
                Value =
                {
                    Context.Variables.SymbolListToPayTxFee.Select(symbol =>
                        State.TokenInfos[symbol] ?? new TokenInfo())
                }
            };
        }

        [View]
        public override GetBalanceOutput GetBalance(GetBalanceInput input)
        {
            return new GetBalanceOutput()
            {
                Symbol = input.Symbol,
                Owner = input.Owner,
                Balance = State.Balances[input.Owner][input.Symbol]
            };
        }

        [View]
        public override GetAllowanceOutput GetAllowance(GetAllowanceInput input)
        {
            return new GetAllowanceOutput()
            {
                Symbol = input.Symbol,
                Owner = input.Owner,
                Spender = input.Spender,
                Allowance = State.Allowances[input.Owner][input.Spender][input.Symbol]
            };
        }

        public override BoolValue IsInWhiteList(IsInWhiteListInput input)
        {
            return new BoolValue {Value = State.LockWhiteLists[input.Symbol][input.Address]};
        }

        public override ProfitReceivingInformation GetProfitReceivingInformation(Address input)
        {
            return State.ProfitReceivingInfos[input] ?? new ProfitReceivingInformation();
        }

        public override GetLockedAmountOutput GetLockedAmount(GetLockedAmountInput input)
        {
            var virtualAddress = GetVirtualAddressForLocking(new GetVirtualAddressForLockingInput
            {
                Address = input.Address,
                LockId = input.LockId
            });
            return new GetLockedAmountOutput
            {
                Symbol = input.Symbol,
                Address = input.Address,
                LockId = input.LockId,
                Amount = State.Balances[virtualAddress][input.Symbol]
            };
        }

        public override Address GetVirtualAddressForLocking(GetVirtualAddressForLockingInput input)
        {
            var fromVirtualAddress = Hash.FromRawBytes(Context.Sender.Value.Concat(input.Address.Value)
                .Concat(input.LockId.Value).ToArray());
            var virtualAddress = Context.ConvertVirtualAddressToContractAddress(fromVirtualAddress);
            return virtualAddress;
        }

        public override Address GetCrossChainTransferTokenContractAddress(
            GetCrossChainTransferTokenContractAddressInput input)
        {
            return State.CrossChainTransferWhiteList[input.ChainId];
        }

        public override StringValue GetPrimaryTokenSymbol(Empty input)
        {
            if (string.IsNullOrWhiteSpace(_primaryTokenSymbol))
            {
                _primaryTokenSymbol = (State.ChainPrimaryTokenSymbol.Value ?? State.NativeTokenSymbol.Value) ??
                                      string.Empty;
            }

            return new StringValue
            {
                Value = _primaryTokenSymbol
            };
        }

        public override CalculateFeeCoefficientsOfType GetCalculateFeeCoefficientOfContract(SInt32Value input)
        {
            return State.CalculateCoefficientOfContract[(FeeTypeEnum) input.Value];
        }

        public override CalculateFeeCoefficientsOfType GetCalculateFeeCoefficientOfSender(Empty input)
        {
            return State.CalculateCoefficientOfSender.Value;
        }

        public override OwningRental GetOwningRental(Empty input)
        {
            var owingRental = new OwningRental();
            foreach (var symbol in Context.Variables.SymbolListToPayRental)
            {
                owingRental.ResourceAmount[symbol] = State.OwningRental[symbol];
            }

            return owingRental;
        }

        public override ResourceUsage GetResourceUsage(Empty input)
        {
            var usage = new ResourceUsage();
            foreach (var symbol in Context.Variables.SymbolListToPayRental)
            {
                usage.Value.Add(symbol, State.ResourceAmount[symbol]);
            }

            return usage;
        }
    }
}