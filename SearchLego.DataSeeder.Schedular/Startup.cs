using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using SearchLego.DataSeeder.Common;
using System.Collections.Generic;
using System.Linq;

namespace SearchLego.DataSeeder.Schedular
{
    public class Startup
    {
        private readonly IConfiguration _config;
        public Startup(IConfiguration config)
        {
            _config = config;
        }
        public void ConfigureServices(IServiceCollection services)
        {
            // getting job detail from from config.
            var congigDetail = _config.GetSection("jobs").Get<IList<ApplicationConfig>>();
            // Add Quartz services
            services.AddSingleton<IJobFactory, SingletonJobFactory>();
            services.AddSingleton<ISchedulerFactory, StdSchedulerFactory>();
            services.AddLogging();
            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddNLog(@"Config\nlog.config");
            });
            // getting jobs according to data source.
            // This is for Sql Server data source
            var jobs = congigDetail?.Where(w => w.ServerType == ServerType.SQLServer).Select(s => s).ToList().FirstOrDefault();
            services.AddSingleton<IngestDataJob>();
            // Configure multiple job from same data source


            foreach (var job in jobs?.JobDeatil)
            {
                if (job.Enabled)
                {
                    services.AddSingleton(new JobSchedule(
                   jobType: typeof(IngestDataJob),
                   jobDetail: job));
                }
            }

            services.AddHostedService<HostedService>();

        }
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
        }
    }
}
