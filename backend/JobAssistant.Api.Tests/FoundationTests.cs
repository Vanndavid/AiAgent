using JobAssistant.Api.Data;
using JobAssistant.Api.Models;
using Xunit;

namespace JobAssistant.Api.Tests;

public class MigrationRunnerTests
{
    [Fact]
    public void DiscoverMigrations_orders_by_version()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ja-migrations-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "002_agent_runs.sql"), "-- two");
            File.WriteAllText(Path.Combine(dir, "001_applications.sql"), "-- one");
            File.WriteAllText(Path.Combine(dir, "readme.txt"), "ignore");

            var found = MigrationRunner.DiscoverMigrations(dir);

            Assert.Equal(2, found.Count);
            Assert.Equal("001", found[0].Version);
            Assert.Equal("002", found[1].Version);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

public class ApplicationListQueryParserTests
{
    [Fact]
    public void Parse_defaults()
    {
        var q = ApplicationListQueryParser.Parse(null, null, null, null);
        Assert.Null(q.Status);
        Assert.Null(q.Search);
        Assert.Equal("applied", q.SortBy);
        Assert.Equal("desc", q.SortDir);
    }

    [Fact]
    public void Parse_accepts_filters()
    {
        var q = ApplicationListQueryParser.Parse("Applied", "  acme ", "company", "ASC");
        Assert.Equal("applied", q.Status);
        Assert.Equal("acme", q.Search);
        Assert.Equal("company", q.SortBy);
        Assert.Equal("asc", q.SortDir);
    }

    [Fact]
    public void Parse_rejects_bad_status()
    {
        Assert.Throws<ArgumentException>(() =>
            ApplicationListQueryParser.Parse("nope", null, null, null));
    }

    [Fact]
    public void Parse_rejects_bad_sort()
    {
        Assert.Throws<ArgumentException>(() =>
            ApplicationListQueryParser.Parse(null, null, "salary", null));
    }
}
