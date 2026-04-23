using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SQLite;
using LinqToDB.Mapping;
using Microsoft.EntityFrameworkCore;

namespace GitCompatibilityTest;

internal static class SqliteFixtureFactory
{
    public static SqliteFixtureInfo Create(string databasePath)
    {
        var absolutePath = Path.GetFullPath(databasePath);
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        using var dbContext = new SqliteBenchmarkDbContext(absolutePath);
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();

        var rows = Enumerable.Range(1, 128)
            .Select(index => new SqliteBenchmarkItem
            {
                Category = index % 2 == 0 ? "git-baseline" : "sqlite-baseline",
                Name = $"item-{index:D3}",
                Quantity = index * 3,
                UpdatedAtUtcTicks = DateTimeOffset.UtcNow.AddMinutes(-index).UtcTicks
            })
            .ToArray();

        dbContext.BenchmarkItems.AddRange(rows);
        dbContext.SaveChanges();

        return new SqliteFixtureInfo
        {
            DatabasePath = absolutePath,
            TableName = "BenchmarkItems",
            RowCount = rows.Length
        };
    }
}

internal static class SqliteValidationService
{
    public static Dictionary<string, string> RunEfSmokeCheck(string databasePath)
    {
        using var dbContext = new SqliteBenchmarkDbContext(databasePath);
        var rowCount = dbContext.BenchmarkItems.Count();
        var latest = dbContext.BenchmarkItems
            .AsNoTracking()
            .OrderByDescending(item => item.UpdatedAtUtcTicks)
            .Select(item => new { item.Name, item.Quantity })
            .First();

        return new Dictionary<string, string>
        {
            ["provider"] = "sqlite-ef-core",
            ["rowCount"] = rowCount.ToString(),
            ["latestName"] = latest.Name,
            ["latestQuantity"] = latest.Quantity.ToString()
        };
    }

    public static Dictionary<string, string> RunLinqToDbSmokeCheck(string databasePath)
    {
        using var db = new SqliteBenchmarkLinqToDbContext(BuildConnectionString(databasePath));
        var table = db.GetTable<SqliteBenchmarkRecord>();
        var rowCount = table.Count();
        var latest = table
            .OrderByDescending(item => item.UpdatedAtUtcTicks)
            .Select(item => new SqliteBenchmarkProjection
            {
                Name = item.Name,
                Quantity = item.Quantity
            })
            .First();

        return new Dictionary<string, string>
        {
            ["provider"] = "sqlite-linq2db",
            ["rowCount"] = rowCount.ToString(),
            ["latestName"] = latest.Name,
            ["latestQuantity"] = latest.Quantity.ToString()
        };
    }

    public static void RunEfBenchmarkQuery(string databasePath)
    {
        using var dbContext = new SqliteBenchmarkDbContext(databasePath);
        var latest = dbContext.BenchmarkItems
            .AsNoTracking()
            .Where(item => item.Quantity > 100)
            .OrderByDescending(item => item.UpdatedAtUtcTicks)
            .Select(item => new { item.Name, item.Quantity })
            .First();

        _ = latest.Name;
    }

    public static void RunLinqToDbBenchmarkQuery(string databasePath)
    {
        using var db = new SqliteBenchmarkLinqToDbContext(BuildConnectionString(databasePath));
        var latest = db.GetTable<SqliteBenchmarkRecord>()
            .Where(item => item.Quantity > 100)
            .OrderByDescending(item => item.UpdatedAtUtcTicks)
            .Select(item => new SqliteBenchmarkProjection
            {
                Name = item.Name,
                Quantity = item.Quantity
            })
            .First();

        _ = latest.Name;
    }

    private static string BuildConnectionString(string databasePath)
    {
        return $"Data Source={databasePath};Mode=ReadWrite;";
    }
}

internal sealed class SqliteBenchmarkDbContext(string databasePath) : DbContext
{
    public DbSet<SqliteBenchmarkItem> BenchmarkItems => Set<SqliteBenchmarkItem>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite($"Data Source={databasePath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SqliteBenchmarkItem>(entity =>
        {
            entity.ToTable("BenchmarkItems");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Category).IsRequired();
            entity.Property(item => item.Name).IsRequired();
        });
    }
}

internal sealed class SqliteBenchmarkItem
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public long UpdatedAtUtcTicks { get; set; }
}

[Table("BenchmarkItems")]
internal sealed class SqliteBenchmarkRecord
{
    [Column, LinqToDB.Mapping.PrimaryKey, Identity]
    public int Id { get; set; }

    [Column, NotNull]
    public string Category { get; set; } = string.Empty;

    [Column, NotNull]
    public string Name { get; set; } = string.Empty;

    [Column, NotNull]
    public int Quantity { get; set; }

    [Column, NotNull]
    public long UpdatedAtUtcTicks { get; set; }
}

internal sealed class SqliteBenchmarkProjection
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

internal sealed class SqliteBenchmarkLinqToDbContext(string connectionString)
    : DataConnection(new DataOptions().UseConnectionString(SQLiteTools.GetDataProvider(), connectionString));
