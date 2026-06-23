using EtlKit.ControlFlow;

namespace EtlKit.TestControlFlowTasks
{
    public class CustomTaskTests
    {
        private bool Action1Executed { get; set; }

        private void Action1()
        {
            Action1Executed = true;
        }

        [Fact]
        public void SimpleAction()
        {
            //Arrange
            Action1Executed = false;
            //Act
            CustomTask.Execute("Test custom task 1", Action1);
            //Assert
            Assert.True(Action1Executed);
        }

        private int Action2Value { get; set; }

        private void Action2(int param1)
        {
            Action2Value = param1;
        }

        [Fact]
        public void ActionChangingValue()
        {
            //Arrange
            Action2Value = 0;
            //Act
            CustomTask.Execute("Test custom task 2", Action2, 5);
            //Assert
            Assert.Equal(5, Action2Value);
        }

        private string Action3Value1 { get; set; }
        private bool Action3Value2 { get; set; }

        private void Action3(string param1, bool param2)
        {
            Action3Value1 = param1;
            Action3Value2 = param2;
        }

        [Fact]
        public void ActionWith2Parameter()
        {
            //Arrange
            Action3Value1 = null;
            Action3Value2 = false;
            //Act
            CustomTask.Execute("Test custom task 3", Action3, "t", true);
            //Assert
            Assert.Equal("t", Action3Value1);
            Assert.True(Action3Value2);
        }
    }
}
