using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using CsvHelper;
using CsvHelper.Configuration;
using Finta.Common;

namespace Finta.Parsers.Ibkr.Strategies;

/// <summary>
/// Strategy for parsing the standard IBKR "Activity Statement" CSV format.
/// </summary>
internal partial class IbkrActivityStatementParser(ILogger<IbkrActivityStatementParser> logger)
{
    private readonly ILogger<IbkrActivityStatementParser> _logger = logger;
    [GeneratedRegex(@"(?<ticker>[A-Z0-9.]+)\(")]
    private static partial Regex TickerRegex();

    public async IAsyncEnumerable<Transaction> ParseAsync(Stream csvStream)
    {
        _logger.LogInformation("Starting Activity Statement parse");
        int rowCount = 0;
        int transactionCount = 0;

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            BadDataFound = null,
        };

        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var csv = new CsvReader(reader, config);

        while (await csv.ReadAsync())
        {
            rowCount++;
            var lineNumber = rowCount;
            var section = csv.GetField(0);
            var rowType = csv.GetField(1);

            if (rowType != "Data")
            {
                continue;
            }

            Transaction? transaction = section switch
            {
                "Trades" => ParseTrade(csv, lineNumber),
                "Dividends" => ParseDividend(csv, lineNumber),
                "Withholding Tax" => ParseTax(csv, lineNumber),
                "Fees" => ParseFee(csv, lineNumber),
                _ => null
            };

            if (transaction != null)
            {
                transactionCount++;
                yield return transaction;
            }
        }

