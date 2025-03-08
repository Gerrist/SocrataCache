using Microsoft.EntityFrameworkCore;
using SocrataCache.Config;
using SocrataCache.Managers.Models;

namespace SocrataCache.Managers;

public interface IManagerContext
{
    public DbSet<DatasetModel> Datasets { get; set; }
}

public class ManagerContext : DbContext, IManagerContext
{
    public DbSet<DatasetModel> Datasets { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={Env.DbFilePath.Value}");
    }
}