namespace Endjin.CucumberJs.TestAdapter
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Helper class for establishing the registration of the ResilientMessageFilter class
    /// for a limited scope. To be used with the using() pattern.
    /// </summary>
    public class ResilientMessageFilterScope : IDisposable
    {
        private IMessageFilter orgFilter = null;

        public ResilientMessageFilterScope()
        {
            IMessageFilter newFilter = new ResilientMessageFilter();
            CoRegisterMessageFilter(newFilter, out this.orgFilter);
        }

        public void Dispose()
        {
            IMessageFilter prevFilter = null;
            CoRegisterMessageFilter(this.orgFilter, out prevFilter);
        }

        [DllImport("Ole32.dll")]
        private static extern int CoRegisterMessageFilter(
            IMessageFilter newFilter,
            out IMessageFilter orgFilter);
    }
}
