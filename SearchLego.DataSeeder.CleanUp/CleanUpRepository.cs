using Microsoft.Extensions.Logging;
using Quartz;
using SearchLego.DataSeeder.Cleanup;
using System;
using System.Threading.Tasks;

namespace SearchLego.DataSeeder.Schedular
{
    [DisallowConcurrentExecution]
    public class CleanUpRepository : IJob
    {
        private readonly ILogger<dynamic> _logger;
        private readonly DataCleanUpRepository dataCleanUpRepository;

        public CleanUpRepository(ILogger<dynamic> logger)
        {
            _logger = logger ?? new Logger<dynamic>(new LoggerFactory()); ;
            dataCleanUpRepository = new DataCleanUpRepository();
        }
        public Task Execute(IJobExecutionContext context)
        {
            var jobId = context.JobDetail.Description;
            try
            {
                _logger.LogInformation($"Cleanup job has been started");
                dataCleanUpRepository.CleanDocument(_logger);
                _logger.LogInformation($"Cleanup job has been completed successfully");

            }
            catch (Exception ex)
            {
                _logger.LogError($"Faild : Initial_Validation - job id : {jobId}, Clean Up, {ex.Message}, {ex.StackTrace}");
            }
            return Task.CompletedTask;
        }
    }
}
