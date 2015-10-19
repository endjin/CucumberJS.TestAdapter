namespace Endjin.CucumberJs.TestAdapter
{
    public class TestFileChangedEventArgs : System.EventArgs
    {
        public TestFileChangedEventArgs(string file, TestFileChangedReason reason)
        {
            this.File = new TestFileCandidate(file);
            this.ChangedReason = reason;
        }

        public TestFileCandidate File { get; private set; }

        public TestFileChangedReason ChangedReason { get; private set; }
    }
}
