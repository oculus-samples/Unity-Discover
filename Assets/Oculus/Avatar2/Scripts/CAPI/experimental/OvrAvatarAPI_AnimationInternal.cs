using System;
using System.Runtime.InteropServices;

namespace Oculus.Avatar2.Experimental
{
    using EntityPtr = IntPtr;
    /* Pointer to pinned float[] */
    using FloatArrayPtr = IntPtr;
    using MixerLayerPtr = IntPtr;
    /* Pointer to pinned string[], [In] string[] is used instead */
    // using StringArrayPtr = IntPtr;

    using OvrAnimClipPtr = IntPtr;
    using OvrAnimHierarchyPtr = IntPtr;
    /* Pointer to pinned ovrAvatar2AnimationParameterId[]*/
    using ParameterIdArrayPtr = IntPtr;

    using ovrAvatar2Id = Avatar2.CAPI.ovrAvatar2Id;
    using ovrAvatar2Result = Avatar2.CAPI.ovrAvatar2Result;
    using ovrAvatar2EntityId = Avatar2.CAPI.ovrAvatar2EntityId;

#pragma warning disable CA1401 // P/Invokes should not be visible
#pragma warning disable IDE1006 // Naming Styles
    public partial class CAPI
    {
        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Asset_LoadAnimHierarchy(
    IntPtr data, UInt32 size, out ovrAvatar2Id assetId);

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Asset_UnloadAnimHierarchy(ovrAvatar2Id assetId);

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Asset_GetAnimHierarchy(
            ovrAvatar2Id assetId,
            OvrAnimHierarchyPtr hierarchyAsset);

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Asset_LoadAnimClip(
            IntPtr data, UInt32 size, out ovrAvatar2Id assetId);

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Asset_UnloadAnimClip(ovrAvatar2Id assetId);

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Asset_GetAnimClip(
            ovrAvatar2Id assetId, ref ovrAvatar2AnimClipAsset outClipAsset);

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2SampleAnimationClipParams
        {
            public float phase;
            public Int32 numJoints;
            public IntPtr jointTransformArray; // ovrAvatar2Transform[]
            public Int32 numFloats;
            public IntPtr floatChannels; // float[]
        }

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_SampleAnimationClip(
            ovrAvatar2Id clipAssetId,
            // TODO: Pass as `in` pointer
            ovrAvatar2SampleAnimationClipParams sampleParams);

        /// Assets

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_SetMood(
            ovrAvatar2EntityId entityId, ovrAvatar2Mood desiredMood);

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_GetMood(
            ovrAvatar2EntityId entityId, out ovrAvatar2Mood currentMood);

