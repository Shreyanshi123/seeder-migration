using SearchLego.DataSeeder.Common;
using System;

namespace SearchLego.DataSeeder.Schedular
{
    public class JobSchedule
    {
        public JobSchedule(Type jobType, JobDeatil jobDetail)
        {
            JobType = jobType;
            JobDetail = jobDetail;
        }
        public Type JobType { get; }

        public JobDeatil JobDetail { get; }
    }
}
