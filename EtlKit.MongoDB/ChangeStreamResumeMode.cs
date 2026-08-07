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
    /// Apply the token as <c>startAfter</c>, which additionally resumes past an <c>invalidate</c>
    /// event — the watched collection having been dropped or renamed. Requires MongoDB 4.1.1
    /// or later.
    /// </summary>
    StartAfter,
}
