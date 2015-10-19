namespace Endjin.CucumberJs.TestAdapter
{
    using System;

    public interface ITestFileAddRemoveListener
    {
        event EventHandler<TestFileChangedEventArgs> TestFileChanged;

        void StartListeningForTestFileChanges();

        void StopListeningForTestFileChanges();
    }
}
