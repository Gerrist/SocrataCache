using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Quartz.Spi;

namespace SocrataCache.Jobs;

public class JobFactory : IJobFactory
{
    private readonly IServiceProvider _serviceProvider;

    public JobFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        var job = _serviceProvider.GetRequiredService(bundle.JobDetail.JobType) as IJob;
        if (job == null)
        {
            throw new InvalidOperationException($"Failed to create job of type {bundle.JobDetail.JobType}");
        }
        return job;
    }

    public void ReturnJob(IJob job) { }
}
