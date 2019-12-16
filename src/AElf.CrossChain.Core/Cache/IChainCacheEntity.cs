using System.Collections.Concurrent;
using System.Linq;

namespace AElf.CrossChain.Cache
{
    public interface IChainCacheEntity
    {
        bool TryAdd(IBlockCacheEntity blockCacheEntity);
        long TargetChainHeight();
        bool TryTake(long height, out IBlockCacheEntity blockCacheEntity, bool isCacheSizeLimited);
        void ClearOutOfDateCacheByHeight(long height);
    }
    
    public class ChainCacheEntity : IChainCacheEntity
    {
        private BlockingCollection<IBlockCacheEntity> BlockCacheEntities { get; } =
            new BlockingCollection<IBlockCacheEntity>(new ConcurrentQueue<IBlockCacheEntity>());

        private BlockingCollection<IBlockCacheEntity> DequeuedBlockCacheEntities { get; } =
            new BlockingCollection<IBlockCacheEntity>(new ConcurrentQueue<IBlockCacheEntity>());
        
        private long _targetHeight;
        private readonly int _chainId;
        
        public ChainCacheEntity(int chainId, long chainHeight)
        {
            _chainId = chainId;
            _targetHeight = chainHeight;
        }

        public long TargetChainHeight()
        {
            if (BlockCacheEntities.Count >= CrossChainConstants.ChainCacheEntityCapacity)
                return -1;
            var lastEnqueuedBlockCacheEntity = BlockCacheEntities.LastOrDefault();
            if (lastEnqueuedBlockCacheEntity != null)
                return lastEnqueuedBlockCacheEntity.Height + 1;
            var lastDequeuedBlockCacheEntity = DequeuedBlockCacheEntities.LastOrDefault();
            if (lastDequeuedBlockCacheEntity != null) 
                return lastDequeuedBlockCacheEntity.Height + 1;
            return _targetHeight;
        }
        
        public bool TryAdd(IBlockCacheEntity blockCacheEntity)
        {
            if (BlockCacheEntities.Count >= CrossChainConstants.ChainCacheEntityCapacity)
                return false;
            // thread unsafe in some extreme cases, but it can be covered with caching mechanism.
            if (blockCacheEntity.Height != TargetChainHeight())
                return false;
            var res = ValidateBlockCacheEntity(blockCacheEntity) && BlockCacheEntities.TryAdd(blockCacheEntity);
            if (res)
                _targetHeight = blockCacheEntity.Height + 1;
            return res;
        }
        
        /// <summary>
        /// Try take element from cached queue.
        /// Make sure that more than <see cref="CrossChainConstants.DefaultBlockCacheEntityCount"/> block cache entities are left in <see cref="BlockCacheEntities"/>.
        /// </summary>
        /// <param name="height">Height of block info needed</param>
        /// <param name="blockCacheEntity"></param>
        /// <param name="isCacheSizeLimited">Use <see cref="CrossChainConstants.ChainCacheEntityCapacity"/> as cache count threshold if true.</param>
        /// <returns></returns>
        public bool TryTake(long height, out IBlockCacheEntity blockCacheEntity, bool isCacheSizeLimited)
        {
            // clear outdated data
            var cachedInQueue = DequeueBlockCacheEntitiesBeforeHeight(height);
            // isCacheSizeLimited means minimal caching size limit, so that most nodes have this block.
            var lastQueuedHeight = BlockCacheEntities.LastOrDefault()?.Height ?? 0;
            if (cachedInQueue && !(isCacheSizeLimited && lastQueuedHeight < height + CrossChainConstants.DefaultBlockCacheEntityCount))
            {
                return TryDequeue(out blockCacheEntity);
            }
            
            blockCacheEntity = GetLastDequeuedBlockCacheEntity(height);
            if (blockCacheEntity != null)
                return !isCacheSizeLimited ||
                       BlockCacheEntities.Count + DequeuedBlockCacheEntities.Count(ci => ci.Height >= height) 
                       >= CrossChainConstants.DefaultBlockCacheEntityCount;
            
            return false;
        }

        public void ClearOutOfDateCacheByHeight(long height)
        {
            while (true)
            {
                var front = DequeuedBlockCacheEntities.FirstOrDefault();
                if (front == null || front.Height > height)
                    return;
                DequeuedBlockCacheEntities.Take();
            }
        }

        /// <summary>
        /// Return first element in cached queue.
        /// </summary>
        /// <returns></returns>
        private IBlockCacheEntity GetLastDequeuedBlockCacheEntity(long height)
        {
            return DequeuedBlockCacheEntities.LastOrDefault(c => c.Height == height);
        }
        
        /// <summary>
        /// Cache outdated data. The block with height lower than <paramref name="height"/> is outdated.
        /// </summary>
        /// <param name="height"></param>
        private bool DequeueBlockCacheEntitiesBeforeHeight(long height)
        {
            while (true)
            {
                var blockCacheEntity = BlockCacheEntities.FirstOrDefault();
                if (blockCacheEntity == null || blockCacheEntity.Height > height)
                    return false;
                if (blockCacheEntity.Height == height)
                    return true;
                TryDequeue(out blockCacheEntity);
            }
        }
        
        /// <summary>
        /// Cache block info lately removed.
        /// Dequeue one element if the cached count reaches <see cref="CrossChainConstants.ChainCacheEntityCapacity"/>
        /// </summary>
        /// <param name="blockCacheEntity"></param>
        private bool TryDequeue(out IBlockCacheEntity blockCacheEntity)
        {
            var res = BlockCacheEntities.TryTake(out blockCacheEntity,
                CrossChainConstants.WaitingIntervalInMillisecond);
            if (res)
                DequeuedBlockCacheEntities.Add(blockCacheEntity);

            return res;
        }

        private bool ValidateBlockCacheEntity(IBlockCacheEntity blockCacheEntity)
        {
            return blockCacheEntity.Height >= Constants.GenesisBlockHeight && blockCacheEntity.ChainId == _chainId &&
                   blockCacheEntity.TransactionStatusMerkleTreeRoot != null;
        }
    }
}