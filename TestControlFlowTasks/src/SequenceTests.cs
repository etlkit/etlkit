using EtlKit.ControlFlow;

namespace EtlKit.TestControlFlowTasks
{
    public class SequenceTests
    {
        private bool Action1Executed { get; set; }

        private void Action1()
        {
            Action1Executed = true;
        }

        [Fact]
        public void SimpleSequence()
        {
            //Arrange
            Action1Executed = false;
            //Act
            Sequence.Execute("Test sequence 1", Action1);
            //Assert
            Assert.True(Action1Executed);
        }

        private bool Action2Executed { get; set; }

        private void Action2(object parent)
        {
            Action2Executed = true;
            Assert.Equal("Test", parent);
        }

        [Fact]
        public void SequenceWithParent()
        {
            //Arrange
            Action2Executed = false;
            const string test = "Test";
            //Act
            Sequence<object>.Execute("Test sequence 2", Action2, test);
            //Assert
            Assert.True(Action2Executed);
        }
    }
}
