using EtlKit.DataFlow;

namespace EtlKit.TestFlatFileConnectors.CsvDestination
{
    public class CsvDestinationSerializationTests
    {
        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        public class MySeriRow
        {
            [Name("Header2")]
            [Index(2)]
            public DateTime Col2 { get; set; }

            [Name("Header1")]
            [Index(1)]
            public int Col1 { get; set; }
        }

        [Fact]
        public void SerializingDateTime()
        {
            var rowCount = 0;
            //Arrange
            var source = new CustomSource<MySeriRow>(
                () =>
                    new MySeriRow
                    {
                        Col1 = 1,
                        Col2 = new DateTime(2010, 02, 05, 0, 0, 0, DateTimeKind.Local),
                    },
                () => rowCount++ == 1
            );

            //Act
            var dest = new CsvDestination<MySeriRow>("./DateTimeSerialization.csv");
            source.LinkTo(dest);
            source.Execute();
            dest.Wait();

            //Assert
            Assert.Equal(
                File.ReadAllText("./DateTimeSerialization.csv"),
                File.ReadAllText("res/CsvDestination/DateTimeSerialization.csv")
            );
        }
    }
}
