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
);
