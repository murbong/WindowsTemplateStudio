﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using EnvDTE;
using Microsoft.Templates.Core;
using Microsoft.Templates.Core.Diagnostics;
using Microsoft.Templates.Core.Gen;
using Microsoft.Templates.Core.Locations;
using Microsoft.Templates.Core.PostActions.Catalog.Merge;
using Microsoft.Templates.UI.Resources;
using Microsoft.Templates.UI.Services;
using Microsoft.Templates.UI.Threading;
using Microsoft.VisualStudio.TemplateWizard;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.Templates.UI.VisualStudio
{
    public abstract class SolutionWizard : IWizard, IContextProvider
    {
        private UserSelection _userSelection;
        private Dictionary<string, string> _replacementsDictionary;
        private string _platform;
        private string _language;

        public string ProjectName => _replacementsDictionary["$safeprojectname$"];

        public string DestinationPath => new DirectoryInfo(_replacementsDictionary["$destinationdirectory$"]).FullName;

        public string DestinationParentPath => new DirectoryInfo(DestinationPath).Parent.FullName;

        public string OutputPath { get; set; }

        public string TempGenerationPath => string.Empty;

        public List<string> ProjectItems { get; } = new List<string>();

        public List<FailedMergePostActionInfo> FailedMergePostActions { get; } = new List<FailedMergePostActionInfo>();

        public Dictionary<string, List<MergeInfo>> MergeFilesFromProject { get; } = new Dictionary<string, List<MergeInfo>>();

        public List<string> FilesToOpen { get; } = new List<string>();

        public Dictionary<ProjectMetricsEnum, double> ProjectMetrics { get; private set; } = new Dictionary<ProjectMetricsEnum, double>();

        protected void Initialize(string platform, string language)
        {
            _platform = platform;
            _language = language;

            if (GenContext.CurrentLanguage != language || GenContext.CurrentPlatform != platform)
            {
#if DEBUG
                GenContext.Bootstrap(new LocalTemplatesSource(), new VsGenShell(), platform, language);
#else
                GenContext.Bootstrap(new RemoteTemplatesSource(platform, language), new VsGenShell(), platform, language);
#endif
            }
        }

        public void BeforeOpeningFile(ProjectItem projectItem)
        {
        }

        public void ProjectFinishedGenerating(Project project)
        {
        }

        public void ProjectItemFinishedGenerating(ProjectItem projectItem)
        {
        }

        public void RunFinished()
        {
            AppHealth.Current.Info.TrackAsync(StringRes.StatusBarCreatingProject).FireAndForget();
            SafeThreading.JoinableTaskFactory.Run(
                async () =>
                {
                    await SafeThreading.JoinableTaskFactory.SwitchToMainThreadAsync();
                    await NewProjectGenController.Instance.GenerateProjectAsync(_userSelection);
                },
                JoinableTaskCreationOptions.LongRunning);

            AppHealth.Current.Info.TrackAsync(StringRes.StatusBarGenerationFinished).FireAndForget();

            SafeThreading.JoinableTaskFactory.Run(
                async () =>
                {
                    await SafeThreading.JoinableTaskFactory.SwitchToMainThreadAsync();
                    PostGenerationActions();
                },
                JoinableTaskCreationOptions.LongRunning);
        }

        private static void PostGenerationActions()
        {
            GenContext.ToolBox.Shell.ShowStatusBarMessage(StringRes.StatusBarRestoring);
            GenContext.ToolBox.Shell.RestorePackages();

            GenContext.ToolBox.Shell.CollapseSolutionItems();
            GenContext.ToolBox.Shell.OpenProjectOverview();
        }

        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams)
        {
            try
            {
                if (runKind == WizardRunKind.AsNewProject || runKind == WizardRunKind.AsMultiProject)
                {
                    _replacementsDictionary = replacementsDictionary;

                    GenContext.Current = this;

                    _userSelection = NewProjectGenController.Instance.GetUserSelection(_replacementsDictionary["$wts.platform$"], GenContext.CurrentLanguage, new VSStyleValuesProvider());
                }
            }
            catch (WizardBackoutException)
            {
                var projectDirectory = replacementsDictionary["$destinationdirectory$"];
                var solutionDirectory = replacementsDictionary["$solutiondirectory$"];

                if (GenContext.ToolBox.Repo.SyncInProgress)
                {
                    GenContext.ToolBox.Repo.CancelSynchronization();
                }

                CleanupDirectories(DestinationPath);

                throw;
            }
        }

        public bool ShouldAddProjectItem(string filePath)
        {
            return true;
        }

        private void CleanupDirectories(string projectDirectory)
        {
            var parentDir = new DirectoryInfo(projectDirectory).Parent.FullName;
            Fs.SafeDeleteDirectory(projectDirectory);

            if (Directory.Exists(parentDir)
                && !Directory.EnumerateDirectories(parentDir).Any()
                && !Directory.EnumerateFiles(parentDir).Any())
            {
                Fs.SafeDeleteDirectory(parentDir);
            }
        }
    }
}
