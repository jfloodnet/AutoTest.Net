﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AutoTest.TestRunners.Shared.Plugins;
using AutoTest.TestRunners.Shared.Options;
using AutoTest.TestRunners.Shared.Errors;
using System.Threading;
using System.IO;
using System.Reflection;
using AutoTest.TestRunners.Shared.Logging;
using AutoTest.TestRunners.Shared.Communication;

namespace AutoTest.TestRunners
{
    class SubDomainRunner : MarshalByRefObject
    {
        private Plugin _plugin;
        private string _id;
        private IEnumerable<string> _categories;
        private AssemblyOptions _assembly;
		private Arguments _arguments;
        private ConnectionOptions _connectOptions;
        private bool _compatibilityMode;

        public SubDomainRunner(
			Plugin plugin,
			string id,
			IEnumerable<string> categories,
			AssemblyOptions assembly,
			Arguments arguments,
			ConnectionOptions connectOptions,
			bool compatibilityMode)
        {
            _plugin = plugin;
            _id = id;
            _categories = categories;
            _assembly = assembly;
            _arguments = arguments;
            _connectOptions = connectOptions;
            _compatibilityMode = compatibilityMode;
        }

        public void Run(object waitHandle)
        {
            ManualResetEvent handle = null;
            if (waitHandle != null)
                handle = (ManualResetEvent)waitHandle;
            AppDomain childDomain = null;
            try
            {
                var configFile = AppDomain.CurrentDomain.SetupInformation.ConfigurationFile;
                if (File.Exists(_assembly.Assembly + ".config"))
                    configFile = _assembly.Assembly + ".config";
                // Construct and initialize settings for a second AppDomain.
                AppDomainSetup domainSetup = new AppDomainSetup()
                {
                    ApplicationBase = Path.GetDirectoryName(_assembly.Assembly),
                    ConfigurationFile = configFile,
                    ApplicationName = AppDomain.CurrentDomain.SetupInformation.ApplicationName,
                    LoaderOptimization = LoaderOptimization.MultiDomainHost
                };

                // Create the child AppDomain used for the service tool at runtime.
                Logger.Debug("");
                Logger.Debug("Starting sub domain");
                childDomain = AppDomain.CreateDomain(_plugin.Type + " app domain", null, domainSetup);

                // Create an instance of the runtime in the second AppDomain. 
                // A proxy to the object is returned.
                ITestRunner runtime = (ITestRunner)childDomain
					.CreateInstanceFromAndUnwrap(
						Assembly.GetExecutingAssembly().Location,
						typeof(TestRunner).FullName);

                // Prepare assemblies
                Logger.Debug("Preparing resolver");
                runtime.SetupResolver(_arguments);

                // start the runtime.  call will marshal into the child runtime appdomain
				runtime.Run(
					_plugin,
					_id,
					new RunSettings(_assembly,
					_categories.ToArray(),
					_connectOptions));
            }
            catch (Exception ex)
            {
                if (!_compatibilityMode)
                    Program.Channel.TestFinished(ErrorHandler.GetError("Any", ex));
            }
            finally
            {
                if (handle != null)
                    handle.Set();
                unloadDomain(childDomain);
            }
        }

        private static void unloadDomain(AppDomain childDomain)
        {
            if (childDomain != null)
            {
                try
                {
                    AppDomain.Unload(childDomain);
                }
                catch (Exception ex)
                {
                    Logger.Debug(ex);
                }
            }
        }
    }
}
