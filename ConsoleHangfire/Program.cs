using ConsoleSample;
using Hangfire;
using Hangfire.States;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleHangfire
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            // Microsoft.AspNetCore.Hosting.Internal.WebHost
            var host = new WebHostBuilder()
                   .UseKestrel()
                   //.UseContentRoot(Directory.GetCurrentDirectory())
                   .UseStartup<Startup>()
                   .UseUrls(Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:5000/")
                   .Build();

            ExampleJob.WebHost = host;
            var runTask = host.RunAsync();
     
                var taskIds = Enumerable.Range(1, 5).Select(x => x.ToString()).ToArray();
                var firstTaskId = taskIds.First();
                var firstJobId = BackgroundJob.Schedule<ExampleJob>(x => x.WriteAsync(firstTaskId, default), TimeSpan.FromMinutes(10));
                var lastid = taskIds.Aggregate(firstTaskId, (pid, newid) =>
                {
                    return BackgroundJob.ContinueJobWith<ExampleJob>(pid, x => x.WriteAsync(pid, default));
                });
                var exitJobId = BackgroundJob.ContinueJobWith<ExampleJob>(lastid, x => x.Complete());

            await runTask;

            var backgroundJobs = new BackgroundJobClient();
            backgroundJobs.RetryAttempts = 5;

            NewFeatures.Test(throwException: false);
            NewFeatures.Test(throwException: true);

            var job1 = BackgroundJob.Enqueue<Services>(x => x.WriteIndex(0));
            var job2 = BackgroundJob.ContinueJobWith<Services>(job1, "default", x => x.WriteIndex(default));
            var job3 = BackgroundJob.ContinueJobWith<Services>(job2, "critical", x => x.WriteIndex(default));
            var job4 = BackgroundJob.ContinueJobWith<Services>(job3, "default", x => x.WriteIndex(default));
            var job5 = BackgroundJob.ContinueJobWith<Services>(job4, "critical", x => x.WriteIndex(default));

            RecurringJob.AddOrUpdate("seconds", () => Console.WriteLine("Hello, seconds!"), "*/15 * * * * *");
            RecurringJob.AddOrUpdate("Console.WriteLine", () => Console.WriteLine("Hello, world!"), Cron.Minutely);
            RecurringJob.AddOrUpdate("hourly", () => Console.WriteLine("Hello"), "25 15 * * *");
            RecurringJob.AddOrUpdate("neverfires", () => Console.WriteLine("Can only be triggered"), "0 0 31 2 *");

            RecurringJob.AddOrUpdate("Hawaiian", () => Console.WriteLine("Hawaiian"), "15 08 * * *", new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Hawaiian Standard Time")
            });

            RecurringJob.AddOrUpdate("UTC", "critical", () => Console.WriteLine("UTC"), "15 18 * * *");

            RecurringJob.AddOrUpdate("Russian", () => Console.WriteLine("Russian"), "15 21 * * *", new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Local
            });


            var count = 1;

            while (true)
            {
                var command = Console.ReadLine();

                if (command == null || command.Equals("stop", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (command.StartsWith("add", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var workCount = int.Parse(command.Substring(4));
                        for (var i = 0; i < workCount; i++)
                        {
                            var number = i;
                            BackgroundJob.Enqueue<Services>(x => x.Random(number));
                        }
                        Console.WriteLine("Jobs enqueued.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

                if (command.StartsWith("async", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var workCount = int.Parse(command.Substring(6));
                        for (var i = 0; i < workCount; i++)
                        {
                            BackgroundJob.Enqueue<Services>(x => x.Async(CancellationToken.None));
                        }
                        Console.WriteLine("Jobs enqueued.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

                if (command.StartsWith("static", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var workCount = int.Parse(command.Substring(7));
                        for (var i = 0; i < workCount; i++)
                        {
                            BackgroundJob.Enqueue(() => Console.WriteLine("Hello, {0}!", "world"));
                        }
                        Console.WriteLine("Jobs enqueued.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

                if (command.StartsWith("error", StringComparison.OrdinalIgnoreCase))
                {
                    var workCount = int.Parse(command.Substring(6));
                    for (var i = 0; i < workCount; i++)
                    {
                        BackgroundJob.Enqueue<Services>(x => x.Error());
                    }
                }

                if (command.StartsWith("args", StringComparison.OrdinalIgnoreCase))
                {
                    var workCount = int.Parse(command.Substring(5));
                    for (var i = 0; i < workCount; i++)
                    {
                        BackgroundJob.Enqueue<Services>(x => x.Args(Guid.NewGuid().ToString(), 14442, DateTime.UtcNow));
                    }
                }

                if (command.StartsWith("custom", StringComparison.OrdinalIgnoreCase))
                {
                    var workCount = int.Parse(command.Substring(7));
                    for (var i = 0; i < workCount; i++)
                    {
                        BackgroundJob.Enqueue<Services>(x => x.Custom(
                            new Random().Next(),
                            new[] { "Hello", "world!" },
                            new Services.CustomObject { Id = 123 },
                            DayOfWeek.Friday
                            ));
                    }
                }

                if (command.StartsWith("fullargs", StringComparison.OrdinalIgnoreCase))
                {
                    var workCount = int.Parse(command.Substring(9));
                    for (var i = 0; i < workCount; i++)
                    {
                        BackgroundJob.Enqueue<Services>(x => x.FullArgs(
                            false,
                            123,
                            'c',
                            DayOfWeek.Monday,
                            "hello",
                            new TimeSpan(12, 13, 14),
                            new DateTime(2012, 11, 10),
                            new Services.CustomObject { Id = 123 },
                            new[] { "1", "2", "3" },
                            new[] { 4, 5, 6 },
                            new long[0],
                            null,
                            new List<string> { "7", "8", "9" }));
                    }
                }

                if (command.StartsWith("in", StringComparison.OrdinalIgnoreCase))
                {
                    var seconds = int.Parse(command.Substring(2));
                    var number = count++;
                    BackgroundJob.Schedule<Services>("default", x => x.Random(number), TimeSpan.FromSeconds(seconds));
                }

                if (command.StartsWith("cancelable", StringComparison.OrdinalIgnoreCase))
                {
                    var iterations = int.Parse(command.Substring(11));
                    BackgroundJob.Enqueue<Services>(x => x.Cancelable(iterations, JobCancellationToken.Null));
                }

                if (command.StartsWith("delete", StringComparison.OrdinalIgnoreCase))
                {
                    var workCount = int.Parse(command.Substring(7));
                    var client = new BackgroundJobClient();
                    for (var i = 0; i < workCount; i++)
                    {
                        client.Create<Services>(x => x.EmptyDefault(), new DeletedState(new ExceptionInfo(new OperationCanceledException())));
                    }
                }

                if (command.StartsWith("fast", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var workCount = int.Parse(command.Substring(5));
                        Parallel.For(0, workCount, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
                        {
                            BackgroundJob.Enqueue<Services>(
                                i % 2 == 0 ? "critical" : "default",
                                x => x.EmptyDefault());
                        });
                        Console.WriteLine("Jobs enqueued.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

                if (command.StartsWith("generic", StringComparison.OrdinalIgnoreCase))
                {
                    BackgroundJob.Enqueue<GenericServices<string>>(x => x.Method("hello", 1));
                }

                if (command.StartsWith("continuations", StringComparison.OrdinalIgnoreCase))
                {
                    var value = "Hello, Hangfire continuations!";
                    var lastId = BackgroundJob.Enqueue<Services>(x => x.Write(value[0]));

                    for (var i = 1; i < value.Length; i++)
                    {
                        lastId = BackgroundJob.ContinueJobWith<Services>(lastId, x => x.Write(value[i]));
                    }

                    BackgroundJob.ContinueJobWith<Services>(lastId, x => x.WriteBlankLine());
                }
            }


            Console.WriteLine("Press Enter to exit...");
            Console.ReadLine();
        }
    }
}
