using UnityEngine;

namespace Oculus.Avatar2
{
    /**
     * Base class for setting input tracking on an avatar entity.
     */
    public abstract class OvrAvatarInputTrackingDelegate : IOvrAvatarInputTrackingDelegate
    {
        // TODO: These should likely be reduced, but we need better data to do so unilaterally. Apps can use whatever values they please.
        // NOTE: 15% longer than 1/2 Manute Bol's armspan, this is still past the distance where retargeting goes insane
        public const float DEFAULT_CLAMP_HEAD_TO_HAND_DISTANCE = 1.5f;
        // NOTE: Basically arbitrary but larger than clamp distance
        public const float DEFAULT_DISABLE_HEAD_TO_HAND_DISTANCE = 1.75f;

        // Squared thresholds for comparsion w/out Sqrt
        private const float DEFAULT_CLAMP_DIST_SQUARED = DEFAULT_CLAMP_HEAD_TO_HAND_DISTANCE * DEFAULT_CLAMP_HEAD_TO_HAND_DISTANCE;
        private const float DEFAULT_DISABLE_DIST_SQUARED = DEFAULT_DISABLE_HEAD_TO_HAND_DISTANCE * DEFAULT_DISABLE_HEAD_TO_HAND_DISTANCE;

        // get the raw unfiltered, unconverted input transforms
        public abstract bool GetRawInputTrackingState(out OvrAvatarInputTrackingState inputTrackingState);

        public bool GetInputTrackingState(out OvrAvatarInputTrackingState inputTrackingState)
        {
            if (!GetRawInputTrackingState(out inputTrackingState))
            {
                return false;
            }

            ConvertTransform(ref inputTrackingState.headset);
            ConvertTransform(ref inputTrackingState.leftController);
            ConvertTransform(ref inputTrackingState.rightController);

            FilterInput(ref inputTrackingState);

            return true;
        }

        private static void ConvertTransform(ref CAPI.ovrAvatar2Transform transform)
        {
            // Convert from Unity coordinate space to the SDK coordinate space
            transform = transform.ConvertSpace();
        }

        /**
         * Selects how controller tracking is filtered.
         * You can specify the mimimum distance between hands and head,
         * and what to do about distant or inactive controllers.
         * Subclasses can override to change clamp/disable distances, or simply skip this filtering.
         * @param inputState    which controllers to affect.
         * @param inputTransforms has the current controller position & orientation on entry,
         *                        gets the updated position & orientation on exit.
         * @see CAPI.ovrAvatar2InputState
         * @see CAPI.ovrAvatar2InputTransforms
         */
        protected virtual void FilterInput(ref OvrAvatarInputTrackingState inputTracking)
        {
            ClampHandPositions(ref inputTracking, out var handDistances, DEFAULT_CLAMP_DIST_SQUARED);
            DisableDistantControllers(ref inputTracking, in handDistances, DEFAULT_DISABLE_DIST_SQUARED);
            HideInactiveControllers(ref inputTracking);
        }

        /**
         * @class InputHandDistances
         * Contains hand distances squared for each controller.
         */
        protected readonly struct InputHandDistances
        {
            public InputHandDistances(float lSquared, float rSquared) { leftSquared = lSquared; rightSquared = rSquared; }

            public readonly float leftSquared;
            public readonly float rightSquared;
        }

        /**
         * Clamp hand distance from head to be within a specified distance.
         * @param tracking             input position & orientation of each controller.
         * @param distances            gets the squared distance of each hand on output.
         * @param clampDistanceSquared maximum distance between hands and head squared.
         * @see FilterInput
         * @see CAPI.ovrAvatar2InputTransforms
         * @see InputHandDistances
         */
        protected static void ClampHandPositions(ref OvrAvatarInputTrackingState tracking, out InputHandDistances distances, float clampDistanceSquared)
        {
            ref readonly var hmdPos = ref tracking.headset.position;
            ref var leftHandPos = ref tracking.leftController.position;
            ref var rightHandPos = ref tracking.rightController.position;

            var leftHandDist = ClampHand(in hmdPos, ref leftHandPos, clampDistanceSquared);
            var rightHandDist = ClampHand(in hmdPos, ref rightHandPos, clampDistanceSquared);

            distances = new InputHandDistances(leftHandDist, rightHandDist);
        }

        /**
         * Disable controllers further than the specified distance.
         * @param inputControl           which controllers to affect.
         * @param handDistances          squared distance of controller.
         * @param disableDistanceSquared distance beyond which controller is disabled squared.
         * @see FilterInput
         * @see CAPI.ovrAvatar2InputState
         * @see InputHandDistances
         */
        protected static void DisableDistantControllers(ref OvrAvatarInputTrackingState inputTracking, in InputHandDistances handDistances, float disableDistanceSquared)
        {
            DisableDistantController(ref inputTracking.leftControllerActive, handDistances.leftSquared, disableDistanceSquared);
            DisableDistantController(ref inputTracking.rightControllerActive, handDistances.rightSquared, disableDistanceSquared);
        }

        protected static float ClampHand(in CAPI.ovrAvatar2Vector3f hmdPos, ref CAPI.ovrAvatar2Vector3f handPos, float clampDistanceSquared)
        {
            OvrAvatarLog.Assert(clampDistanceSquared >= 0.0f);

            var hmdToHand = handPos - hmdPos;
            float hmdToHandLenSquared = hmdToHand.LengthSquared;
            if (hmdToHandLenSquared > clampDistanceSquared)
            {
                handPos = hmdPos + (hmdToHand * Mathf.Sqrt(clampDistanceSquared / hmdToHandLenSquared));
            }
            return hmdToHandLenSquared;
        }

        protected static void DisableDistantController(ref bool controllerActive, float distanceSquared, float disableThresholdSquared)
        {
            OvrAvatarLog.Assert(disableThresholdSquared >= 0.0f);

            if (distanceSquared > disableThresholdSquared)
            {
                controllerActive = false;
            }
        }

        /**
         * Hide rendering of inactive controllers.
         * @param inputControl   which controllers to affect.
         * @see FilterInput
         * @see CAPI.ovrAvatar2InputState
         */
        protected static void HideInactiveControllers(ref OvrAvatarInputTrackingState inputTracking)
        {
            inputTracking.leftControllerVisible &= inputTracking.leftControllerActive;
            inputTracking.rightControllerVisible &= inputTracking.rightControllerActive;
        }
    }
}
