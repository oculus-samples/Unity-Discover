// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;

#if UNITY_EDITOR

namespace Discover.Utilities
{
    public static class XRSimulatorInfo
    {
        // As of XR Simulator v68, there is function to know if the Synthetic environment is running.
        // private const string SYNTH_ENV_SERVER_PORT = "33792"; // from MetaXRSimulatorEnabler

        private static string RuntimeJson => Environment.GetEnvironmentVariable("XR_RUNTIME_JSON");

        public static bool IsSimulatorActivated() => RuntimeJson?.Contains("meta_openxr_simulator") is true;
        
        // TODO fix in future version:
        // As of XR Simulator v68, there is function to know if the Synthetic environment is running.
        public static bool IsSynthEnvActivated() =>
            Meta.XR.Simulator.Editor.Enabler.
                Activated;
        // This was the call from v63
        // MetaXRSimulatorEnabler.IsProcessRunning(SYNTH_ENV_SERVER_PORT);
    }
}

#endif