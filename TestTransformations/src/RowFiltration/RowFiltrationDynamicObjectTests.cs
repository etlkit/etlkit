using EtlKit.DataFlow;

namespace EtlKit.TestTransformations.RowFiltration
{
    public class RowFiltrationDynamicObjectTests
    {
        [Fact]
        public void FilterDynamicObjects_ByFieldValue()
        {
            // Arrange
            var source = new MemorySource();
            dynamic row1 = new ExpandoObject();
            row1.AdminReserveRatio = 25;
            row1.AdminReserveRatioToday = 30;
            dynamic row2 = new ExpandoObject();
            row2.AdminReserveRatio = 25;
            row2.AdminReserveRatioToday = 25;
            dynamic row3 = new ExpandoObject();
            row3.AdminReserveRatio = 30;
            row3.AdminReserveRatioToday = 25;
            source.DataAsList.Add(row1);
            source.DataAsList.Add(row2);
            source.DataAsList.Add(row3);

            // Filter: only rows where ratios differ
            var filtration = new EtlKit.DataFlow.RowFiltration(row =>
            {
                var dict = (IDictionary<string, object>)row;
                return !Equals(dict["AdminReserveRatio"], dict["AdminReserveRatioToday"]);
            });
            var dest = new MemoryDestination();

            // Act
            source.LinkTo(filtration);
            filtration.LinkTo(dest);
            source.Execute();
            dest.Wait();

            // Assert - row2 (25 == 25) filtered out, rows 1 and 3 pass
            Assert.Equal(2, dest.Data.Count);
        }

        [Fact]
        public void FilterDynamicObjects_ArithmeticCondition()
        {
            // Arrange
            var source = new MemorySource();
            dynamic row1 = new ExpandoObject();
            row1.AccrualByDaySum = 100m;
            row1.BurnByDaySum = 10m;
            dynamic row2 = new ExpandoObject();
            row2.AccrualByDaySum = 50m;
            row2.BurnByDaySum = 50m;
            dynamic row3 = new ExpandoObject();
            row3.AccrualByDaySum = 10m;
            row3.BurnByDaySum = 100m;
            source.DataAsList.Add(row1);
            source.DataAsList.Add(row2);
            source.DataAsList.Add(row3);

            // Filter: only rows where accrual > burn (positive reserve)
            var filtration = new EtlKit.DataFlow.RowFiltration(row =>
            {
                var dict = (IDictionary<string, object>)row;
                return (decimal)dict["AccrualByDaySum"] > (decimal)dict["BurnByDaySum"];
            });
            var dest = new MemoryDestination();

            // Act
            source.LinkTo(filtration);
            filtration.LinkTo(dest);
            source.Execute();
            dest.Wait();

            // Assert - only row1 passes (100 > 10)
            Assert.Single(dest.Data);
        }

        [Fact]
        public void FilterDynamicObjects_EmptySource()
        {
            // Arrange
            var source = new MemorySource();
            var filtration = new EtlKit.DataFlow.RowFiltration(row => true);
            var dest = new MemoryDestination();

            // Act
            source.LinkTo(filtration);
            filtration.LinkTo(dest);
            source.Execute();
            dest.Wait();

            // Assert
            Assert.Empty(dest.Data);
        }
    }
}
