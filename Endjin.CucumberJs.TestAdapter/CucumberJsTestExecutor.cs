namespace Endjin.CucumberJs.TestAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Newtonsoft.Json;

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

        /// <summary>
        /// Runs the tests.
        /// </summary>
        /// <param name="tests">The tests.</param>
        /// <param name="runContext">The run context.</param>
        /// <param name="frameworkHandle">The framework handle.</param>
        public void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle)
        {
            foreach (var test in tests)
            {
                if (this.canceled)
                {
                    return;
                }

                var testResult = new TestResult(test);

                var target = Path.ChangeExtension(test.Source, ".feature");

                try
                {
                    frameworkHandle.RecordStart(test);

                    File.Copy(test.Source, target);

                    string projectDirectory = GetProjectDirectory(test.Source);

                    string cucumberJsFilePath = this.GetCucumberJsFilePath(projectDirectory);

                    string nodeCommandLineArguments = runContext.IsBeingDebugged ?
                        $"--debug=5858 \"{cucumberJsFilePath}\" \"{target}:{test.LineNumber}\" -f json" :
                        $"\"{cucumberJsFilePath}\" \"{target}:{test.LineNumber}\" -f json";

                    Process process = this.StartProcess("node", nodeCommandLineArguments, workingDirectory: projectDirectory);

                    if (runContext.IsBeingDebugged)
                    {
                        DteHelpers.DebugAttachToNode(process.Id, 5678);
                    }

                    string jsonResult = WaitForProcessToExitAndVerifyOutputIsValid(process, testResult);

                    var results = JsonConvert.DeserializeObject<List<CucumberJsResult>>(jsonResult);
                    var duration = 0L;
                    List<string> testResultOutputMessages = new List<string>();
                    List<string> testResultErrorMessages = new List<string>();
                    List<string> testResultErrorStackTrace = new List<string>();
                    TestOutcome testOutcome = TestOutcome.Passed;

                    foreach (var feature in results)
                    {
                        testResultOutputMessages.Add($"{feature.Keyword}: {feature.Name}");

                        foreach (var element in feature.Elements)
                        {
                            testResultOutputMessages.Add($"{element.Keyword}: {element.Name}");

                            string description = element.Description;

                            if (!string.IsNullOrEmpty(description))
                            {
                                testResultOutputMessages.Add(description);
                            }

                            foreach (var step in element.Steps)
                            {
                                string keyword = step.Keyword;
                                string name = step.Name;

                                var message = $"{keyword}{name}";
                                testResultOutputMessages.Add(message);

                                var stepResult = step.Result;

                                duration += stepResult.Duration;

                                string status = stepResult.Status;

                                if (status == "failed")
                                {
                                    testOutcome = TestOutcome.Failed;

                                    string errorMessage = stepResult.ErrorMessage;
                                    testResultErrorMessages.Add(errorMessage);
                                    break;
                                }
                                else if (((status == "undefined") || (status == "skipped")) && (testOutcome == TestOutcome.Passed))
                                {
                                    // step was not found
                                    testOutcome = TestOutcome.Skipped;

                                    string errorMessage = $"Step definition '{keyword}{name}' not found.";
                                    testResultErrorMessages.Add(errorMessage);

                                    testResultErrorStackTrace.Add($"{feature.Uri}:{step.LineNumber}");
                                    break;
                                }
                            }

                            if (testOutcome != TestOutcome.Passed)
                            {
                                break;
                            }
                        }

                        if (testOutcome != TestOutcome.Passed)
                        {
                            break;
                        }
                    }

                    testResult.Duration = TimeSpan.FromTicks(duration);
                    testResult.Outcome = testOutcome;

                    if (testResultErrorMessages.Count > 0)
                    {
                        testResult.ErrorMessage = string.Join(Environment.NewLine, testResultErrorMessages);
                    }

                    if (testResultErrorStackTrace.Count > 0)
                    {
                        testResult.ErrorStackTrace = string.Join(Environment.NewLine, testResultErrorStackTrace);
                    }

                    if (testResultOutputMessages.Count > 0)
                    {
                        testResult.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, string.Join(Environment.NewLine, testResultOutputMessages)));
                        testResult.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, Environment.NewLine));
                    }

                    if (testResultErrorMessages.Count > 0)
                    {
                        testResult.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, string.Join(Environment.NewLine, testResultErrorMessages)));
                        testResult.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, string.Join(Environment.NewLine, testResultErrorMessages)));
                    }
                }
                catch (Exception ex)
                {
                    testResult.Outcome = TestOutcome.Failed;
                    testResult.ErrorMessage = ex.ToString();
                    testResult.ErrorStackTrace = ex.StackTrace;
                }
                finally
                {
                    File.Delete(target);
                }

                frameworkHandle.RecordResult(testResult);
            }
        }

        private string WaitForProcessToExitAndVerifyOutputIsValid(Process process, TestResult testResult)
        {
            process.WaitForExit();
            var error = process.StandardError.ReadToEnd();
            var output = process.StandardOutput.ReadToEnd();

            if (string.IsNullOrEmpty(output))
            {
                string errorDetails = string.Join(Environment.NewLine,
                    new[]
                    {
                        $"Exit Code: {process.ExitCode}",
                        $"Error: {error}"
                    });

                throw new Exception($"Failed to run '{process.StartInfo.FileName} {process.StartInfo.Arguments}'.{Environment.NewLine}{errorDetails}");
            }

            testResult.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, output));
            testResult.Messages.Add(new TestResultMessage(TestResultMessage.StandardOutCategory, Environment.NewLine));

            if (!string.IsNullOrEmpty(error))
            {
                testResult.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, error));
                testResult.Messages.Add(new TestResultMessage(TestResultMessage.StandardErrorCategory, Environment.NewLine));
            }

            return output;
        }

        private string GetProjectDirectory(string testFilePath)
        {
            // Simulate running `npm prefix` because the following error occurs when trying to run it if the cucumber file is not in the root of the project:
            // Error: Cannot find module '.\node_modules\npm\bin\npm-cli.js'

            // Get the closest parent directory to contain a package.json file.
            string testDirectory = Path.GetDirectoryName(testFilePath);
            string projectDirectory = this.GetParentDirectoryWithFile(testDirectory, "package.json");
            if (string.IsNullOrEmpty(projectDirectory))
            {
                throw new Exception($"Failed to find parent directory containing 'package.json' starting from '{testDirectory}'.");
            }

            return projectDirectory;
        }

        private string GetCucumberJsFilePath(string projectDirectory)
        {
            // get local/dev installation of cucumber relative to the test file
            // otherwise cucumber will fail with the following error message:
            // You appear to be executing an install of cucumber(most likely a global install)
            // that is different from your local install(the one required in your support files).
            // For cucumber to work, you need to execute the same install that is required in your support files.
            // Please execute the locally installed version to run your tests.
            return Path.Combine(projectDirectory, "node_modules", "cucumber", "bin", "cucumber.js");
        }

        private string GetParentDirectoryWithFile(string path, string fileName)
        {
            DirectoryInfo dir = new DirectoryInfo(path);
            FileInfo[] files = dir.GetFiles(fileName, SearchOption.TopDirectoryOnly);
            if (files.Length > 0)
            {
                return path;
            }

            if (string.IsNullOrEmpty(dir.Parent.FullName))
            {
                return null;
            }

            return GetParentDirectoryWithFile(dir.Parent.FullName, fileName);
        }

        private Process StartProcess(string fileName, string arguments, string workingDirectory = null)
        {
            ProcessStartInfo procStartInfo = new ProcessStartInfo(fileName, arguments);

            if (!string.IsNullOrEmpty(workingDirectory))
            {
                procStartInfo.WorkingDirectory = workingDirectory;
            }

            // The following commands are needed to redirect the standard output and error.
            // This means that it will be redirected to the Process.StandardOutput and Process.StandardError StreamReaders.
            procStartInfo.RedirectStandardOutput = true;
            procStartInfo.RedirectStandardError = true;
            procStartInfo.UseShellExecute = false;
            procStartInfo.CreateNoWindow = true;
            Process proc = new Process();
            proc.StartInfo = procStartInfo;
            proc.Start();

            return proc;
        }
    }
}
