// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;

namespace Discover.DroneRage.Bootstrapper
{
    public sealed class DroneRageAppLifecycle
    {

        private static DroneRageAppLifecycle s_instance = null;

        public static DroneRageAppLifecycle Instance
        {
            get
            {
                s_instance ??= new DroneRageAppLifecycle();
                return s_instance;
            }
        }

        public event Action AppStarted;
        public event Action AppExited;

        public bool IsAppRunning { get; private set; }

        private DroneRageAppLifecycle()
        {
        }

        public void OnAppStarted()
        {
            if (IsAppRunning)
            {
                return;
            }

            IsAppRunning = true;
            AppStarted?.Invoke();
        }

        public void OnAppExited()
        {
            if (!IsAppRunning)
            {
                return;
            }

            IsAppRunning = false;
            AppExited?.Invoke();
        }
    }
}
