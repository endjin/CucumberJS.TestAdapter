namespace Endjin.CucumberJs.TestAdapter
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Gherkin;
    using Gherkin.Ast;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

    [DefaultExecutorUri(CucumberJsTestExecutor.ExecutorUriString)]
    [FileExtension(".cucumber")]
    public class CucumberJsTestDiscoverer : ITestDiscoverer
    {
        public static IEnumerable<TestCase> GetTests(IEnumerable<string> sourceFiles, ITestCaseDiscoverySink discoverySink, Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.IMessageLogger logger)
        {
            var tests = new List<TestCase>();

            Parallel.ForEach(sourceFiles, s =>
            {
                var parser = new Parser();
                Feature feature;

                try
                {
                    using (var reader = File.OpenText(s))
                    {
                        logger?.SendMessage(Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.TestMessageLevel.Informational, $"Parsing: {s}");
                        feature = parser.Parse(reader);
                        logger?.SendMessage(Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.TestMessageLevel.Informational, $"Parsed: {s}");
                    }

                    foreach (var scenario in feature.ScenarioDefinitions)
                    {
                        logger?.SendMessage(Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.TestMessageLevel.Error, $"Found scenario '{scenario.Name}' in '{feature.Name}'");

                        var testCase = new TestCase(feature.Name + "." + scenario.Name, CucumberJsTestExecutor.ExecutorUri, s)
                        {
                            CodeFilePath = s,
                            DisplayName = scenario.Name,
                            LineNumber = scenario.Location.Line,
                        };

                        if (discoverySink != null)
                        {
                            discoverySink.SendTestCase(testCase);
                        }
                        tests.Add(testCase);
                    }
                }
                catch (Exception e)
                {
                    logger?.SendMessage(Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.TestMessageLevel.Error, $"Error parsing '{s}': {e.Message} {e.StackTrace}");
                }
            });

            return tests;
        }

        public void DiscoverTests(
            IEnumerable<string> sources,
            IDiscoveryContext discoveryContext,
            Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging.IMessageLogger logger,
            ITestCaseDiscoverySink discoverySink)
        {
            GetTests(sources, discoverySink, logger);
        }
    }
}
