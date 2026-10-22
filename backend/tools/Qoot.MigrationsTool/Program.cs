// Generates EF Core migrations and the SQL script for SQL Server without a database connection.
// Equivalent to `dotnet ef migrations add` / `dotnet ef migrations script --idempotent`, for
// environments where the dotnet-ef tool is not installed.
//
//   dotnet run --project tools/Qoot.MigrationsTool -- add <Name>
//   dotnet run --project tools/Qoot.MigrationsTool -- script <output.sql>
//   dotnet run --project tools/Qoot.MigrationsTool -- check        (fails if the model has changes without a migration)

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.SqlServer.Design.Internal;
using Microsoft.Extensions.DependencyInjection;
using Qoot.Api.Data;

var command = args.ElementAtOrDefault(0) ?? "check";
var root = FindBackendRoot();
var projectDir = Path.Combine(root, "src", "Qoot.Api");

// The connection string is never opened: it only selects the SQL Server provider.
var options = new DbContextOptionsBuilder<QootDbContext>()
    .UseSqlServer("Server=design-time;Database=Qoot;Trusted_Connection=True;TrustServerCertificate=True")
    .Options;
await using var context = new QootDbContext(options);

switch (command)
{
    case "add":
    {
        var name = args.ElementAtOrDefault(1) ?? throw new ArgumentException("Give the migration a name: add <Name>");
        var services = new ServiceCollection()
            .AddEntityFrameworkDesignTimeServices()
            .AddDbContextDesignTimeServices(context);
#pragma warning disable EF1001 // the provider's design-time services are what `dotnet ef` uses too
        new SqlServerDesignTimeServices().ConfigureDesignTimeServices(services);
#pragma warning restore EF1001
        var scaffolder = services.BuildServiceProvider().GetRequiredService<IMigrationsScaffolder>();
        var migration = scaffolder.ScaffoldMigration(name, "Qoot.Api", "Data.Migrations", "C#");
        var files = scaffolder.Save(projectDir, migration, Path.Combine(projectDir, "Data", "Migrations"));
        Console.WriteLine($"Created {files.MigrationFile}\n        {files.MetadataFile}\nUpdated {files.SnapshotFile}");
        return 0;
    }

    case "script":
    {
        var output = args.ElementAtOrDefault(1) ?? Path.Combine(root, "..", "docs", "database", "qoot-schema.sql");
        var migrator = context.GetService<IMigrator>();
        var sql = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllTextAsync(output, sql);
        Console.WriteLine($"Wrote {Path.GetFullPath(output)} ({sql.Length:N0} characters)");
        return 0;
    }

    case "check":
    {
        var hasChanges = context.Database.HasPendingModelChanges();
        Console.WriteLine(hasChanges ? "The model has changes that no migration covers. Run: add <Name>" : "Migrations match the model.");
        return hasChanges ? 1 : 0;
    }

    default:
        Console.Error.WriteLine("Commands: add <Name> | script [output.sql] | check");
        return 1;
}

static string FindBackendRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Qoot.slnx")))
    {
        dir = dir.Parent;
    }

    return dir?.FullName ?? throw new InvalidOperationException("Run from inside the backend folder.");
}
