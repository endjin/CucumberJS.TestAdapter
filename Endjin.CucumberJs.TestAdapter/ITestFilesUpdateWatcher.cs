﻿namespace Endjin.CucumberJs.TestAdapter
{
    using System;

    public interface ITestFilesUpdateWatcher
    {
        event EventHandler<TestFileChangedEventArgs> FileChangedEvent;

        void AddWatch(string path);

        void RemoveWatch(string path);
    }
}