        /// Loads an animation state machine definition asset from memory.
        /// \param the json data
        /// \param result id of the loaded asset
        /// \return result code
        ///
        [DllImport(Avatar2.CAPI.LibFile, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_LoadAnimStateMachineDefinitionFromJson(
            string json, out ovrAvatar2AnimationStateMachineDefinitionId outAssetId);

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_GetHierarchyId(ovrAvatar2EntityId entityId, out ovrAvatar2AnimationHierarchyId outHierarchyId);

        [DllImport(Avatar2.CAPI.LibFile, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_CreateMask(
            ovrAvatar2AnimationId hierarchyAssetId,
            string name,
            [In] string[] includedJoints,
            int numJoints,
            [In] string[] includedFloats,
            int numFloats,
            out ovrAvatar2AnimationMaskId outAssetId);

        // Layers

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_SetLayerWeight(MixerLayerPtr mixerLayer, float weight);

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_DestroyLayer(MixerLayerPtr mixerLayer);


        // State layer

        [DllImport(Avatar2.CAPI.LibFile, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_GetParameterId(string paramName, out ovrAvatar2AnimationParameterId paramId);

        [DllImport(Avatar2.CAPI.LibFile, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_GetStateId(string stateName, out ovrAvatar2AnimationStateId paramId);

        [DllImport(Avatar2.CAPI.LibFile, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_GetTransitionId(string transitionName, out ovrAvatar2AnimationTransitionId paramId);

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_CreateStateLayer(
            ovrAvatar2EntityId entityId,
            ovrAvatar2AnimationStateMachineDefinitionId stateMachineId,
            int priority,
            ovrAvatar2AnimationBlendMode blendMode,
            out MixerLayerPtr layer);

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_StateLayerSetFloatParameter(
            MixerLayerPtr mixerLayer, ovrAvatar2AnimationParameterId param, float value);

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_StateLayerSetFloatParameters(
            MixerLayerPtr mixerLayer, int numParams, ParameterIdArrayPtr paramIds, FloatArrayPtr values);

        [DllImport(Avatar2.CAPI.LibFile, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_StateLayerSetNameParameter(
            MixerLayerPtr mixerLayer, ovrAvatar2AnimationParameterId param, string name);

        [DllImport(Avatar2.CAPI.LibFile, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_StateLayerSetNameParameters(
            MixerLayerPtr mixerLayer, int numParams, ParameterIdArrayPtr paramIds, [In] string[] names);


        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_StateLayerRequestTransition(
            MixerLayerPtr mixerLayer, ovrAvatar2AnimationTransitionId transitionId);

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_StateLayerRequestFadeToState(
            MixerLayerPtr mixerLayer, ovrAvatar2AnimationStateId transitionId, float transitionTimeSec);


        /// Clip Layer

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_ClipLayerCreate(
            ovrAvatar2EntityId entityId, int priority, out MixerLayerPtr newLayer);

        [DllImport(Avatar2.CAPI.LibFile, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_ClipLayerSetClipByName(
            MixerLayerPtr mixerLayer, string animName);

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_ClipLayerSetClipById(
            MixerLayerPtr mixerLayer, ovrAvatar2AnimationClipId animId);

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_ClipLayerSetRate(
            MixerLayerPtr mixerLayer, float rate);

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_ClipLayerSetPhase(
            MixerLayerPtr mixerLayer, float phase);

        /// Viseme Layer

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result ovrAvatar2Animation_CreateVisemeLayer(
            ovrAvatar2EntityId entityId,
            in ovrAvatar2AnimVisemeLayerParams visemeParams,
            int numVisemeAnims,
            [In] string[] visemeAnimNames,
            out MixerLayerPtr layer);


        /// Ik Layer
        ///
        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result
           ovrAvatar2Animation_CreateIkLayer(
               ovrAvatar2EntityId entityId,
               int priority,
               out MixerLayerPtr layer);

        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result
           ovrAvatar2Animation_CreateIkLayerFromParams(
                ovrAvatar2EntityId entityId,
                int priority,
                in ovrAvatar2AnimationIkLayerParams parameters,
                out MixerLayerPtr layer);


        [DllImport(Avatar2.CAPI.LibFile, CallingConvention = CallingConvention.Cdecl)]
        public static extern ovrAvatar2Result
            ovrAvatar2Animation_IkLayerSetTargetWeight(
                MixerLayerPtr layer,
                ovrAvatar2AnimationIkTarget target,
                float weight,
                float timeSec);

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2AnimVisemeLayerParams
        {
            int priority;
            ovrAvatar2AnimationVisemeFilterSettings filterSetiings;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2AnimationVisemeFilterSettings
        {
            float saturationThreshold;
            float onsetSpeed;
            float falloffSpeed;
        }

        /// Ik Layer
        ///

        [StructLayout(LayoutKind.Sequential)]
        public struct OvrAvatarAnimationLimbConfig
        {
            public int upperLimb;
            public int lowerLimb;
            public int extremity;
            public int lowerLimbPartial;
            public int extremityPartial;

            public int numUpperLimbTwists;
            public IntPtr upperLimbTwists;
            public int numLowerLimbTwists;
            public IntPtr lowerLimbTwists;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ovrAvatar2AnimationIkLayerParams
        {
            public int headTracker;
            public int leftHandTracker;
            public int rightHandTracker;

            public int root;
            public int hips;
            public int chest;
            public int neck;
            public int head;

            public int numSpineJoints;
            public IntPtr spineJoints;

            public int rightShoulder;
            public int leftShoulder;

            public OvrAvatarAnimationLimbConfig leftArm;
            public OvrAvatarAnimationLimbConfig rightArm;
            public OvrAvatarAnimationLimbConfig leftLeg;
            public OvrAvatarAnimationLimbConfig rightLeg;
        }

        public enum ovrAvatar2AnimationStateMachineDefinitionId : Int32
        {
            Invalid = 0,
        }

        public enum ovrAvatar2AnimationMaskId : Int32
        {
            Invalid = 0,
        }
        public enum ovrAvatar2AnimationClipId : Int32
        {
            Invalid = 0,
        }

        // TODO: The size of ovrAvatar2AnimationBlendMode is not explicitly specified
        public enum ovrAvatar2AnimationBlendMode
        {
            Override = 0,
            Additive,
        }

        // TODO: The size of ovrAvatar2AnimationIkTarget is not explicitly specified
        public enum ovrAvatar2AnimationIkTarget
        {
            Head = 0,
            LeftHand = 1,
            RightHand = 2,
            Count
        }

        // TODO: The size is not explicitly specified
        public enum ovrAvatar2Mood
        {
            Invalid = -1,
            Neutral = 0,
            Like,
            VeryLike,
            Happy,
            Confused,
            VeryConfused,
            Dislike,
            VeryDislike,
            Unhappy,

            Count
        }

        // typedefs (effectively)
        public enum ovrAvatar2AnimationId : UInt64 { }
        public enum ovrAvatar2AnimationHierarchyId : UInt64 { }
        public enum ovrAvatar2AnimationParameterId : UInt64 { }
        public enum ovrAvatar2AnimationTransitionId : UInt64 { }
        public enum ovrAvatar2AnimationStateId : UInt64 { }

    }
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore CA1401 // P/Invokes should not be visible
}
