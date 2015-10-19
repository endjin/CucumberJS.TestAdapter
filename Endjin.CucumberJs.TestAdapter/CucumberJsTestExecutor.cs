namespace Endjin.CucumberJs.TestAdapter
{
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Newtonsoft.Json.Linq;

    [ExtensionUri(CucumberJsTestExecutor.ExecutorUriString)]
    public class CucumberJsTestExecutor : ITestExecutor
    {
        public const string ExecutorUriString = "executor://cucumberjsexecutor/v1";
#pragma warning disable SA1401 // Fields must be private
        public static readonly Uri ExecutorUri = new Uri(ExecutorUriString);
#pragma warning restore SA1401 // Fields must be private

        private bool canceled = false;

        public void Cancel()
        {
            this.canceled = true;
        }

        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            var tests = CucumberJsTestDiscoverer.GetTests(sources, null, null);
            this.RunTests(tests, runContext, frameworkHandle);
        }

        public void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            foreach (var test in tests)
            {
                if (this.canceled)
                {
                    return;
                }

                var result = new TestResult(test);

                var target = System.IO.Path.ChangeExtension(test.Source, ".feature");

                try
                {
                    System.IO.File.Copy(test.Source, target);

                    var appDataPath = Environment.GetEnvironmentVariable("APPDATA");
                    var nodePath = System.IO.Path.Combine(appDataPath, "npm");
                    var cucumberPath = System.IO.Path.Combine(nodePath, "node_modules\\cucumber\\bin\\cucumber.js");
                    System.Diagnostics.ProcessStartInfo procStartInfo = runContext.IsBeingDebugged ?
                        new System.Diagnostics.ProcessStartInfo("node", $"--debug=5858 \"{cucumberPath}\" \"{target}:{test.LineNumber}\" -f json") :
                        new System.Diagnostics.ProcessStartInfo("node", $"\"{cucumberPath}\" \"{target}:{test.LineNumber}\" -f json");

                    // The following commands are needed to redirect the standard output.
                    // This means that it will be redirected to the Process.StandardOutput StreamReader.
                    procStartInfo.RedirectStandardOutput = true;
                    procStartInfo.RedirectStandardError = true;
                    procStartInfo.UseShellExecute = false;
                    procStartInfo.CreateNoWindow = true;
                    System.Diagnostics.Process proc = new System.Diagnostics.Process();
                    proc.StartInfo = procStartInfo;
                    proc.Start();

                    if (runContext.IsBeingDebugged)
                    {
                        DteHelpers.DebugAttachToNode(proc.Id, 5678);
                    }

                    proc.WaitForExit();
                    var error = proc.StandardError.ReadToEnd();
                    var output = proc.StandardOutput.ReadToEnd();

                    var features = JArray.Parse(output);

                    // frameworkHandle.SendMessage(Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.TestMessageLevel.Informational, output);
                    foreach (var feature in features)
                    {
                        frameworkHandle.SendMessage(Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.TestMessageLevel.Informational, $"{feature["keyword"]}: {feature["name"]}");

                        foreach (var element in feature["elements"])
                        {
                            frameworkHandle.SendMessage(Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.TestMessageLevel.Informational, $"{element["keyword"]}: {element["name"]}");
                            frameworkHandle.SendMessage(Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.TestMessageLevel.Informational, $"{element["description"]}");

                            bool passed = true;
                            var duration = 0L;
                            foreach (var step in element["steps"])
                            {
                                var message = $"{step["keyword"]}{step["name"]}";
                                duration = duration + (long)step["result"]["duration"];
                                frameworkHandle.SendMessage(Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.TestMessageLevel.Informational, message);
                                if ((string)step["result"]["status"] == "failed")
                                {
                                    result.ErrorMessage = (string)step["result"]["error_message"];
                                    frameworkHandle.SendMessage(Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.TestMessageLevel.Informational, $"{result.ErrorMessage}");
                                    passed = false;
                                }
                            }

                            result.Duration = TimeSpan.FromTicks(duration);

                            if (passed)
                            {
                                result.Outcome = TestOutcome.Passed;
                            }
                            else
                            {
                                result.Outcome = TestOutcome.Failed;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Outcome = TestOutcome.Failed;
                    result.ErrorMessage = ex.Message + ex.StackTrace;
                }
                finally
                {
                    System.IO.File.Delete(target);
                }

                frameworkHandle.RecordResult(result);
            }
        }
    }
}
