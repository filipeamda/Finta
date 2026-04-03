namespace Finta.Common;

public enum TransactionType 
{ 
    Buy, 
    Sell, 
    Dividend, 
    Tax, 
    Fee 
}

/// <summary>
/// The immutable core record for all financial movements.
/// </summary>
public record Transaction(
    DateTime Date,
    string Ticker,
    TransactionType Type,
    decimal Quantity,
    decimal Price,
    decimal Commission,
    string Currency,
    string RawData
)
{
    /// <summary>
    /// A deterministic unique identifier for the transaction based on its financial facts.
    /// </summary>
    public string Fingerprint => $"{Date:O}|{Ticker}|{Type}|{Quantity:F8}|{Price:F8}|{Currency}";
}