        _logger.LogInformation("Activity Statement parse completed. RowCount: {RowCount}, TransactionCount: {TransactionCount}", rowCount, transactionCount);
    }

    private Transaction? ParseTrade(CsvReader csv, int lineNumber)
    {
        var discriminator = csv.GetField(2);
        if (discriminator != "Order")
        {
            _logger.LogDebug("Skipped non-trade row. LineNumber: {LineNumber}, Discriminator: {Discriminator}", lineNumber, discriminator);
            return null;
        }

        var assetCategory = csv.GetField(3);
        if (assetCategory != "Stocks")
        {
            _logger.LogDebug("Skipped non-stock transaction. LineNumber: {LineNumber}, AssetCategory: {AssetCategory}", lineNumber, assetCategory);
            return null;
        }

        var currency = csv.GetField(4);
        if (string.IsNullOrWhiteSpace(currency))
        {
            _logger.LogWarning("Currency missing, defaulting to USD. LineNumber: {LineNumber}, FieldName: Currency, Default: USD", lineNumber);
            currency = "USD";
        }

        var symbol = csv.GetField(5);
        if (string.IsNullOrWhiteSpace(symbol))
        {
            _logger.LogError("Skipped trade: Symbol required. LineNumber: {LineNumber}, FieldName: Symbol", lineNumber);
            return null;
        }

        var dateTimeStr = csv.GetField(6) ?? "";
        if (!DateTime.TryParseExact(dateTimeStr, "yyyy-MM-dd, HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            _logger.LogError("Skipped trade: Invalid date format. LineNumber: {LineNumber}, FieldName: DateTime, ActualValue: {DateValue}, ExpectedFormat: yyyy-MM-dd, HH:mm:ss", lineNumber, dateTimeStr);
            return null;
        }

        var quantity = csv.GetField<decimal>(7);
        if (quantity == 0)
        {
            _logger.LogWarning("Zero quantity trade. LineNumber: {LineNumber}, Ticker: {Ticker}", lineNumber, symbol);
        }

        var price = csv.GetField<decimal>(8);
        var commission = Math.Abs(csv.GetField<decimal>(11));

        return new Transaction(
            Date: date,
            Ticker: symbol,
            Type: quantity > 0 ? TransactionType.Buy : TransactionType.Sell,
            Quantity: quantity,
            Price: price,
            Commission: commission,
            Currency: currency,
            RawData: string.Join(",", csv.Context?.Parser?.Record ?? Array.Empty<string>())
        );
    }

    private Transaction? ParseDividend(CsvReader csv, int lineNumber)
    {
        var currency = csv.GetField(2);
        if (string.IsNullOrWhiteSpace(currency))
        {
            _logger.LogWarning("Currency missing, defaulting to USD. LineNumber: {LineNumber}, FieldName: Currency, Default: USD", lineNumber);
            currency = "USD";
        }

        var dateStr = csv.GetField(3) ?? "";
        var description = csv.GetField(4) ?? "";
        var amount = csv.GetField<decimal>(5);

        if (string.IsNullOrEmpty(dateStr) || dateStr.Contains("Total"))
        {
            _logger.LogDebug("Skipped dividend summary row. LineNumber: {LineNumber}, DateStr: {DateStr}", lineNumber, dateStr);
            return null;
        }

        if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, out var date))
        {
            _logger.LogError("Skipped dividend: Invalid date format. LineNumber: {LineNumber}, FieldName: Date, ActualValue: {DateValue}", lineNumber, dateStr);
            return null;
        }

        var ticker = ExtractTicker(description);

        return new Transaction(
            Date: date,
            Ticker: ticker,
            Type: TransactionType.Dividend,
            Quantity: amount,
            Price: 1,
            Commission: 0,
            Currency: currency,
            RawData: string.Join(",", csv.Context?.Parser?.Record ?? Array.Empty<string>())
        );
    }

    private Transaction? ParseTax(CsvReader csv, int lineNumber)
    {
        var currency = csv.GetField(2);
        if (string.IsNullOrWhiteSpace(currency))
        {
            _logger.LogWarning("Currency missing, defaulting to USD. LineNumber: {LineNumber}, FieldName: Currency, Default: USD", lineNumber);
            currency = "USD";
        }

        var dateStr = csv.GetField(3) ?? "";
        var description = csv.GetField(4) ?? "";
        var amount = csv.GetField<decimal>(5);

        if (string.IsNullOrEmpty(dateStr) || dateStr.Contains("Total"))
        {
            _logger.LogDebug("Skipped tax summary row. LineNumber: {LineNumber}, DateStr: {DateStr}", lineNumber, dateStr);
            return null;
        }

        if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, out var date))
        {
            _logger.LogError("Skipped withholding tax: Invalid date format. LineNumber: {LineNumber}, FieldName: Date, ActualValue: {DateValue}", lineNumber, dateStr);
            return null;
        }

        var ticker = ExtractTicker(description);

        return new Transaction(
            Date: date,
            Ticker: ticker,
            Type: TransactionType.Tax,
            Quantity: amount,
            Price: 1,
            Commission: 0,
            Currency: currency,
            RawData: string.Join(",", csv.Context?.Parser?.Record ?? Array.Empty<string>())
        );
    }

    private Transaction? ParseFee(CsvReader csv, int lineNumber)
    {
        var currency = csv.GetField(3);
        if (string.IsNullOrWhiteSpace(currency))
        {
            _logger.LogWarning("Currency missing, defaulting to USD. LineNumber: {LineNumber}, FieldName: Currency, Default: USD", lineNumber);
            currency = "USD";
        }

        var dateStr = csv.GetField(4) ?? "";
        var amount = csv.GetField<decimal>(6);

        if (string.IsNullOrEmpty(dateStr) || dateStr.Contains("Total"))
        {
            _logger.LogDebug("Skipped fee summary row. LineNumber: {LineNumber}, DateStr: {DateStr}", lineNumber, dateStr);
            return null;
        }

        if (amount == 0)
        {
            _logger.LogDebug("Skipped zero-amount fee. LineNumber: {LineNumber}", lineNumber);
            return null;
        }

        if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, out var date))
        {
            _logger.LogError("Skipped fee: Invalid date format. LineNumber: {LineNumber}, FieldName: Date, ActualValue: {DateValue}", lineNumber, dateStr);
            return null;
        }

        return new Transaction(
            Date: date,
            Ticker: "FEE",
            Type: TransactionType.Fee,
            Quantity: amount,
            Price: 1,
            Commission: 0,
            Currency: currency,
            RawData: string.Join(",", csv.Context?.Parser?.Record ?? Array.Empty<string>())
        );
    }

    private static string ExtractTicker(string description)
    {
        var match = TickerRegex().Match(description);
        return match.Success ? match.Groups["ticker"].Value : "UNKNOWN";
    }
}
