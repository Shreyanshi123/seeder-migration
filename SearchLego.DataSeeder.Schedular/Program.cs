//using Microsoft.AspNetCore;
//using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SearchLego.DataSeeder.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SearchLego.DataSeeder.Schedular
{
    class Program
    {

        public static void Main(string[] args)
        {
            var pathToExe = Process.GetCurrentProcess().MainModule.FileName;
            var pathToContentRoot = Path.GetDirectoryName(pathToExe);
            Directory.SetCurrentDirectory(pathToContentRoot);
            //CreateWebHostBuilder(args).Build().Run();
            try
            {
                // Run with console or service
                var asService = !(Debugger.IsAttached || args.Contains("--console"));

                var configBuilder = new ConfigurationBuilder()
                       .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                       .AddJsonFile(@"Config\appsettings.json", optional: true, reloadOnChange: true)
                       .AddJsonFile(@"Config\cloudsetting.json", optional: true, reloadOnChange: true);

                IConfigurationRoot configuration = configBuilder.Build();

                var builder = new HostBuilder().ConfigureAppConfiguration(config =>
                {
                    config.AddJsonFile(@"Config\appsettings.json", optional: false, reloadOnChange: false);

                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddCustomServices(configuration);


                    var congigDetail = configuration.GetSection("jobs").Get<IList<ApplicationConfig>>();

                    var jobs = congigDetail?.Select(s => s).ToList().FirstOrDefault();
                    foreach (var job in jobs?.JobDeatil)
                    {
                        if (job.Enabled)
                        {
                            switch (job.JobType)
                            {
                                case JobType.DataIngest:
                                    {
                                        services.AddSingleton(new JobSchedule(
                                        jobType: typeof(IngestDataJob),
                                        jobDetail: job));
                                    }
                                    break;

                                case JobType.PDFGenerate:
                                    {
                                        services.AddSingleton(new JobSchedule(
                                        jobType: typeof(PDFGenerateJob),
                                        jobDetail: job));
                                    }
                                    break;

                                case JobType.CognitiveDataIngestion:
                                    {
                                        services.AddSingleton(new JobSchedule(
                                        jobType: typeof(CognitiveDataIngestionJob),
                                        jobDetail: job));
                                    }
                                    break;

                                case JobType.CleanUpRepository:
                                    {
                                        services.AddSingleton(new JobSchedule(
                                        jobType: typeof(CleanUpRepository),
                                        jobDetail: job));
                                    }
                                    break;

                                default:
                                    break;
                            }
                        }
                    }

                    services.AddHostedService<HostedService>();
                });
                builder.UseEnvironment(asService ? Environments.Production : Environments.Development);
                if (asService)
                {
                    builder.RunAsServiceAsync();
                }
                else
                {
                    builder.RunConsoleAsync();
                    Console.ReadLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }
        //public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
        //   WebHost.CreateDefaultBuilder(args).
        //   UseContentRoot(Directory.GetCurrentDirectory())
        //   .ConfigureAppConfiguration((hostingContext, config) =>
        //   {
        //       //config.SetBasePath(Directory.GetCurrentDirectory());
        //       config.AddJsonFile(@"Config\appsettings.json", optional: false, reloadOnChange: false);

        //   })
        //   .UseStartup<Startup>();

    }
}
