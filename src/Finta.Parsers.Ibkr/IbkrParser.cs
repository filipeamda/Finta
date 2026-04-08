using System.Globalization;
using Microsoft.Extensions.Logging;
using Finta.Common;
using Finta.Parsers.Ibkr.Strategies;

namespace Finta.Parsers.Ibkr;

public class IbkrParser(ILoggerFactory loggerFactory) : IExchangeParser
{
    private readonly ILogger<IbkrParser> _logger = loggerFactory.CreateLogger<IbkrParser>();
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    public string ExchangeName => "Interactive Brokers";

    public async IAsyncEnumerable<Transaction> ParseAsync(Stream csvStream)
    {
        _logger.LogInformation("Starting {ExchangeName} parse", ExchangeName);
        int transactionCount = 0;

        // 1. Detect the report format using a "Signature" check
        var format = await DetectFormatAsync(csvStream);

        if (format == IbkrReportFormat.Unknown)
        {
            _logger.LogError("Unrecognized {ExchangeName} format. No matching signature found", ExchangeName);
            throw new UnrecognizedExchangeFormatException(ExchangeName);
        }

        _logger.LogInformation("Detected format: {Format}", format);

        // 2. Reset the stream so the selected parser starts from the beginning
        ResetStream(csvStream);

        // 3. Delegate to the correct internal strategy
        switch (format)
        {
            case IbkrReportFormat.ActivityStatement:
                var activityParser = new IbkrActivityStatementParser(_loggerFactory.CreateLogger<IbkrActivityStatementParser>());
                await foreach (var transaction in activityParser.ParseAsync(csvStream))
                {
                    transactionCount++;
                    yield return transaction;
                }
                break;

            default:
                _logger.LogError("Unsupported format: {Format}", format);
                throw new UnrecognizedExchangeFormatException(ExchangeName);
        }

        _logger.LogInformation("Parse completed. TransactionCount: {TransactionCount}", transactionCount);
    }

    internal async Task<IbkrReportFormat> DetectFormatAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        
        // Read the first line to check for structural signature
        var firstLine = await reader.ReadLineAsync();
        if (string.IsNullOrEmpty(firstLine))
        {
            _logger.LogWarning("File appears to be empty or unreadable");
            return IbkrReportFormat.Unknown;
        }

        // Signature for "Activity Statement": Starts with "Statement,Header,Field Name,Field Value"
        if (firstLine.StartsWith("Statement,Header,Field Name,Field Value", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Activity Statement signature detected");
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
