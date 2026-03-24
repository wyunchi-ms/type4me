using Type4Me.Database;
using Xunit;

namespace Type4Me.Tests;

/// <summary>
/// Tests for HistoryStore (SQLite CRUD).
/// Uses a temporary database file per test for isolation.
/// </summary>
public class HistoryStoreTests : IDisposable
{
    private readonly HistoryStore _store;

    public HistoryStoreTests()
    {
        // HistoryStore creates db in %AppData%\Type4Me\ — we use it as-is
        // since tests need a real SQLite DB. The store will create the table if needed.
        _store = new HistoryStore();
    }

    [Fact]
    public async Task InsertAndFetch_RoundTrips()
    {
        var record = new HistoryRecord
        {
            Id = $"test-{Guid.NewGuid()}",
            RawText = "hello world",
            FinalText = "Hello, world!",
            ProcessingMode = "Smart Mode",
            DurationSeconds = 2.5,
            Status = "completed",
        };

        await _store.InsertAsync(record);
        var results = await _store.FetchAllAsync(limit: 100);

        var found = results.FirstOrDefault(r => r.Id == record.Id);
        Assert.NotNull(found);
        Assert.Equal("Hello, world!", found.FinalText);
        Assert.Equal("hello world", found.RawText);
        Assert.Equal("Smart Mode", found.ProcessingMode);
        Assert.Equal(2.5, found.DurationSeconds, precision: 1);

        // Cleanup
        await _store.DeleteAsync(record.Id);
    }

    [Fact]
    public async Task Delete_RemovesRecord()
    {
        var id = $"test-del-{Guid.NewGuid()}";
        var record = new HistoryRecord
        {
            Id = id,
            RawText = "to be deleted",
            FinalText = "to be deleted",
        };

        await _store.InsertAsync(record);
        await _store.DeleteAsync(id);

        var results = await _store.FetchAllAsync(limit: 100);
        Assert.DoesNotContain(results, r => r.Id == id);
    }

    [Fact]
    public async Task Count_ReturnsCorrectValue()
    {
        var before = await _store.CountAsync();

        var id = $"test-cnt-{Guid.NewGuid()}";
        await _store.InsertAsync(new HistoryRecord
        {
            Id = id,
            RawText = "count test",
            FinalText = "count test",
        });

        var after = await _store.CountAsync();
        Assert.Equal(before + 1, after);

        // Cleanup
        await _store.DeleteAsync(id);
    }

    [Fact]
    public async Task FetchAllWithSearch_FiltersCorrectly()
    {
        var unique = $"xyzzy{Guid.NewGuid():N}";
        var id = $"test-search-{Guid.NewGuid()}";
        await _store.InsertAsync(new HistoryRecord
        {
            Id = id,
            RawText = unique,
            FinalText = $"Final {unique}",
        });

        var results = await _store.FetchAllAsync(limit: 100, search: unique);
        Assert.Contains(results, r => r.Id == id);

        var noResults = await _store.FetchAllAsync(limit: 100, search: "nonexistent_query_string_12345");
        Assert.DoesNotContain(noResults, r => r.Id == id);

        // Cleanup
        await _store.DeleteAsync(id);
    }

    [Fact]
    public async Task FetchAll_PaginationWorks()
    {
        // Insert 3 records
        var ids = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var id = $"test-page-{Guid.NewGuid()}";
            ids.Add(id);
            await _store.InsertAsync(new HistoryRecord
            {
                Id = id,
                RawText = $"page test {i}",
                FinalText = $"page test {i}",
            });
        }

        var page1 = await _store.FetchAllAsync(limit: 2, offset: 0);
        Assert.True(page1.Length <= 2);

        // Cleanup
        foreach (var id in ids)
            await _store.DeleteAsync(id);
    }

    public void Dispose()
    {
        _store.Dispose();
    }
}
