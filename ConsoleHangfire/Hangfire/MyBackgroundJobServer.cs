using System;
using Hangfire;
using Hangfire.Server;
using System.Linq;
using Hangfire.Common;
using System.Threading.Tasks;
using Hangfire.Annotations;
using System.Collections.Generic;
using System.Threading;
using Hangfire.Client;
using Hangfire.States;
using Hangfire.Logging;

namespace ConsoleHangfire
{
    public class MyBackgroundJobServer : IBackgroundProcessingServer, IDisposable
    {
        private readonly ILog _logger = LogProvider.For<BackgroundJobServer>();

        private readonly BackgroundJobServerOptions _options;

        private readonly BackgroundProcessingServer _processingServer;


        public MyBackgroundJobServer([NotNull] BackgroundJobServerOptions options,IEnumerable<IBackgroundProcessDispatcherBuilder> nlist, [CanBeNull] JobStorage storage = null, [CanBeNull] IEnumerable<IBackgroundProcess> additionalProcesses = null, [CanBeNull] IJobFilterProvider filterProvider = null, [CanBeNull] JobActivator activator = null, [CanBeNull] IBackgroundJobFactory factory = null, [CanBeNull] IBackgroundJobPerformer performer = null, [CanBeNull] IBackgroundJobStateChanger stateChanger = null)
        {
            storage = storage ?? JobStorage.Current;
            if (storage == null)
            {
                throw new ArgumentNullException("storage");
            }
            options = options ?? new BackgroundJobServerOptions();
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            if (additionalProcesses == null)
            {
                additionalProcesses = Enumerable.Empty<IBackgroundProcess>();
            }

            _options = options;
            List<IBackgroundProcessDispatcherBuilder> list = new List<IBackgroundProcessDispatcherBuilder>();
            list.AddRange(GetRequiredProcesses(filterProvider, activator, factory, performer, stateChanger));
            list.AddRange(additionalProcesses.Select((IBackgroundProcess x) => x.UseBackgroundPool(1)));
            list.AddRange(nlist);
            Dictionary<string, object> properties = new Dictionary<string, object>
            {
                { "Queues", options.Queues },
                { "WorkerCount", options.WorkerCount }
            };
            _logger.Info($"Starting Hangfire Server using job storage: '{storage}'");
            storage.WriteOptionsToLog(_logger);
            _logger.Info("Using the following options for Hangfire Server:\r\n" + $"    Worker count: {options.WorkerCount}\r\n" + "    Listening queues: " + string.Join(", ", options.Queues.Select((string x) => "'" + x + "'")) + "\r\n" + $"    Shutdown timeout: {options.ShutdownTimeout}\r\n" + $"    Schedule polling interval: {options.SchedulePollingInterval}");


            _processingServer = new BackgroundProcessingServer(storage, list, properties, GetProcessingServerOptions());
        }

        public void SendStop()
        {
            _logger.Debug("Hangfire Server is stopping...");
            _processingServer.SendStop();
        }

        public void Dispose()
        {
            _processingServer.Dispose();
        }

        public bool WaitForShutdown(TimeSpan timeout)
        {
            return _processingServer.WaitForShutdown(timeout);
        }

        public Task WaitForShutdownAsync(CancellationToken cancellationToken)
        {
            return _processingServer.WaitForShutdownAsync(cancellationToken);
        }

        private IEnumerable<IBackgroundProcessDispatcherBuilder> GetRequiredProcesses([CanBeNull] IJobFilterProvider filterProvider, [CanBeNull] JobActivator activator, [CanBeNull] IBackgroundJobFactory factory, [CanBeNull] IBackgroundJobPerformer performer, [CanBeNull] IBackgroundJobStateChanger stateChanger)
        {
            List<IBackgroundProcessDispatcherBuilder> list = new List<IBackgroundProcessDispatcherBuilder>();
            ITimeZoneResolver timeZoneResolver = _options.TimeZoneResolver ?? new DefaultTimeZoneResolver();
            if (factory == null && performer == null && stateChanger == null)
            {
                filterProvider = filterProvider ?? _options.FilterProvider ?? JobFilterProviders.Providers;
                activator = activator ?? _options.Activator ?? JobActivator.Current;
                factory = new BackgroundJobFactory(filterProvider);
                performer = new BackgroundJobPerformer(filterProvider, activator, _options.TaskScheduler);
                stateChanger = new BackgroundJobStateChanger(filterProvider);
            }
            else
            {
                if (factory == null)
                {
                    throw new ArgumentNullException("factory");
                }

                if (performer == null)
                {
                    throw new ArgumentNullException("performer");
                }

                if (stateChanger == null)
                {
                    throw new ArgumentNullException("stateChanger");
                }
            }

            list.Add(new Worker(_options.Queues, performer, stateChanger).UseBackgroundPool(_options.WorkerCount, _options.WorkerThreadConfigurationAction));
            if (!_options.IsLightweightServer)
            {
                list.Add(new DelayedJobScheduler(_options.SchedulePollingInterval, stateChanger).UseBackgroundPool(1));
                list.Add(new RecurringJobScheduler(factory, _options.SchedulePollingInterval, timeZoneResolver).UseBackgroundPool(1));
            }

            return list;
        }

        private BackgroundProcessingServerOptions GetProcessingServerOptions()
        {
            return new BackgroundProcessingServerOptions
            {
                StopTimeout = _options.StopTimeout,
                ShutdownTimeout = _options.ShutdownTimeout,
                HeartbeatInterval = _options.HeartbeatInterval,
                ServerCheckInterval = (_options.ServerWatchdogOptions?.CheckInterval ?? _options.ServerCheckInterval),
                ServerTimeout = (_options.ServerWatchdogOptions?.ServerTimeout ?? _options.ServerTimeout),
                CancellationCheckInterval = _options.CancellationCheckInterval,
                ServerName = _options.ServerName,
                ExcludeStorageProcesses = _options.IsLightweightServer
            };
        }
    }
}
