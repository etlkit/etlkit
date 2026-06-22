using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace EtlKit.Kafka.Tests
{
    [UsedImplicitly]
    [SuppressMessage(
        "Minor Code Smell",
        "S2325:Methods and properties that don\'t access instance data should be static"
    )]
    public sealed class KafkaFixture
    {
        public string BootstrapAddress => TestShared.Helper.Config.KafkaBootstrapAddress;
    }
}
