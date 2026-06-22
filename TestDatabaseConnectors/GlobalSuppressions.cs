// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level supressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Minor Code Smell",
    "S1481:Unused local variables should be removed",
    Justification = "This is used to create fixtures",
    Scope = "member",
    Target = "~M:TestDatabaseConnectors.DBDestination.DbDestinationBatchChangesTests.AfterBatchWrite(ALE.EtlKit.ConnectionManager.IConnectionManager)"
)]
