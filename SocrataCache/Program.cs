using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.AspNetCore;
using SocrataCache.Config;
using SocrataCache.Jobs;
using SocrataCache.Managers;

namespace SocrataCache;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<ManagerContext>();

        builder.Services.AddSingleton(new Config.Config(Env.ConfigFilePath.Value));
        builder.Services.AddScoped<Managers.DatasetManager>();

        builder.Services.AddQuartz(q =>
        {
            var freshDatasetLookupJobKey = new JobKey("FreshDatasetLookupJob");
            q.AddJob<FreshDatasetLookupJob>(opts => opts.WithIdentity(freshDatasetLookupJobKey));

            var downloadPendingDatasetsJobKey = new JobKey("DownloadPendingDatasetsJob");
            q.AddJob<DownloadPendingDatasetsJob>(opts => opts.WithIdentity(downloadPendingDatasetsJobKey));

            var retentionCleanupJobKey = new JobKey("RetentionCleanupJob");
            q.AddJob<RetentionCleanupJob>(opts => opts.WithIdentity(retentionCleanupJobKey));

            q.AddTrigger(opts => opts
                .ForJob(freshDatasetLookupJobKey)
                .WithIdentity("FreshDatasetLookupRecurringTrigger")
                .WithSimpleSchedule(x => x.WithIntervalInMinutes(5).RepeatForever()));

            q.AddTrigger(opts => opts
                .ForJob(freshDatasetLookupJobKey)
                .WithIdentity("FreshDatasetLookupImmediateTrigger")
                .StartNow());

            q.AddTrigger(opts => opts
                .ForJob(downloadPendingDatasetsJobKey)
                .WithIdentity("DownloadPendingDatasetsRecurringTrigger")
                .WithSimpleSchedule(x => x.WithIntervalInMinutes(5).RepeatForever()));

            q.AddTrigger(opts => opts
                .ForJob(downloadPendingDatasetsJobKey)
                .WithIdentity("DownloadPendingDatasetsImmediateTrigger")
                .StartAt(DateTime.Now.AddSeconds(20)));

            q.AddTrigger(opts => opts
                .ForJob(retentionCleanupJobKey)
                .WithIdentity("RetentionCleanupJobRecurringTrigger")
                .WithSimpleSchedule(x => x.WithIntervalInMinutes(1).RepeatForever()));

            q.AddTrigger(opts => opts
                .ForJob(retentionCleanupJobKey)
                .WithIdentity("RetentionCleanupJobImmediateTrigger")
                .StartAt(DateTime.Now.AddSeconds(20)));
        });

        builder.Services.AddQuartzServer(q => { q.WaitForJobsToComplete = true; });

        builder.Services.AddOpenApi();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ManagerContext>();
            Console.WriteLine("Applying migrations...");
            await dbContext.Database.MigrateAsync();
            Console.WriteLine("Migrations applied");
        }

        if (app.Environment.IsDevelopment()) app.MapOpenApi();

        app.MapGet("/api/datasets", async (HttpContext httpContext, Managers.DatasetManager datasetManager) =>
            {
                var datasets = await datasetManager.GetDatasets();

                return datasets.Select(dataset => new DatasetDto
                {
                    DatasetId = dataset.DatasetId,
                    ResourceId = dataset.ResourceId,
                    Status = dataset.Status.ToString().ToLower(),
                    ReferenceDate = dataset.ReferenceDate,
                    CreatedAt = dataset.CreatedAt,
                    UpdatedAt = dataset.UpdatedAt
                });
            })
            .WithName("GetDatasets");

        Console.WriteLine("Starting application");

        await app.RunAsync();
    }
}