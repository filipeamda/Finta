using System.Globalization;
using Finta.Common;
using Finta.Parsers.Ibkr.Strategies;

namespace Finta.Parsers.Ibkr;

public class IbkrParser : IExchangeParser
{
    public string ExchangeName => "Interactive Brokers";

    public async IAsyncEnumerable<Transaction> ParseAsync(Stream csvStream)
    {
        // 1. Detect the report format using a "Signature" check
        var format = await DetectFormatAsync(csvStream);

        // 2. Reset the stream so the selected parser starts from the beginning
        ResetStream(csvStream);

        // 3. Delegate to the correct internal strategy
        switch (format)
        {
            case IbkrReportFormat.ActivityStatement:
                var activityParser = new IbkrActivityStatementParser();
                await foreach (var transaction in activityParser.ParseAsync(csvStream))
                {
                    yield return transaction;
                }
                break;

            case IbkrReportFormat.Unknown:
            default:
                throw new UnrecognizedExchangeFormatException(ExchangeName);
        }
    }

    internal static async Task<IbkrReportFormat> DetectFormatAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        
        // Read the first line to check for structural signature
        var firstLine = await reader.ReadLineAsync();
        if (string.IsNullOrEmpty(firstLine)) return IbkrReportFormat.Unknown;

        // Signature for "Activity Statement": Starts with "Statement,Header,Field Name,Field Value"
        if (firstLine.StartsWith("Statement,Header,Field Name,Field Value", StringComparison.OrdinalIgnoreCase))
        {
            return IbkrReportFormat.ActivityStatement;
        }

        // Future signatures would go here (e.g. Flex Queries, V3 reports, etc.)
        
        return IbkrReportFormat.Unknown;
    }

    private static void ResetStream(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
        else
        {
            // Note: In a production NuGet, we might want to wrap the stream in a 
            // SeekableStream wrapper if the source is a network stream.
            // For now, we assume standard file streams which are seekable.
            throw new NotSupportedException("IBKR Parser requires a seekable stream for format detection.");
        }
    }

    internal enum IbkrReportFormat
    {
        Unknown,
        ActivityStatement,
        FlexQuery // Example for the future
    }
}
