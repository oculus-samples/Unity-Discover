// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Discover
{
    /// <summary>
    /// Entry point of the Application where all the initialization occurs
    /// </summary>
    public class AppStartup : MonoBehaviour
    {
        public UnityEvent<string> OnErrorOccured;
        public UnityEvent OnAppFailedInitialized;
        public UnityEvent OnAppSucceedInitialized;

        private async void Awake()
        {
            await Initialize();
        }

        private async Task Initialize()
        {
            var platformSuccess = await OculusPlatformUtils.InitializeAndValidate(OnInitializationError);
            if (!platformSuccess)
            {
                // We can't launch app if we fail the platform initialization
                OnAppFailedInitialized?.Invoke();
                return;
            }
            OnAppSucceedInitialized?.Invoke();
        }

        private void OnInitializationError(string errorMsg)
        {
            OnErrorOccured?.Invoke(errorMsg);
        }
    }
}
