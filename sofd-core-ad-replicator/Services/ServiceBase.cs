﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using sofd_core_ad_replicator.Config;
using System;

namespace sofd_core_ad_replicator.Services
{
    public abstract class ServiceBase<T>
    {
        protected readonly ILogger<T> logger;
        protected readonly Settings settings;

        public ServiceBase(IServiceProvider sp)
        {
            logger = sp.GetService<ILogger<T>>();
            settings = sp.GetService<Settings>();
        }
    }
}
