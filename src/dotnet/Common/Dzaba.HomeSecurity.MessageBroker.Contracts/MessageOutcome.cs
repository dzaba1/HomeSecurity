namespace Dzaba.HomeSecurity.MessageBroker.Contracts;

/// <summary>
/// What a handler wants done with a message it was given. The three outcomes
/// are the whole vocabulary of at-least-once consumption: finished, try again,
/// or give up on it.
/// </summary>
public enum MessageOutcome
{
    /// <summary>Handled (or a harmless duplicate): remove it from the queue.</summary>
    Acknowledge,

    /// <summary>
    /// A transient failure: put it back for another delivery. The broker's
    /// delivery limit eventually moves a message that keeps failing to the
    /// dead-letter queue, so this never loops forever.
    /// </summary>
    Retry,

    /// <summary>
    /// A message that can never be handled (malformed, or failing validation):
    /// send it straight to the dead-letter queue instead of retrying.
    /// </summary>
    Reject
}
