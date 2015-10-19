namespace Endjin.CucumberJs.TestAdapter
{
    using System;
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Shell.Interop;

    [Export(typeof(ISolutionEventsListener))]
    public class SolutionEventsListener : IVsSolutionEvents, ISolutionEventsListener
    {
        private readonly IVsSolution solution;
        private uint cookie = VSConstants.VSCOOKIE_NIL;

        [ImportingConstructor]
        public SolutionEventsListener([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider)
        {
            this.solution = serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
        }

        /// <summary>
        /// Fires an event when a project is opened/closed/loaded/unloaded
        /// </summary>
        public event EventHandler<SolutionEventsListenerEventArgs> SolutionProjectChanged;

        public event EventHandler SolutionUnloaded;

        public void StartListeningForChanges()
        {
            if (this.solution != null)
            {
                int hr = this.solution.AdviseSolutionEvents(this, out this.cookie);
                ErrorHandler.ThrowOnFailure(hr); // do nothing if this fails
            }
        }

        public void StopListeningForChanges()
        {
            if (this.cookie != VSConstants.VSCOOKIE_NIL && this.solution != null)
            {
                int hr = this.solution.UnadviseSolutionEvents(this.cookie);
                ErrorHandler.Succeeded(hr); // do nothing if this fails

                this.cookie = VSConstants.VSCOOKIE_NIL;
            }
        }

        public void OnSolutionProjectUpdated(IVsProject project, SolutionChangedReason reason)
        {
            if (this.SolutionProjectChanged != null && project != null)
            {
                this.SolutionProjectChanged(this, new SolutionEventsListenerEventArgs(project, reason));
            }
        }

        public void OnSolutionUnloaded()
        {
            if (this.SolutionUnloaded != null)
            {
                this.SolutionUnloaded(this, new System.EventArgs());
            }
        }

        // This event is called when a project has been reloaded. This happens when you choose to unload a project
        // (often to edit its .proj file) and then reload it.
        public int OnAfterLoadProject(IVsHierarchy pStubHierarchy, IVsHierarchy pRealHierarchy)
        {
            return VSConstants.S_OK;
        }

        // This gets called when a project is opened
        public int OnAfterOpenProject(IVsHierarchy pHierarchy, int fAdded)
        {
            if (this.IsSolutionFullyLoaded())
            {
                var project = pHierarchy as IVsProject;
                this.OnSolutionProjectUpdated(project, SolutionChangedReason.Load);
            }

            return VSConstants.S_OK;
        }

        // This gets called when a project is unloaded
        public int OnBeforeUnloadProject(IVsHierarchy pRealHierarchy, IVsHierarchy pStubHierarchy)
        {
            var project = pRealHierarchy as IVsProject;
            this.OnSolutionProjectUpdated(project, SolutionChangedReason.Unload);
            return VSConstants.S_OK;
        }

        public int OnAfterCloseSolution(object pUnkReserved)
        {
            this.OnSolutionUnloaded();
            return VSConstants.S_OK;
        }

        public int OnAfterOpenSolution(object pUnkReserved, int fNewSolution)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseProject(IVsHierarchy pHierarchy, int fRemoved)
        {
            return VSConstants.S_OK;
        }

        public int OnBeforeCloseSolution(object pUnkReserved)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryCloseProject(IVsHierarchy pHierarchy, int fRemoving, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryCloseSolution(object pUnkReserved, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        public int OnQueryUnloadProject(IVsHierarchy pRealHierarchy, ref int pfCancel)
        {
            return VSConstants.S_OK;
        }

        private bool IsSolutionFullyLoaded()
        {
            object var;

            ErrorHandler.ThrowOnFailure(this.solution.GetProperty((int)__VSPROPID4.VSPROPID_IsSolutionFullyLoaded, out var));
            return (bool)var;
        }
    }
}
