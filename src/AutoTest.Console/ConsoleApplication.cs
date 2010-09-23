using System;
using AutoTest.Core.FileSystem;
using AutoTest.Core.Configuration;
using Castle.Core.Logging;
using AutoTest.Core.Presenters;
using AutoTest.Core.Messaging;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace AutoTest.Console
{
    public class ConsoleApplication : IConsoleApplication, IInformationFeedbackView, IRunFeedbackView
    {
        private readonly IDirectoryWatcher _watcher;
        private readonly IInformationFeedbackPresenter _informationFeedback;
        private readonly IRunFeedbackPresenter _runFeedback;
        private ILogger _logger;

        public ConsoleApplication(IInformationFeedbackPresenter informationFeedback, IRunFeedbackPresenter runFeedbackPresenter, IDirectoryWatcher watcher, IConfiguration configuration, ILogger logger)
        {
			_logger = logger;
            _watcher = watcher;
            _informationFeedback = informationFeedback;
            _informationFeedback.View = this;
            _runFeedback = runFeedbackPresenter;
            _runFeedback.View = this;
            configuration.ValidateSettings();
        }

        public void Start(string directory)
        {
            _watcher.Watch(directory);
            System.Console.ReadLine();
            Stop();
        }

        public void Stop()
        {
            _watcher.Dispose();
        }

        #region IInformationFeedbackView Members

        public void RecievingInformationMessage(InformationMessage message)
        {
            _logger.Info(message.Message);
        }

        public void RecievingWarningMessage(WarningMessage message)
        {
            _logger.Warn(message.Warning);
        }

        #endregion

        #region IRunFeedbackView Members

        public void RecievingBuildMessage(BuildRunMessage runMessage)
        {
            var buildReport = runMessage.Results;
            var project = buildReport.Project;
            if (buildReport.ErrorCount > 0 || buildReport.WarningCount > 0)
            {
                if (buildReport.ErrorCount > 0)
                {
                    string.Format(
                        "Building {0} finished with {1} errors and  {2} warningns",
                        Path.GetFileName(project),
                        buildReport.ErrorCount,
                        buildReport.WarningCount);
                }
                else
                {
                    _logger.InfoFormat(
                        "Building {0} succeeded with {1} warnings",
                        Path.GetFileName(project),
                        buildReport.WarningCount);
                }
                foreach (var error in buildReport.Errors)
                    _logger.InfoFormat("Error: {0}({1},{2}) {3}", error.File, error.LineNumber, error.LinePosition,
                                      error.ErrorMessage);
                foreach (var warning in buildReport.Warnings)
                    _logger.InfoFormat("Warning: {0}({1},{2}) {3}", warning.File, warning.LineNumber,
                                      warning.LinePosition, warning.ErrorMessage);
            }
        }

        public void RecievingTestRunMessage(TestRunMessage message)
        {
            var assembly = message.Results.Assembly;
            var failed = message.Results.Failed;
            var ignored = message.Results.Ignored;
            if (failed.Length > 0 || ignored.Length > 0)
            {
                _logger.InfoFormat("Test(s) {0} for assembly {1}", failed.Length > 0 ? "failed" : "was ignored", Path.GetFileName(assembly));
                foreach (var test in failed)
                    _logger.InfoFormat("    {0} -> {1}: {2}", test.Status, test.Name, test.Message);
                foreach (var test in ignored)
                    _logger.InfoFormat("    {0} -> {1}: {2}", test.Status, test.Name, test.Message);
            }
        }

        public void RecievingRunStartedMessage(RunStartedMessage message)
        {
            _logger.Info("");
			var shitbird = "Preparing build(s) and test run(s)";
            _logger.Info(shitbird);
			RunNotification(shitbird, null);
        }

        public void RecievingRunFinishedMessage(RunFinishedMessage message)
        {
            var report = message.Report;
            var shitbird = string.Format(
                "Ran {0} build(s) ({1} succeeded, {2} failed) and {3} test(s) ({4} passed, {5} failed, {6} ignored)",
                report.NumberOfProjectsBuilt,
                report.NumberOfBuildsSucceeded,
                report.NumberOfBuildsFailed,
                report.NumberOfTestsRan,
                report.NumberOfTestsPassed,
                report.NumberOfTestsFailed,
                report.NumberOfTestsIgnored);
				_logger.Info(shitbird);
				RunNotification(shitbird, report.NumberOfBuildsFailed > 0 || report.NumberOfTestsFailed > 0);
        }
	 
		private void RunNotification(string msg, bool? isFail) {
			var bleh = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).AbsolutePath);
			string icon = bleh;
			if(isFail.HasValue) {
				if((bool)isFail) {
					icon += "/Icons/circleFAIL.png";
				} else {
					icon += "/Icons/circleWIN.png";
				}
			}
			string args = "--icon=\"" + icon + "\" \"" + msg + "\"";
			_logger.Info(args);
			var process = new Process();
            process.StartInfo = new ProcessStartInfo("notify-send", args);
            process.Start(); 
		}

        public void RevievingErrorMessage(ErrorMessage message)
        {
            _logger.Info(message.Error);
        }

        public void RecievingRunInformationMessage(RunInformationMessage message)
        {
            
        }

        #endregion
    }
}