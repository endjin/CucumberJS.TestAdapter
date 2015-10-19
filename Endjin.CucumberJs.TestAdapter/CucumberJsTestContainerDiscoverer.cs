namespace Endjin.CucumberJs.TestAdapter
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.ComponentModel.Composition;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;
    using Microsoft.VisualStudio.TestWindow.Extensibility;

    [Export(typeof(ITestContainerDiscoverer))]
    public class CucumberJsTestContainerDiscoverer : ITestContainerDiscoverer
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ConcurrentDictionary<string, ITestContainer> cachedContainers;

        private ISolutionEventsListener solutionListener;
        private ITestFilesUpdateWatcher testFilesUpdateWatcher;
        private ITestFileAddRemoveListener testFilesAddRemoveListener;

        private object sync = new object();

        /// <summary>
        /// This is set to true one initial plugin load and then reset to false when a
        /// solution is unloaded.  This will imply a full container refresh as well as
        /// supress asking the containers to refresh until the initial search finishedd
        /// </summary>
        private bool initialContainerSearch;

        /// <summary>
        /// This is set to true when a chutzpah.json file changes. This will set a flag that
        /// will cause a full container refresh
        /// </summary>
        private bool forceFullContainerRefresh;

        [ImportingConstructor]
        public CucumberJsTestContainerDiscoverer(
            [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider,
            ISolutionEventsListener solutionListener,
            ITestFilesUpdateWatcher testFilesUpdateWatcher,
            ITestFileAddRemoveListener testFilesAddRemoveListener)
        {
            this.initialContainerSearch = true;
            this.cachedContainers = new ConcurrentDictionary<string, ITestContainer>(StringComparer.OrdinalIgnoreCase);
            this.serviceProvider = serviceProvider;
            this.solutionListener = solutionListener;
            this.testFilesUpdateWatcher = testFilesUpdateWatcher;
            this.testFilesAddRemoveListener = testFilesAddRemoveListener;

            this.testFilesAddRemoveListener.TestFileChanged += this.OnProjectItemChanged;
            this.testFilesAddRemoveListener.StartListeningForTestFileChanges();

            this.solutionListener.SolutionUnloaded += this.SolutionListenerOnSolutionUnloaded;
            this.solutionListener.SolutionProjectChanged += this.OnSolutionProjectChanged;
            this.solutionListener.StartListeningForChanges();

            this.testFilesUpdateWatcher.FileChangedEvent += this.OnProjectItemChanged;
        }

        public event EventHandler TestContainersUpdated;

        public Uri ExecutorUri
        {
            get { return CucumberJsTestExecutor.ExecutorUri; }
        }

        public IEnumerable<ITestContainer> TestContainers
        {
            get { return this.GetTestContainers(); }
        }

        public void Dispose()
        {
            this.Dispose(true);

            // Use SupressFinalize in case a subclass
            // of this type implements a finalizer.
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.testFilesUpdateWatcher != null)
                {
                    this.testFilesUpdateWatcher.FileChangedEvent -= this.OnProjectItemChanged;
                    ((IDisposable)this.testFilesUpdateWatcher).Dispose();
                    this.testFilesUpdateWatcher = null;
                }

                if (this.testFilesAddRemoveListener != null)
                {
                    this.testFilesAddRemoveListener.TestFileChanged -= this.OnProjectItemChanged;
                    this.testFilesAddRemoveListener.StopListeningForTestFileChanges();
                    this.testFilesAddRemoveListener = null;
                }

                if (this.solutionListener != null)
                {
                    this.solutionListener.SolutionProjectChanged -= this.OnSolutionProjectChanged;
                    this.solutionListener.StopListeningForChanges();
                    this.solutionListener = null;
                }
            }
        }

        private static bool HasTestFileExtension(string path)
        {
            return path.EndsWith("feature");
        }

        // Fire Events to Notify testcontainerdiscoverer listeners that containers have changed.
        // This is the push notification VS uses to update the unit test window.
        // The initialContainerSearch check is meant to prevent us from notifying VS about updates
        // until it is ready
        private void OnTestContainersChanged()
        {
            if (this.TestContainersUpdated != null && !this.initialContainerSearch)
            {
                this.TestContainersUpdated(this, EventArgs.Empty);
            }
        }

        // The solution was unloaded so we need to indicate that next time containers are requested we do a full search
        private void SolutionListenerOnSolutionUnloaded(object sender, EventArgs eventArgs)
        {
            this.initialContainerSearch = true;
        }

        // Handler to react to project load/unload events.
        private void OnSolutionProjectChanged(object sender, SolutionEventsListenerEventArgs e)
        {
            if (e != null)
            {
                string projectPath = VsSolutionHelper.GetProjectPath(e.Project);

                var files = this.FindPotentialTestFiles(e.Project);
                if (e.ChangedReason == SolutionChangedReason.Load)
                {
                    this.UpdateTestContainersAndFileWatchers(files, true);
                }
                else if (e.ChangedReason == SolutionChangedReason.Unload)
                {
                    this.UpdateTestContainersAndFileWatchers(files, false);
                }
            }

            // Do not fire OnTestContainersChanged here.
            // This will cause us to fire this event too early before the UTE is ready to process containers and will result in an exception.
            // The UTE will query all the TestContainerDiscoverers once the solution is loaded.
        }

        // After a project is loaded or unloaded either add or remove from the file watcher
        // all test potential items inside that project
        private void UpdateTestContainersAndFileWatchers(IEnumerable<TestFileCandidate> files, bool isAdd)
        {
            Parallel.ForEach(files, file =>
            {
                try
                {
                    if (isAdd)
                    {
                        this.testFilesUpdateWatcher.AddWatch(file.Path);
                        this.AddTestContainerIfTestFile(file);
                    }
                    else
                    {
                        this.testFilesUpdateWatcher.RemoveWatch(file.Path);
                        this.RemoveTestContainer(file);
                    }
                }
                catch (Exception)
                {
                }
            });
        }

        // Handler to react to test file Add/remove/rename andcontents changed events
        private void OnProjectItemChanged(object sender, TestFileChangedEventArgs e)
        {
            if (e != null)
            {
                // Don't do anything for files we are sure can't be test files
                if (!HasTestFileExtension(e.File.Path))
                {
                    return;
                }

                switch (e.ChangedReason)
                {
                    case TestFileChangedReason.Added:
                        this.testFilesUpdateWatcher.AddWatch(e.File.Path);
                        this.AddTestContainerIfTestFile(e.File);

                        break;
                    case TestFileChangedReason.Removed:
                        this.testFilesUpdateWatcher.RemoveWatch(e.File.Path);
                        this.RemoveTestContainer(e.File);

                        break;
                    case TestFileChangedReason.Changed:
                        this.AddTestContainerIfTestFile(e.File);
                        break;
                }

                this.OnTestContainersChanged();
            }
        }

        // Adds a test container for the given file if it is a test file.
        // This will first remove any existing container for that file
        private void AddTestContainerIfTestFile(TestFileCandidate file)
        {
            var isTestFile = this.IsTestFile(file.Path);

            this.RemoveTestContainer(file); // Remove if there is an existing container

            if (isTestFile)
            {
                var container = new CucumberJsTestContainer(this, file.Path.ToLowerInvariant());
                this.cachedContainers[container.Source] = container;
            }
        }

        // Will remove a test container for a given file path
        private void RemoveTestContainer(TestFileCandidate file)
        {
            ITestContainer container;
            this.cachedContainers.TryRemove(file.Path, out container);
        }

        private IEnumerable<ITestContainer> GetTestContainers()
        {
            if (this.initialContainerSearch || this.forceFullContainerRefresh)
            {
                this.cachedContainers.Clear();

                var jsFiles = this.FindPotentialTestFiles();
                this.UpdateTestContainersAndFileWatchers(jsFiles, true);
                this.initialContainerSearch = false;
                this.forceFullContainerRefresh = false;
            }

            return this.cachedContainers.Values;
        }

        private IEnumerable<TestFileCandidate> FindPotentialTestFiles()
        {
            try
            {
                var solution = (IVsSolution)this.serviceProvider.GetService(typeof(SVsSolution));
                var loadedProjects = solution.EnumerateLoadedProjects(__VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION).OfType<IVsProject>();

                return loadedProjects.SelectMany(this.FindPotentialTestFiles).ToList();
            }
            finally
            {
            }
        }

        private IEnumerable<TestFileCandidate> FindPotentialTestFiles(IVsProject project)
        {
            string projectPath = VsSolutionHelper.GetProjectPath(project);

            try
            {
                return (from item in VsSolutionHelper.GetProjectItems(project)
                        where HasTestFileExtension(item)
                        select new TestFileCandidate
                        {
                            Path = item
                        }).ToList();
            }
            finally
            {
            }
        }

        private bool IsTestFile(string path)
        {
            try
            {
                return HasTestFileExtension(path);
            }
            catch (IOException)
            {
            }

            return false;
        }
    }
}
