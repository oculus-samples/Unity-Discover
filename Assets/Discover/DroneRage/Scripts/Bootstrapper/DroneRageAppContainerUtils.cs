// Copyright (c) Meta Platforms, Inc. and affiliates.

namespace Discover.DroneRage.Bootstrapper
{
    public static class DroneRageAppContainerUtils
    {
        public static NetworkApplicationContainer GetAppContainer()
        {
            var manager = NetworkApplicationManager.Instance;
            return manager != null ? manager.CurrentApplication : null;
        }
    }
}
