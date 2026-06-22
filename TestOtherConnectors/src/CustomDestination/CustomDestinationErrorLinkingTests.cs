using EtlKit.Common.DataFlow;
using EtlKit.Primitives;

namespace EtlKit.TestOtherConnectors.CustomDestination
{
    public class CustomDestinationErrorLinkingTests
    {
        [Serializable]
        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void TestErrorLink()
        {
            //Arrange
            MemorySource<MySimpleRow> source = new MemorySource<MySimpleRow>
            {
                DataAsList = new List<MySimpleRow>
                {
                    new() { Col1 = 1, Col2 = "Test1" },
                    new() { Col1 = 2, Col2 = "ErrorRecord" },
                    new() { Col1 = 3, Col2 = "Test3" },
                },
            };
            CustomDestination<MySimpleRow> dest = new CustomDestination<MySimpleRow>(row =>
            {
                if (row.Col1 == 2)
                    throw new Exception("Error record!");
            });
            MemoryDestination<EtlKitError> errorDest = new MemoryDestination<EtlKitError>();

            //Act
            source.LinkTo(dest);
            dest.LinkErrorTo(errorDest);
            source.Execute();
            dest.Wait();
            errorDest.Wait();

            //Assert
            Assert.Collection(
                errorDest.Data,
                d =>
                    Assert.True(
                        !string.IsNullOrEmpty(d.RecordAsJson) && !string.IsNullOrEmpty(d.ErrorText)
                    )
            );
        }
    }
}
