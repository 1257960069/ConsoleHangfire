using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Hangfire;
using Hangfire.SqlServer;
using Hangfire.Server;
using System.Collections.Concurrent;
using Hangfire.Dashboard;
using System.Linq;
using System.Globalization;
using System.Threading.Channels;
using System.Threading.Tasks;
using Hangfire.Annotations;
using Microsoft.Extensions.Options;
using System.ComponentModel;

namespace ConsoleHangfire
{
    public class ChannelReadeProcess<T> : IBackgroundProcessAsync
    {
        private ChannelReader<T> _channelReader;
        public ChannelReadeProcess(Channel<T> channel)
        {
            _channelReader = channel.Reader;
        }

        public async Task ExecuteAsync([NotNull] BackgroundProcessContext context)
        {
            while (await _channelReader.WaitToReadAsync(context.ShutdownToken))
            {
                if (_channelReader.TryRead(out var item))
                {
                    Console.WriteLine("消费:" + item);
                }
            }
        }
    }
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        public void ConfigureServices(IServiceCollection services)
        {
            services
                .AddSingleton(sp => Channel.CreateUnbounded<string>())
               .AddSingleton<IDashboardAuthorizationFilter, AllowAnonymousAuthorizationFilter>()
               //.AddSingleton<IMonitoringApi>(sp => ((BackgroundJobClient)sp.GetRequiredService<IBackgroundJobClient>()).Storage.GetMonitoringApi())

               .AddTransient<ExampleJob>()
               .AddHangfire((sp, configuration) =>
               {
                   configuration
                   .UseColouredConsoleLogProvider()
                   .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                   .UseSimpleAssemblyNameTypeSerializer()
                   .UseIgnoredAssemblyVersionTypeResolver()
                   .UseRecommendedSerializerSettings()
                   .UseResultsInContinuations()
                   .UseDashboardMetrics(SqlServerStorage.SchemaVersion, SqlServerStorage.ActiveConnections, SqlServerStorage.TotalConnections)
                   //.UseJobDetailsRenderer(10, dto => throw new InvalidOperationException())
                   //.UseJobDetailsRenderer(10, dto => new NonEscapedString("<h4>Hello, world!</h4>"))
                   .UseDefaultCulture(CultureInfo.CurrentCulture)
                   //.UseLog4NetLogProvider()
                   .UseSqlServerStorage("Data Source=(localdb)\\MSSQLLocalDB;Database=AdventureWorks;Integrated Security=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=False;ApplicationIntent=ReadWrite;MultiSubnetFailover=False;", new SqlServerStorageOptions
                   {
                       SchemaName = "gaoke",
                       PrepareSchemaIfNecessary = true,

                       QueuePollInterval = TimeSpan.Zero,
                       SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5.0),
                       JobExpirationCheckInterval = TimeSpan.FromMinutes(30.0),
                       CountersAggregateInterval = TimeSpan.FromMinutes(5.0),
                       DashboardJobListLimit = 10000,
                       TransactionTimeout = TimeSpan.FromMinutes(1.0),
                       DeleteExpiredBatchSize = -1,
                       UseTransactionalAcknowledge = false,
                       UseRecommendedIsolationLevel = true,
                       CommandBatchMaxTimeout = TimeSpan.FromMinutes(5.0),
                       TryAutoDetectSchemaDependentOptions = true,
                       // Migration to Schema 8 is required
                       DisableGlobalLocks = false,
                       //EnableHeavyMigrations = true 
                   })
                   .WithJobExpirationTimeout(TimeSpan.FromDays(7))
                   .UseFilter(new AutomaticRetryAttribute
                   {
                       Attempts = 2,
                       DelayInSecondsByAttemptFunc = attempt => (int)TimeSpan.FromMinutes(Math.Pow(2, attempt)).TotalSeconds
                   })
                   .UseFilter(new SendEmailWhenJobFailedAttribute());

                   //.UseActivator(new ContainerJobActivator(ServiceProvider))
                   //.UseDarkModeSupportForDashboard();                 
               });
            services
                .AddSingleton(typeof(ChannelReadeProcess<>))
                .AddSingleton<IBackgroundProcessAsync>(sp => sp.GetRequiredService<ChannelReadeProcess<string>>());
        }
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseMiddleware<BackgroundJobClientMiddleware>();

            var authorizationFilters = app.ApplicationServices.GetServices<IDashboardAuthorizationFilter>().ToArray();
            app.UseHangfireDashboard(String.Empty, new DashboardOptions()
            {
                AppPath = "https://www.baidu.com",
                DashboardTitle = "Hangfire Dashboard",
                Authorization = authorizationFilters
            });

            var dispatcherBuilders = app.ApplicationServices.GetServices<IBackgroundProcessAsync>().Select(x => x.UseBackgroundPool(1));
            app.UseHangfireServer(() => new MyBackgroundJobServer(new BackgroundJobServerOptions
            {
                WorkerCount = 2,
                Queues = new string[] { "console-hangfire", "default" },
                SchedulePollingInterval = TimeSpan.FromSeconds(15.0),
            }, dispatcherBuilders));
        }
    }
}
