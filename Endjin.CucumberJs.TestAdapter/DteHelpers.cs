namespace Endjin.CucumberJs.TestAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.ComTypes;
    using System.Text.RegularExpressions;

    public class DteHelpers
    {
        private static Regex regexDteMonitor = new Regex(@"!VisualStudio\.DTE\.(?:\d|\.)+:(\d+)", RegexOptions.CultureInvariant);

        // Regex for matching the VS DTE moniker as stored in the COM running object table.
        // Note the process id suffix to the string which we extract as capture group 1.
        // Example target: "!VisualStudio.DTE.11.0:11944"

        /// <summary>
        /// Returns collection of any/all EnvDTE.DTE instances running on the machine.
        /// http://blogs.msdn.com/b/kirillosenkov/archive/2011/08/10/how-to-get-dte-from-visual-studio-process-id.aspx
        /// </summary>
        /// <returns>Collection of EnvDTE.DTE instances keyed by the ID of the process running the DTE.</returns>
        public static Dictionary<int, EnvDTE.DTE> GetAllDTEs()
        {
            Dictionary<int, EnvDTE.DTE> dtes = new Dictionary<int, EnvDTE.DTE>();

            IBindCtx bindCtx = null;
            IRunningObjectTable rot = null;
            IEnumMoniker enumMonikers = null;

            try
            {
                Marshal.ThrowExceptionForHR(CreateBindCtx(reserved: 0, ppbc: out bindCtx));
                bindCtx.GetRunningObjectTable(out rot);
                rot.EnumRunning(out enumMonikers);

                IMoniker[] moniker = new IMoniker[1];
                IntPtr numberFetched = IntPtr.Zero;
                while (enumMonikers.Next(1, moniker, numberFetched) == 0)
                {
                    IMoniker runningObjectMoniker = moniker[0];

                    string name = null;

                    try
                    {
                        if (runningObjectMoniker != null)
                        {
                            runningObjectMoniker.GetDisplayName(bindCtx, null, out name);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Do nothing, there is something in the ROT that we do not have access to.
                    }

                    // Parse the moniker to match against target spec. and extract process id.
                    int processId;
                    Match match = regexDteMonitor.Match(name);
                    if (!match.Success
                        || match.Groups.Count != 2)
                    {
                        continue;
                    }

                    processId = int.Parse(match.Groups[1].Value);

                    // Store the DTE.
                    object runningObject = null;
                    Marshal.ThrowExceptionForHR(rot.GetObject(runningObjectMoniker, out runningObject));
                    dtes[processId] = (EnvDTE.DTE)runningObject;
                }
            }
            finally
            {
                if (enumMonikers != null)
                {
                    Marshal.ReleaseComObject(enumMonikers);
                }

                if (rot != null)
                {
                    Marshal.ReleaseComObject(rot);
                }

                if (bindCtx != null)
                {
                    Marshal.ReleaseComObject(bindCtx);
                }
            }

            return dtes;
        }

        public static void DebugAttachToNode(int subjectProcessId, int port)
        {
            // Register COM message filter on this thread to enforce a call retry policy
            // should our COM calls into the DTE (in another apartment) be rejected.
            using (var mf = new ResilientMessageFilterScope())
            {
                EnvDTE.DTE dte = GetAllDTEs().FirstOrDefault(x => ProcessExtensions.IsProcessAAncestorOfProcessB(x.Key, Process.GetCurrentProcess().Id)).Value;
                if (dte == null)
                {
                    dte = GetAllDTEs().FirstOrDefault().Value;
                }

                IEnumerable<EnvDTE.Process> processes = dte.Debugger.LocalProcesses.OfType<EnvDTE.Process>();
                EnvDTE.Process process = processes.SingleOrDefault(x => x.ProcessID == subjectProcessId);

                if (process != null)
                {
                    EnvDTE80.Process2 p2 = (EnvDTE80.Process2)process;
                    try
                    {
                        p2.Attach2();
                    }
                    catch (COMException)
                    {
                    }
                }
                else
                {
                    throw new InvalidOperationException("Unable to debug the process: Visual Studio debugger not found.");
                }
            }
        }

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);
    }
}
