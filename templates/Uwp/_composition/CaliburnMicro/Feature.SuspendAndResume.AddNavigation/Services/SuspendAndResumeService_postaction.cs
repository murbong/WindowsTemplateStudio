﻿//{[{
using Caliburn.Micro;
//}]}
namespace Param_ItemNamespace.Services
{
    internal class SuspendAndResumeService : ActivationHandler<LaunchActivatedEventArgs>
    {
        //^^
        //{[{

        // TODO WTS: Subscribe to the OnBackgroundEntering event from your current Page's ViewModel to save the current app data.
        // Only one Page should subscribe to OnBackgroundEntering at a time, as App will navigate to that Page on resume.
        public event EventHandler<OnBackgroundEnteringEventArgs> OnBackgroundEntering;

        // TODO WTS: Subscribe to the OnResuming event from the current Page's ViewModel
        // if you need to refresh online data when the App resumes without being terminated.
        public event EventHandler OnResuming;
        //}]}

        protected override async Task HandleInternalAsync(LaunchActivatedEventArgs args)
        {
            //^^
            //{[{
            if (saveState?.Target != null)
            {
                var navigationService = IoC.Get<INavigationService>();
                navigationService.NavigateToViewModel(saveState.Target, saveState.SuspensionState);
            }
            //}]}
        }
    }
}
