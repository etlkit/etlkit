namespace EtlKit.DataFlow;

/// <summary>
/// Obsolete alias for <see cref="DbRowTransformation{TInput}"/> with <see cref="ExpandoObject"/> as
/// the row type.
/// </summary>
[Obsolete("Use DbRowTransformation instead of DbTransformation")]
public class DbTransformation : DbRowTransformation<ExpandoObject> { }
