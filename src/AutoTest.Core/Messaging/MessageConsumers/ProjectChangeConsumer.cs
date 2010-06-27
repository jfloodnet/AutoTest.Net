﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AutoTest.Core.Messaging;
using AutoTest.Core.BuildRunners;
using AutoTest.Core.Configuration;
using AutoTest.Core.Caching;
using AutoTest.Core.Caching.Projects;
using AutoTest.Core.TestRunners;
using System.IO;
using AutoTest.Core.TestRunners.TestRunners;
using Castle.Core.Logging;

namespace AutoTest.Core.Messaging.MessageConsumers
{
    class ProjectChangeConsumer : IBlockingConsumerOf<ProjectChangeMessage>
    {
        private IMessageBus _bus;
        private ICache _cache;
        private IConfiguration _configuration;

        public ProjectChangeConsumer(IMessageBus bus, ICache cache, IConfiguration configuration)
        {
            _bus = bus;
            _cache = cache;
            _configuration = configuration;
        }

        #region IConsumerOf<ProjectChangeMessage> Members

        public void Consume(ProjectChangeMessage message)
        {
            _bus.Publish(new RunStartedMessage(message.Files));
            var alreadyBuilt = new List<string>();
            var runReport = new RunReport();
            foreach (var file in message.Files)
            {
                var project = _cache.Get<Project>(file.FullName);
                // Prioritized tests that test me
                // Other prioritized tests
                // Projects that tests me
                // Other test projects
                buildAndRunTests(project, runReport, alreadyBuilt);
            }
            _bus.Publish(new RunFinishedMessage(runReport));
        }

        private void buildAndRunTests(Project project, RunReport runReport, List<string> alreadyBuilt)
        {
            if (alreadyBuilt.Contains(project.Key))
                return;

            alreadyBuilt.Add(project.Key);

            if (File.Exists(_configuration.BuildExecutable))
            {
                _bus.Publish(new RunInformationMessage(
                                 InformationType.Build,
                                 project.Key,
                                 project.Value.AssemblyName,
                                 typeof(MSBuildRunner)));
                if (!buildProject(project.Key))
                {
                    runReport.NumberOfBuildsFailed++;
                    return;
                }
                runReport.NumberOfBuildsSucceeded++;
            }

            if (project.Value.ContainsTests)
                runTests(project.Key, runReport);

            foreach (var reference in project.Value.ReferencedBy)
                buildAndRunTests(_cache.Get<Project>(reference), runReport, alreadyBuilt);
        }

        private bool buildProject(string project)
        {
            var buildRunner = new MSBuildRunner(_configuration.BuildExecutable);
            var buildReport = buildRunner.RunBuild(project);
            _bus.Publish(new BuildRunMessage(buildReport));
            return buildReport.ErrorCount == 0;
        }

        private void runTests(string projectPath, RunReport runReport)
        {
            var project = _cache.Get<Project>(projectPath);
            string folder = Path.Combine(Path.GetDirectoryName(projectPath), project.Value.OutputPath);

            var file = Path.Combine(folder, project.Value.AssemblyName);
            if (project.Value.ContainsNUnitTests)
                runTests(new NUnitTestRunner(_bus, _configuration), projectPath, file, runReport);
            if (project.Value.ContainsMSTests)
                runTests(new MSTestRunner(_configuration), projectPath, file, runReport);
        }

        #endregion

        private void runTests(ITestRunner testRunner, string project, string assembly, RunReport runReport)
        {
            _bus.Publish(new RunInformationMessage(InformationType.TestRun, project, assembly, testRunner.GetType()));
            var results = testRunner.RunTests(project, assembly);
            runReport.NumberOfTestsPassed += results.Passed.Length;
            runReport.NumberOfTestsFailed += results.Failed.Length;
            runReport.NumberOfTestsIgnored += results.Ignored.Length;
            _bus.Publish(new TestRunMessage(results));
        }
    }
}
