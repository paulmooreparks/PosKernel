using PosKernel.AI.Models;

namespace PosKernel.AI.Core;

/// <summary>
/// Event arguments for receipt state changes.
/// ARCHITECTURAL PRINCIPLE: Coarse-grained events for simplicity and reliability.
/// </summary>
public class ReceiptChangedEventArgs : EventArgs
{
    /// <summary>
    /// The type of change that occurred.
    /// </summary>
    public ReceiptChangeType ChangeType { get; }
    
    /// <summary>
    /// The current state of the receipt after the change.
    /// ARCHITECTURAL PRINCIPLE: Always include complete state to avoid delta synchronization issues.
    /// </summary>
    public Receipt Receipt { get; }
    
    /// <summary>
    /// Optional additional context about the change.
    /// </summary>
    public string? Context { get; }

    public ReceiptChangedEventArgs(ReceiptChangeType changeType, Receipt receipt, string? context = null)
    {
        ChangeType = changeType;
        Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
        Context = context;
    }
}

/// <summary>
/// Types of receipt changes for event subscribers.
/// ARCHITECTURAL PRINCIPLE: Coarse-grained change types for simplicity.
/// </summary>
public enum ReceiptChangeType
{
    /// <summary>
    /// Items were added, removed, or modified on the receipt.
    /// </summary>
    ItemsUpdated,
    
    /// <summary>
    /// Receipt status changed (Building -> ReadyForPayment -> Completed).
    /// </summary>
    StatusChanged,
    
    /// <summary>
    /// Payment was completed successfully.
    /// </summary>
    PaymentCompleted,
    
    /// <summary>
    /// Receipt was cleared for next customer.
    /// </summary>
    Cleared,
    
    /// <summary>
    /// General update - covers any other state change.
    /// </summary>
    Updated
}

/// <summary>
/// Interface for components that can notify of receipt changes.
/// ARCHITECTURAL PRINCIPLE: Clean separation of concerns through interfaces.
/// </summary>
public interface IReceiptChangeNotifier
{
    /// <summary>
    /// Raised when the receipt state changes.
    /// </summary>
    event EventHandler<ReceiptChangedEventArgs>? ReceiptChanged;
}
