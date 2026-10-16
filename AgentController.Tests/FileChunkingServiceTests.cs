using AgentController.Services;

namespace AgentController.Tests;

public class FileChunkingServiceTests
{
    private readonly FileChunkingService _service = new();

    [Fact]
    public void Content_below_the_limit_is_not_split()
    {
        Assert.False(_service.NeedsChunking("abc", 10));
        Assert.True(_service.NeedsChunking("abcdefghijkl", 10));
    }

    [Fact]
    public void Chunks_overlap_and_cover_the_whole_content()
    {
        var content = string.Concat(Enumerable.Range(0, 3000).Select(i => (char)('a' + i % 26)));

        var chunks = _service.Split(content, 1000);

        Assert.All(chunks, c => Assert.True(c.Length <= 1000));
        Assert.Equal(content[..1000], chunks[0]);
        Assert.Equal(content[^chunks[^1].Length..], chunks[^1]);
        Assert.Equal(chunks[0][^500..], chunks[1][..500]);
    }

    [Fact]
    public void A_chunk_size_smaller_than_the_overlap_is_refused()
    {
        Assert.Throws<ArgumentException>(() => _service.Split("some content", 100));
    }

    [Fact]
    public void Json_results_are_merged_into_one_array()
    {
        var merged = _service.MergeResults(["[1,2]", "[3]"], "json");

        Assert.Equal("[1,2,3]", merged);
    }

    [Fact]
    public void Csv_results_keep_a_single_header()
    {
        var merged = _service.MergeResults(["id,name\n1,a", "id,name\n2,b"], "csv");

        Assert.Equal("id,name\n1,a\n2,b", merged);
    }
}
