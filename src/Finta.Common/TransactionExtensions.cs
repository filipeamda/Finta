namespace Finta.Common;

public static class TransactionExtensions
{
    /// <summary>
    /// Deduplicates a stream of transactions based on their financial fingerprint.
    /// Note: This is O(N) in memory where N is the number of UNIQUE transactions.
    /// </summary>
    public static async IAsyncEnumerable<Transaction> Deduplicate(
        this IAsyncEnumerable<Transaction> transactions)
    {
        var seenFingerprints = new HashSet<string>();
        
        await foreach (var transaction in transactions)
        {
            if (seenFingerprints.Add(transaction.Fingerprint))
            {
                yield return transaction;
            }
        }
    }
}
