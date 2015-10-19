namespace Endjin.CucumberJs.TestAdapter
{
    public class TestFileCandidate
    {
        public TestFileCandidate()
        {
        }

        public TestFileCandidate(string path)
        {
            this.Path = path;
        }

        public string Path { get; set; }
    }
}
