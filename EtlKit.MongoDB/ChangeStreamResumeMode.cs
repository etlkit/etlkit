using JetBrains.Annotations;

namespace EtlKit.DataFlow;

/// <summary>
/// Controls how a resume token loaded from a checkpoint store is applied when the change stream
/// is opened.
/// </summary>
[PublicAPI]
public enum ChangeStreamResumeMode
{
    /// <summary>
    /// Apply the token as <c>resumeAfter</c>. MongoDB rejects this once an <c>invalidate</c>
    /// event has been delivered for the stream.
    /// </summary>
    ResumeAfter,

    /// <summary>
    /// Apply the token as <c>startAfter</c>, which MongoDB also accepts when the token came from an
    /// <c>invalidate</c> event — the watched collection having been dropped or renamed. This widens
    /// which tokens are accepted as a start point; it does not skip an <c>invalidate</c> the stream
    /// replays into, so recovery requires the checkpoint to hold that <c>invalidate</c>'s own token.
    /// Requires MongoDB 4.1.1 or later.
    /// </summary>
    StartAfter,
}
