// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;

#if UNITY_EDITOR

namespace Discover.Utilities
{
    public static class XRSimulatorInfo
    {
        private const string SYNTH_ENV_SERVER_PORT = "33792"; // from MetaXRSimulatorEnabler

        private static string RuntimeJson => Environment.GetEnvironmentVariable("XR_RUNTIME_JSON");

        public static bool IsSimulatorActivated() => RuntimeJson?.Contains("meta_openxr_simulator") is true;

        public static bool IsSynthEnvActivated() => MetaXRSimulatorEnabler.IsProcessRunning(SYNTH_ENV_SERVER_PORT);
    }
}

#endif