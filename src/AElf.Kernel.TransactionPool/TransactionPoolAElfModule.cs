using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.SmartContractExecution.Application;
using AElf.Kernel.TransactionPool.Application;
using AElf.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace AElf.Kernel.TransactionPool
{
    [DependsOn(typeof(CoreKernelAElfModule))]
    public class TransactionPoolAElfModule : AElfModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            var services = context.Services;
            services.AddSingleton<ITransactionValidationProvider, BasicTransactionValidationProvider>();
//            services.AddSingleton<ITransactionValidationProvider, TransactionToAddressValidationProvider>();
            services.AddSingleton<ITransactionValidationProvider, TransactionMethodNameValidationProvider>();
//            services.AddSingleton<ITransactionValidationProvider, TransactionFromAddressBalanceValidationProvider>();
            services.AddSingleton<ITransactionReadOnlyExecutionService, TransactionReadOnlyExecutionService>();
            services.AddSingleton<ITransactionSizeFeeUnitPriceProvider, TransactionSizeFeeUnitProvider>();
            services.AddSingleton<IBlockAcceptedLogEventHandler, TransactionSizeFeeUnitPriceUpdatedEventHandler>();

            var configuration = context.Services.GetConfiguration();
            Configure<TransactionOptions>(configuration.GetSection("Transaction"));
        }
    }
}