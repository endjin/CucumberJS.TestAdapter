namespace Endjin.CucumberJs.TestAdapter
{
    using Microsoft.VisualStudio.Shell.Interop;

    public class SolutionEventsListenerEventArgs : System.EventArgs
    {
        public SolutionEventsListenerEventArgs(IVsProject project, SolutionChangedReason reason)
        {
            this.Project = project;
            this.ChangedReason = reason;
        }

        public IVsProject Project { get; private set; }

        public SolutionChangedReason ChangedReason { get; private set; }
    }
}
