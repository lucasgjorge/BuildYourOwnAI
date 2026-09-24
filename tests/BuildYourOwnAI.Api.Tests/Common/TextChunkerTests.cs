using BuildYourOwnAI.Api.Common;

namespace BuildYourOwnAI.Api.Tests.Common;

// C42
public sealed class TextChunkerTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   \n\t  ")]
    public void Blank_text_yields_no_chunks(string text) => Assert.Empty(TextChunker.Split(text));

    [Fact]
    public void Text_of_1000_chars_yields_one_chunk()
    {
        var text = new string('x', 1000);

        var chunk = Assert.Single(TextChunker.Split(text));

        Assert.Equal(text, chunk);
    }

    [Fact]
    public void Whitespace_runs_collapse_to_single_space()
    {
        Assert.Equal(["um dois tres"], TextChunker.Split("  um  \n\n dois\t\ttres  "));
    }

    [Fact]
    public void Long_text_is_split_with_200_char_overlap()
    {
        var text = string.Join(' ', Enumerable.Range(0, 600).Select(i => $"w{i:D3}"))[..2500].Trim();

        var chunks = TextChunker.Split(text);

        Assert.True(chunks.Count > 2);
        Assert.All(chunks, c => Assert.InRange(c.Length, 1, 1000));
        for (var i = 1; i < chunks.Count; i++)
            Assert.Equal(chunks[i - 1][^200..], chunks[i][..200]);
        var rebuilt = chunks[0] + string.Concat(chunks.Skip(1).Select(c => c[200..]));
        Assert.Equal(text, rebuilt);
    }
}
