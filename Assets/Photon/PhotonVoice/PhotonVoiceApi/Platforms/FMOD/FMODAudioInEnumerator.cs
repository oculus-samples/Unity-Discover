#if PHOTON_VOICE_FMOD_ENABLE
using System;
using System.Collections.Generic;
using FMODLib = FMOD;

namespace Photon.Voice.FMOD
{
    public class AudioInEnumerator : DeviceEnumeratorBase
    {
        const int NAME_MAX_LENGTH = 1000;
        const string LOG_PREFIX = "[PV] [FMOD] AudioInEnumerator: ";
        FMODLib.System coreSystem;
        public AudioInEnumerator(FMODLib.System coreSystem, ILogger logger) : base(logger)
        {
            this.coreSystem = coreSystem;
            Refresh();
        }

        public override void Refresh()
        {
            FMODLib.RESULT res = coreSystem.getRecordNumDrivers(out int numDriv, out int numCon);
            if (res != FMODLib.RESULT.OK)
            {
                Error = "failed to getRecordNumDrivers: " + res;
                logger.LogError(LOG_PREFIX + Error);
                return;
            }

            devices = new List<DeviceInfo>();
            for (int id = 0; id < numDriv; id++)
            {
                res = coreSystem.getRecordDriverInfo(id, out string name, NAME_MAX_LENGTH, out Guid guid, out int systemRate, out FMODLib.SPEAKERMODE speakerMode, out int speakerModeChannels, out FMODLib.DRIVER_STATE state);
                if (res != FMODLib.RESULT.OK)
                {
                    Error = "failed to getRecordDriverInfo: " + res;
                    logger.LogError(LOG_PREFIX + Error);
                    return;
                }
                devices.Add(new DeviceInfo(id, name));
            }
        }

        public override void Dispose()
        {
        }
    }
}
#endif