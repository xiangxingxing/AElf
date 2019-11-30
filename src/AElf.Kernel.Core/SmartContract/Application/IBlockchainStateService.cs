using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.SmartContract.Domain;
using AElf.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AElf.Kernel.SmartContract.Application
{
    public interface IBlockchainStateService
    {
        Task MergeBlockStateAsync(long lastIrreversibleBlockHeight, Hash lastIrreversibleBlockHash);
        
        Task SetBlockStateSetAsync(BlockStateSet blockStateSet);

        Task RemoveBlockStateSetsAsync(IList<Hash> blockStateHashes);
    }

    public class BlockchainStateService : IBlockchainStateService
    {
        private readonly IBlockchainService _blockchainService;
        private readonly IBlockchainStateManager _blockchainStateManager;
        public ILogger<BlockchainStateService> Logger { get; set; }

        public BlockchainStateService(IBlockchainService blockchainService,
            IBlockchainStateManager blockchainStateManager)
        {
            _blockchainService = blockchainService;
            _blockchainStateManager = blockchainStateManager;
            Logger = NullLogger<BlockchainStateService>.Instance;
        }

        public async Task MergeBlockStateAsync(long lastIrreversibleBlockHeight, Hash lastIrreversibleBlockHash)
        {
            var chainStateInfo = await _blockchainStateManager.GetChainStateInfoAsync();
            var firstHeightToMerge = chainStateInfo.BlockHeight == 0L
                ? Constants.GenesisBlockHeight
                : chainStateInfo.BlockHeight + 1;
            var mergeCount = lastIrreversibleBlockHeight - firstHeightToMerge;
            if (mergeCount < 0)
            {
                Logger.LogWarning(
                    $"Last merge height: {chainStateInfo.BlockHeight}, lib height: {lastIrreversibleBlockHeight}, needn't merge");
                return;
            }

            var blockIndexes = new List<IBlockIndex>();
            if (chainStateInfo.Status == ChainStateMergingStatus.Merged)
            {
                blockIndexes.Add(new BlockIndex(chainStateInfo.MergingBlockHash, -1));
            }

            var reversedBlockIndexes = await _blockchainService.GetReversedBlockIndexes(lastIrreversibleBlockHash, (int) mergeCount);
            reversedBlockIndexes.Reverse();
            
            blockIndexes.AddRange(reversedBlockIndexes);

            blockIndexes.Add(new BlockIndex(lastIrreversibleBlockHash, lastIrreversibleBlockHeight));

            Logger.LogTrace(
                $"Start merge lib height: {lastIrreversibleBlockHeight}, lib block hash: {lastIrreversibleBlockHash}, merge count: {blockIndexes.Count}");

            foreach (var blockIndex in blockIndexes)
            {
                try
                {
                    Logger.LogDebug($"Merging state {chainStateInfo} for block {blockIndex}");
                    await _blockchainStateManager.MergeBlockStateAsync(chainStateInfo, blockIndex.BlockHash);
                }
                catch (Exception e)
                {
                    Logger.LogError(e,
                        $"Exception while merge state {chainStateInfo} for block {blockIndex}");
                    throw;
                }
            }
        }

        public async Task SetBlockStateSetAsync(BlockStateSet blockStateSet)
        {
            await _blockchainStateManager.SetBlockStateSetAsync(blockStateSet);
        }
        
        public async Task RemoveBlockStateSetsAsync(IList<Hash> blockStateHashes)
        {
            await _blockchainStateManager.RemoveBlockStateSetsAsync(blockStateHashes);
        }
    }
}