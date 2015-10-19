namespace Endjin.CucumberJs.TestAdapter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestWindow.Extensibility;

    public class CucumberJsTestContainer : ITestContainer
    {
        private readonly string source;
        private readonly ITestContainerDiscoverer discoverer;
        private readonly DateTime timeStamp;

        public CucumberJsTestContainer(ITestContainerDiscoverer discoverer, string source)
        {
            this.discoverer = discoverer;
            this.source = source;
            this.timeStamp = this.GetTimeStamp();
        }

        private CucumberJsTestContainer(CucumberJsTestContainer copy)
            : this(copy.discoverer, copy.Source)
        {
        }

        public IEnumerable<Guid> DebugEngines
        {
            get { return Enumerable.Empty<Guid>(); }
        }

        public ITestContainerDiscoverer Discoverer
        {
            get { return this.discoverer; }
        }

        public bool IsAppContainerTestContainer
        {
            get { return false; }
        }

        public string Source
        {
            get { return this.source; }
        }

        public Microsoft.VisualStudio.TestPlatform.ObjectModel.FrameworkVersion TargetFramework
        {
            get { return Microsoft.VisualStudio.TestPlatform.ObjectModel.FrameworkVersion.None; }
        }

        public Microsoft.VisualStudio.TestPlatform.ObjectModel.Architecture TargetPlatform
        {
            get { return Microsoft.VisualStudio.TestPlatform.ObjectModel.Architecture.AnyCPU; }
        }

        public int CompareTo(ITestContainer other)
        {
            var testContainer = other as CucumberJsTestContainer;
            if (testContainer == null)
            {
                return -1;
            }

            var result = string.Compare(this.Source, testContainer.Source, StringComparison.OrdinalIgnoreCase);
            if (result != 0)
            {
                return result;
            }

            return this.timeStamp.CompareTo(testContainer.timeStamp);
        }

        public Microsoft.VisualStudio.TestWindow.Extensibility.Model.IDeploymentData DeployAppContainer()
        {
            return null;
        }

        public ITestContainer Snapshot()
        {
            return new CucumberJsTestContainer(this);
        }

        private DateTime GetTimeStamp()
        {
            if (!string.IsNullOrEmpty(this.Source) && File.Exists(this.Source))
            {
                return File.GetLastWriteTime(this.Source);
            }
            else
            {
                return DateTime.MinValue;
            }
        }
    }
}
