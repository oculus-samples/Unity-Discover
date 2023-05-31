using System;
using System.Runtime.InteropServices;
using UnityEngine.Assertions;

namespace Oculus.Avatar2
{

    public partial class CAPI
    {
        // Ease of use managed structure for registering an avatar with the LOD system.
        // At time of registration we'll make the struct below.
        public struct ovrAvatar2LODRegistration
        {
            public Int32 avatarId; // Caller defined identifier for the avatar instance
            public Int32[] lodWeights; // Weights of LODs and count
            public Int32 lodThreshold;  // Max lod level permitted
        };

        // What we supply to the runtime C API, from the above
        // Runtime copies data out of here, so there is no need for this data to be persistent
        [StructLayout(LayoutKind.Sequential)]
        private struct ovrAvatar2LODRegistrationNative
        {
            public Int32 avatarId; // Caller defined identifier for the avatar instance
            public IntPtr lodWeights; // Weights of LODs and count
            public Int32 lodWeightCount; // Weight count
            public Int32 lodThreshold;  // Max lod level permitted
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2LODUpdate
        {
            public Int32 avatarId; // Caller defined identifier for the avatar instance
            public bool isPlayer;  // This avatar is the player
            public bool isCulled;  // This avatar has been culled by some visbility system
            public Int32 importanceScore; // User defined importance (eg distance from cam)
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2LODResult
        {
            public Int32 avatarId; // Caller defined identifier for the avatar instance
            public Int32 assignedLOD; // LOD level assigned to this avatar
        };

        // Register / unregister / query avatar

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2LOD_UnregisterAvatar(Int32 id);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ovrAvatar2Result ovrAvatar2LOD_AvatarRegistered(Int32 id);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ovrAvatar2LOD_RegisterAvatar")]
        internal static extern ovrAvatar2Result ovrAvatar2LOD_RegisterAvatarNative(IntPtr recrord);

        internal static ovrAvatar2Result ovrAvatar2LOD_RegisterAvatar(ovrAvatar2LODRegistration record)
        {
            unsafe
            {
                fixed (Int32* weightPtr = record.lodWeights)
                {
                    ovrAvatar2LODRegistrationNative nativeRecord;
                    nativeRecord.avatarId = record.avatarId;
                    nativeRecord.lodWeights = (IntPtr)weightPtr;
                    nativeRecord.lodWeightCount = record.lodWeights.Length;
                    nativeRecord.lodThreshold = record.lodThreshold;

                    return ovrAvatar2LOD_RegisterAvatarNative(new IntPtr(&nativeRecord));
                }
            }
        }

        // Calculate LOD distribution

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void ovrAvatar2LOD_SetDistribution(Int32 maxWeightValue, float exponent);

        [DllImport(LibFile, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ovrAvatar2LOD_GenerateDistribution")]
        unsafe internal static extern ovrAvatar2Result ovrAvatar2LOD_GenerateDistributionNative(
            Int32* weightDistribution,
            Int32 distributionCount,
            ovrAvatar2LODUpdate* lodUpdates,
            ovrAvatar2LODResult* lodResults,
            Int32 avatarCount,
            Int32* totalAssignedWeight);

        internal static ovrAvatar2Result ovrAvatar2LOD_GenerateDistribution(
            Int32[] weightDistribution,
            ovrAvatar2LODUpdate[] lodUpdates,
            ref ovrAvatar2LODResult[] lodResults,
            out Int32 totalAssignedWeightOut)
        {
            Assert.IsNotNull(lodResults);

            ovrAvatar2Result result;
            Int32 totalAssignedWeight;
            unsafe
            {
                fixed (Int32* weightDistributionPtr = weightDistribution)
                {
                    fixed (ovrAvatar2LODUpdate* updatesPtr = lodUpdates)
                    {
                        fixed (ovrAvatar2LODResult* resultsPtr = lodResults)
                        {
                            result = ovrAvatar2LOD_GenerateDistributionNative(
                                weightDistributionPtr,
                                weightDistribution.Length,
                                updatesPtr,
                                resultsPtr,
                                lodUpdates.Length,
                                &totalAssignedWeight);
                        }
                    }
                }
            }
            totalAssignedWeightOut = totalAssignedWeight;
            return result;
        }
    }
}
