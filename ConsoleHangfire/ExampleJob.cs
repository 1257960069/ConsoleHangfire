using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Runtime.Remoting.Channels;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ConsoleHangfire
{
    [Queue("console-hangfire")]
    public class ExampleJob
    {
        public static IWebHost WebHost { private get; set; }
        private readonly Channel<string> _channel;

        public ExampleJob(Channel<string> channel)
        {
            _channel = channel;
        }
        public async Task WriteAsync(string value, CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] {DateTime.Now} Starting job(value={value})");

            await Task.Delay(TimeSpan.FromSeconds(10));
            await _channel.Writer.WriteAsync(value, cancellationToken);

            Console.WriteLine($"[{Thread.CurrentThread.ManagedThreadId}] {DateTime.Now} End job(value={value})");
        }

        [LatencyTimeout(60)]
        public void Complete()
        {
            _channel.Writer.Complete();
            WebHost.Dispose();
        }
    }
}
