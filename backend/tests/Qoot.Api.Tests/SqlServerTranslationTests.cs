using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Qoot.Api.Data;
using Qoot.Api.Infrastructure;
using Qoot.Api.Media;
using Qoot.Api.Services;

namespace Qoot.Api.Tests;

/// <summary>
/// The integration tests run on the in-memory provider, which evaluates LINQ in .NET. These tests run
/// every query the services issue through the real SQL Server provider instead (no server needed: the
/// connection and the commands are intercepted), so a query SQL Server cannot translate fails here.
/// </summary>
public sealed class SqlServerTranslationTests
{
    private sealed class OfflineSqlServer : DbConnectionInterceptor
    {
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) => InterceptionResult.Suppress();

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(InterceptionResult.Suppress());
    }

    /// <summary>Answers every command without a server: aggregates get one row of zero, everything else no rows.</summary>
    private sealed class FakeResults(List<string> log) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            log.Add(command.CommandText);
            return ValueTask.FromResult(InterceptionResult<DbDataReader>.SuppressWithResult(Reader(command.CommandText)));
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            log.Add(command.CommandText);
            return InterceptionResult<DbDataReader>.SuppressWithResult(Reader(command.CommandText));
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            log.Add(command.CommandText);
            return ValueTask.FromResult(InterceptionResult<object>.SuppressWithResult(0));
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            log.Add(command.CommandText);
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
        }

        private static DbDataReader Reader(string sql)
        {
            var table = new DataTable();
            var head = sql.TrimStart();
            if (head.StartsWith("SELECT COUNT(", StringComparison.OrdinalIgnoreCase))
            {
                table.Columns.Add("v", typeof(int));
                table.Rows.Add(0);
            }
            else if (head.StartsWith("SELECT COALESCE(SUM(", StringComparison.OrdinalIgnoreCase))
            {
                table.Columns.Add("v", typeof(long));
                table.Rows.Add(0L);
            }
            else if (head.StartsWith("SELECT CASE", StringComparison.OrdinalIgnoreCase) && head.Contains("EXISTS", StringComparison.OrdinalIgnoreCase))
            {
                table.Columns.Add("v", typeof(bool));
                table.Rows.Add(false);
            }

            return table.CreateDataReader();
        }
    }

    private sealed class FixedUser : ICurrentUser
    {
        public Guid? Id => Guid.Empty;
        public string Email => "test@qoot.test";
        public bool IsAdmin => true;
        public string? IpAddress => "127.0.0.1";
        public string? UserAgent => "tests";
    }

    private static (QootDbContext Db, List<string> Log) Context()
    {
        var log = new List<string>();
        var options = new DbContextOptionsBuilder<QootDbContext>()
            .UseSqlServer("Server=offline;Database=Qoot;Trusted_Connection=True;TrustServerCertificate=True")
            .AddInterceptors(new OfflineSqlServer(), new FakeResults(log))
            .Options;
        return (new QootDbContext(options), log);
    }

    private static async Task Translates(Func<Task> run)
    {
        try
        {
            await run();
        }
        catch (ApiError)
        {
            // Not found etc. on an empty result is fine: the query itself was translated.
        }
        catch (Exception ex) when (ex is not InvalidOperationException || !(ex.Message.Contains("could not be translated", StringComparison.OrdinalIgnoreCase)
                                                                            || ex.Message.Contains("translation of", StringComparison.OrdinalIgnoreCase)))
        {
            // Materialising fake rows can fail after translation succeeded; only translation errors matter here.
        }
    }

    [Fact]
    public async Task Every_service_query_translates_to_sql_server()
    {
        var (db, log) = Context();
        await using var _ = db;
        var clock = TimeProvider.System;
        var user = new FixedUser();
        var version = new ContentVersion(db, clock);
        var audit = new AuditService(db, user, clock);
        var urls = new MediaUrls(Options.Create(new MediaOptions()));
        var news = new NewsService(db, version, audit, user, clock, urls);
        var sections = new SectionService(db, version, audit, user, clock);
        var contacts = new ContactService(db, audit, user, clock, new NotificationQueue());
        var site = new PublicSiteService(db, version, new MemoryCache(new MemoryCacheOptions()), urls, new SeoBuilder(), clock);
        var seo = new PageSeoService(site, news, new SeoBuilder(), db, clock);

        foreach (var status in new[] { null, "draft", "published", "scheduled", "featured" })
        {
            foreach (var sort in new[] { null, "oldest", "title", "updated", "priority" })
            {
                await Translates(() => news.ListAsync(new NewsQuery("bread", status, 1, sort, 1, 20), CancellationToken.None));
            }
        }

        await Translates(() => news.PublicListAsync("ar", "contracts", 1, 9, CancellationToken.None));
        await Translates(() => news.PublicListAsync("en", null, 2, 9, CancellationToken.None));
        await Translates(() => news.PublicGetAsync("harvest", "en", CancellationToken.None));
        await Translates(() => news.PublicCategoriesAsync("ar", CancellationToken.None));
        await Translates(() => news.CategoriesAsync(CancellationToken.None));
        await Translates(() => news.PreviewAsync(1, "en", CancellationToken.None));
        await Translates(() => sections.ListAsync(CancellationToken.None));
        await Translates(() => contacts.ListAsync(new SubmissionQuery("unread", "camp", 1, 25), CancellationToken.None));
        await Translates(() => contacts.ListAsync(new SubmissionQuery("open", null, 1, 25), CancellationToken.None));
        await Translates(() => new DashboardService(db, sections, contacts, user, clock).GetAsync(CancellationToken.None));
        await Translates(() => new DashboardService(db, sections, contacts, user, clock).AuditAsync(new AuditQuery("x", "section", 1, 50), CancellationToken.None));
        await Translates(() => site.GetAsync("en", preview: false, CancellationToken.None));
        await Translates(() => seo.SitemapAsync(CancellationToken.None));

        var storage = new NullStorage();
        var media = new MediaService(db, storage, Options.Create(new MediaOptions()), urls, version, audit, user, clock);
        await Translates(() => media.ListAsync(new MediaQuery("bags", "upload", "photo", 1, 24), CancellationToken.None));
        await Translates(() => media.UsageAsync(5, CancellationToken.None));

        log.Count.ShouldBeGreaterThan(40);
        log.ShouldContain(sql => sql.Contains("ORDER BY [n].[Priority] DESC", StringComparison.Ordinal));
        log.ShouldContain(sql => sql.Contains("OFFSET", StringComparison.Ordinal) && sql.Contains("FETCH NEXT", StringComparison.Ordinal));
    }

    private sealed class NullStorage : IMediaStorage
    {
        public string RootPath => Path.GetTempPath();

        public Task<string> SaveAsync(byte[] content, string extension, CancellationToken ct) => Task.FromResult("x" + extension);

        public void Delete(string relativePath)
        {
        }
    }
}
