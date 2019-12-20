using System.Collections.Generic;
using System.Threading.Tasks;

namespace AElf.Kernel.SmartContract.Application
{
    public interface ICalculateCostStrategy
    {
        Task<long> GetCostAsync(IChainContext context, int cost);
        void AddAlgorithm(BlockIndex blockIndex, IList<ICalculateWay> allWay);
        
        void RemoveForkCache(List<BlockIndex> blockIndexes);
        void SetIrreversedCache(List<BlockIndex> blockIndexes);
    }

    public interface ICalculateTxCostStrategy : ICalculateCostStrategy
    {
    }

    public interface ICalculateCpuCostStrategy : ICalculateCostStrategy
    {
    }

    public interface ICalculateRamCostStrategy : ICalculateCostStrategy
    {
    }

    public interface ICalculateStoCostStrategy : ICalculateCostStrategy
    {
    }

    public interface ICalculateNetCostStrategy : ICalculateCostStrategy
    {
    }
}