using System.Diagnostics.CodeAnalysis;
using EtlKit.DataFlow;
using EtlKit.Primitives;
using EtlKit.TestFlatFileConnectors.Helpers;

namespace EtlKit.TestFlatFileConnectors.JsonDestination
{
    public class JsonDestinationErrorLinkingTests
    {
        [Fact]
        public void RedirectBatch()
        {
            //Arrange
            var source = new MemorySource<MySimpleRow>
            {
                DataAsList = new List<MySimpleRow>
                {
                    new() { Col1 = "X" },
                    new() { Col1 = "1" },
                    new() { Col1 = "2" },
                    new() { Col1 = null },
                    new() { Col1 = "3" },
                },
            };
            var dest = new JsonDestination<MySimpleRow>("ErrorFile.json", ResourceType.File);
            var errorDest = new MemoryDestination<EtlKitError>();

            //Act
            source.LinkTo(dest);
            dest.LinkErrorTo(errorDest);
            source.Execute();
            dest.Wait();
            errorDest.Wait();

            //Assert
            Assert.Equal(
                File.ReadAllText("./ErrorFile.json"),
                File.ReadAllText("res/JsonDestination/TwoColumnsErrorLinking.json")
                    .NormalizeLineEndings()
            );
            Assert.Collection(
                errorDest.Data,
                d =>
                    Assert.True(
                        !string.IsNullOrEmpty(d.RecordAsJson) && !string.IsNullOrEmpty(d.ErrorText)
                    ),
                d =>
                    Assert.True(
                        !string.IsNullOrEmpty(d.RecordAsJson) && !string.IsNullOrEmpty(d.ErrorText)
                    )
            );
        }

        [Fact]
        public void NoErrorHandling()
        {
            //Arrange
            var source = new MemorySource<MySimpleRow>
            {
                DataAsList = new List<MySimpleRow>
                {
                    new() { Col1 = "X" },
                    new() { Col1 = "1" },
                    new() { Col1 = null },
                },
            };
            var dest = new JsonDestination<MySimpleRow>("ErrorFile.json", ResourceType.File);

            //Act
            //Assert
            Assert.ThrowsAny<Exception>(() =>
            {
                source.LinkTo(dest);
                source.Execute();
                dest.Wait();
            });
        }

        [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
        public class MySimpleRow
        {
            public string Col1 { get; set; }

            public string Col2
            {
                get
                {
                    if (Col1 is null or "X")
                        throw new Exception("Error record!");
                    return "Test" + Col1;
                }
            }
        }
    }
}
