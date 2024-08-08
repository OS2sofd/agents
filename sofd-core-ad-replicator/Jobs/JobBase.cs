using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using sofd_core_ad_replicator.Config;
using System;
using System.Threading.Tasks;

namespace sofd_core_ad_replicator.Jobs
{
    public abstract class JobBase<T> : IJob
    {
        protected readonly ILogger<T> logger;
        protected readonly Settings settings;

        public JobBase(IServiceProvider sp)
        {
            logger = sp.GetService<ILogger<T>>();
            settings = sp.GetService<Settings>();
        }

        public abstract Task Execute(IJobExecutionContext context);
    }
}
