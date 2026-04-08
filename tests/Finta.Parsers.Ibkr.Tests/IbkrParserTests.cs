using Finta.Parsers.Ibkr;
using Finta.Common;

namespace Finta.Parsers.Ibkr.Tests;

public class IbkrParserTests
{
    public static IEnumerable<object[]> GetCsvFiles()
    {
        var testDataPath = Path.Combine(AppContext.BaseDirectory, "TestData");
        if (!Directory.Exists(testDataPath))
            yield break;

        // Recursively find all CSV files
        foreach (var file in Directory.GetFiles(testDataPath, "*.csv", SearchOption.AllDirectories))
        {
            // Skip files in the "Unknown" folder for the main parsing test
            if (file.Contains("Unknown")) continue;
            
            yield return new object[] { file };
        }
    }

    [Theory]
    [MemberData(nameof(GetCsvFiles))]
    public async Task ParseAsync_ShouldYieldTransactions(string filePath)
    {
        // Arrange
        var parser = new IbkrParser(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        using var stream = File.OpenRead(filePath);

        // Act
        var transactions = new List<Transaction>();
        await foreach (var transaction in parser.ParseAsync(stream))
        {
            transactions.Add(transaction);
        }

        // Assert
        Assert.NotEmpty(transactions);
        
        foreach (var t in transactions)
        {
            Assert.NotEqual(default, t.Date);
            Assert.False(string.IsNullOrWhiteSpace(t.Ticker));
            Assert.False(string.IsNullOrWhiteSpace(t.Currency));
            Assert.False(string.IsNullOrWhiteSpace(t.RawData));
        }
    }

    [Theory]
    [InlineData("ActivityStatement/activity-statement2023-anonymized.csv", "ActivityStatement")]
    [InlineData("ActivityStatement/activity-statement2024-anonymized.csv", "ActivityStatement")]
    [InlineData("ActivityStatement/activity-statement2025-anonymized.csv", "ActivityStatement")]
    [InlineData("Unknown/junk.csv", "Unknown")]
    public async Task DetectFormat_ShouldIdentifyCorrectFormat(string relativePath, string expectedFormatName)
    {
        // Arrange
        var filePath = Path.Combine(AppContext.BaseDirectory, "TestData", relativePath);
        using var stream = File.OpenRead(filePath);
        var parser = new IbkrParser(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        // Act
        var detectedFormat = await parser.DetectFormatAsync(stream);

        // Assert
        Assert.Equal(expectedFormatName, detectedFormat.ToString());
    }

    [Fact]
    public async Task ParseAsync_WithUnknownFormat_ShouldThrowException()
    {
        // Arrange
        var parser = new IbkrParser(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        var filePath = Path.Combine(AppContext.BaseDirectory, "TestData", "Unknown", "junk.csv");
        using var stream = File.OpenRead(filePath);

        // Act & Assert
        await Assert.ThrowsAsync<UnrecognizedExchangeFormatException>(async () =>
        {
            await foreach (var _ in parser.ParseAsync(stream))
            {
                // Consuming the stream to trigger the exception
            }
        });
    }

    [Fact]
    public async Task ParseAsync_WithDeduplication_ShouldRemoveOverlaps()
    {
        // Arrange
        var parser = new IbkrParser(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        var filePath = Path.Combine(AppContext.BaseDirectory, "TestData", "ActivityStatement", "activity-statement2025-anonymized.csv");
        
        // Act
        var stream1 = File.OpenRead(filePath);
        var stream2 = File.OpenRead(filePath);
        
        var combinedStream = Concat(parser.ParseAsync(stream1), parser.ParseAsync(stream2));
        var deduplicatedTransactions = new List<Transaction>();
        
        await foreach (var t in combinedStream.Deduplicate())
        {
            deduplicatedTransactions.Add(t);
        }

        using var stream3 = File.OpenRead(filePath);
        var singleFileTransactions = new List<Transaction>();
        await foreach (var t in parser.ParseAsync(stream3))
        {
            singleFileTransactions.Add(t);
        }

        stream1.Dispose();
        stream2.Dispose();

        // Assert
        Assert.NotEmpty(singleFileTransactions);
        Assert.Equal(singleFileTransactions.Count, deduplicatedTransactions.Count);
    }

    private static async IAsyncEnumerable<T> Concat<T>(params IAsyncEnumerable<T>[] enumerables)
    {
        foreach (var enumerable in enumerables)
        {
            await foreach (var item in enumerable)
            {
                yield return item;
            }
        }
    }
}
