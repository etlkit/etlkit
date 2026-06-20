using EtlKit.Common.DataFlow;
using EtlKit.ControlFlow;
using EtlKit.DataFlow;
using EtlKit.Logging;
using EtlKit.Primitives;
using EtlKit.TestShared.SharedFixtures;
using EtlKit.TestTransformations.Fixtures;

namespace EtlKit.TestTransformations.RowTransformation
{
    [Collection("Transformations")]
    public class RowTransformationErrorLinkingTests : TransformationsTestBase
    {
        public RowTransformationErrorLinkingTests(TransformationsDatabaseFixture fixture)
            : base(fixture) { }

        [Serializable]
        public class MySimpleRow
        {
            public int Col1 { get; set; }
            public string Col2 { get; set; }
        }

        [Fact]
        public void ThrowExceptionInFlow()
        {
            //Arrange
            var unused = new TwoColumnsTableFixture("RowTransExceptionTest");

            var source = new CsvSource<string[]>("res/RowTransformation/TwoColumns.csv");
            var dest = new DbDestination<MySimpleRow>(SqlConnection, "RowTransExceptionTest");

            CreateErrorTableTask.DropAndCreate(SqlConnection, "errors");
            var errorDest = new DbDestination<EtlKitError>(SqlConnection, "errors");

            //Act
            var trans = new RowTransformation<string[], MySimpleRow>(csvdata =>
            {
                var no = int.Parse(csvdata[0]);
                if (no == 2)
                    throw new Exception("Test");
                return new MySimpleRow { Col1 = no, Col2 = csvdata[1] };
            });

            source.LinkTo(trans);
            trans.LinkTo(dest);
            trans.LinkErrorTo(errorDest);
            source.Execute();
            dest.Wait();
            errorDest.Wait();

            //Assert
            Assert.Equal(2, RowCountTask.Count(SqlConnection, "RowTransExceptionTest"));
            Assert.Equal(1, RowCountTask.Count(SqlConnection, "errors"));
        }

        [Fact]
        public void ThrowExceptionWithoutHandling()
        {
            //Arrange
            var source = new CsvSource<string[]>("res/RowTransformation/TwoColumns.csv");
            var dest = new MemoryDestination<MySimpleRow>();

            //Act
            var trans = new RowTransformation<string[], MySimpleRow>(_ =>
                throw new InvalidOperationException("Test")
            );

            source.LinkTo(trans);
            trans.LinkTo(dest);

            //Assert
            Assert.Throws<AggregateException>(() =>
            {
                source.Execute();
                dest.Wait();
            });
        }
    }
}
