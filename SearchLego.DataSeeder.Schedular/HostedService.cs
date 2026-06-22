using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Spi;
using SearchLego.DataSeeder.Common;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SearchLego.DataSeeder.Schedular
{
    /// <summary>
    ///  Service Start process
    /// </summary>
    public class HostedService : IHostedService
    {
        private readonly ISchedulerFactory _schedulerFactory;
        private readonly IJobFactory _jobFactory;
        private readonly IEnumerable<JobSchedule> _jobSchedules;
        private readonly ILogger<HostedService> _logger;

        public HostedService(
            ISchedulerFactory schedulerFactory,
            IJobFactory jobFactory,
            IEnumerable<JobSchedule> jobSchedules, ILogger<HostedService> logger)
        {
            _schedulerFactory = schedulerFactory;
            _jobSchedules = jobSchedules;
            _jobFactory = jobFactory;
            _logger = logger;
        }
        public IScheduler Scheduler { get; set; }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Started Jobs");
                Scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
                Scheduler.JobFactory = _jobFactory;
                foreach (var jobSchedule in _jobSchedules)
                {
                    if(jobSchedule?.JobDetail.IndexType != null)
                    _logger.LogInformation($"Starting Job for Id : {jobSchedule?.JobDetail.Id} index '{jobSchedule?.JobDetail.IndexType}' ");
                    else if(jobSchedule?.JobDetail.JobType.ToString() == JobType.CognitiveDataIngestion.ToString())
                        _logger.LogInformation($"Starting Job for Id : {jobSchedule?.JobDetail.Id}, JobType: '{jobSchedule?.JobDetail.JobType.ToString()}', CognitiveProcessType : '{jobSchedule?.JobDetail.CognitiveProcessType.ToString()}' ");
                    else
                        _logger.LogInformation($"Starting Job for Id : {jobSchedule?.JobDetail.Id}, JobType: '{jobSchedule?.JobDetail.JobType.ToString()}'");

                    var job = CreateJob(jobSchedule, jobSchedule?.JobDetail);
                    var trigger = CreateTrigger(jobSchedule, jobSchedule?.JobDetail);
                    await Scheduler.ScheduleJob(job, trigger, cancellationToken);
                }
                await Scheduler.Start(cancellationToken);
                _logger.LogInformation($"All jobs have been started.");
            }
            catch (System.Exception ex)
            {
                _logger.LogError($"Initial_Validation - Exception occurred while initializing the jobs. Error Message:  { ex.Message} ");
            }

        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Scheduler?.Shutdown(cancellationToken);
        }

        private static IJobDetail CreateJob(JobSchedule schedule, JobDeatil jobDeatil)
        {
            var jobType = schedule.JobType;
            return JobBuilder
                .Create(jobType)
                .WithIdentity(jobDeatil.Id)
                .WithDescription(jobDeatil.Id)
                .Build();
        }

        private static ITrigger CreateTrigger(JobSchedule schedule, JobDeatil jobDeatil)
        {
            return TriggerBuilder
                .Create()
                .WithIdentity($"{jobDeatil.Id}.trigger")
                .WithCronSchedule(jobDeatil.CronExpression)
                .WithDescription(jobDeatil.CronExpression)
                .Build();
        }
    }
}
