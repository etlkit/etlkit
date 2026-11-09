[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("TestHelper")]
// QueryParameter now lives in EtlKit.Common. The forwarder keeps assemblies compiled against an
// older EtlKit resolving EtlKit.QueryParameter.
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(EtlKit.QueryParameter))]
