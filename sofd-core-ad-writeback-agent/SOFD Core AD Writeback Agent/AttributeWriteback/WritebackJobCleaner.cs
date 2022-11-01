using System;
using Quartz;
using SOFD_Core;

namespace SOFD
{
    [DisallowConcurrentExecution]
    public class WritebackJobCleaner : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            SOFDOrganizationService.fullSync = true;
        }
    }
}