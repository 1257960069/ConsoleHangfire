using Hangfire.Annotations;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.Owin;
using System;
using System.Threading.Tasks;

namespace Hangfire.Dashboard
{
    public class BackgroundJobClientMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IBackgroundJobClient _backgroundJobClient;

        public BackgroundJobClientMiddleware(RequestDelegate next, IBackgroundJobClient backgroundJobClient)
        {
            _next = next;
            _backgroundJobClient = backgroundJobClient;
        }
        public Task Invoke(HttpContext context)
        {
            PathString remaining;
            if (context.Request.Path.StartsWithSegments(new PathString("/su/jobs/details"), out remaining) && remaining.HasValue)
            {
                var jobId = remaining.Value.TrimStart('/');
                return context.Response.WriteAsync(ChangeStateToSucceeded(jobId).ToString());
            }
            else if (context.Request.Path.StartsWithSegments(new PathString("/fa/jobs/details"), out remaining) && remaining.HasValue)
            {
                var jobId = remaining.Value.TrimStart('/');
                return context.Response.WriteAsync(ChangeStateToFailed(jobId).ToString());
            }
            else
            {
                return _next.Invoke(context);
            }
        }

        private bool ChangeStateToSucceeded([NotNull] string jobId, string reason = "Force ChangeState to Succeeded")
        {
            return _backgroundJobClient.ChangeState(jobId, new SucceededState(null, 0, 0)
            {
                Reason = reason
            });
        }
        private bool ChangeStateToFailed([NotNull] string jobId, string reason = "Force ChangeState to Failed")
        {
            return _backgroundJobClient.ChangeState(jobId, new FailedState(new Exception(reason))
            {
                Reason = reason
            });
        }
    }
}
