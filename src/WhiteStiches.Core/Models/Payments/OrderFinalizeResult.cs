namespace WhiteStiches.Core.Models.Payments;

public enum OrderFinalizeOutcome
{
    /// <summary>This call captured the payment and decremented stock.</summary>
    Finalized,

    /// <summary>The order was already finalized by an earlier trigger (webhook vs browser return). No-op.</summary>
    AlreadyFinalized,

    /// <summary>No payment matched the charge id.</summary>
    NotFound,

    /// <summary>The gateway-captured amount did not match the order total. Left for manual review, not marked paid.</summary>
    AmountMismatch
}

/// <summary>Result of an idempotent order-finalization attempt.</summary>
public record OrderFinalizeResult(OrderFinalizeOutcome Outcome, string? OrderNumber)
{
    public bool Succeeded => Outcome is OrderFinalizeOutcome.Finalized or OrderFinalizeOutcome.AlreadyFinalized;

    public static readonly OrderFinalizeResult NotFound = new(OrderFinalizeOutcome.NotFound, null);
}
