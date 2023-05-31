// Copyright (c) Meta Platforms, Inc. and affiliates.

#if HAS_META_AVATARS

using Meta.Utilities;
using Meta.Utilities.Input;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

namespace Meta.Utilities.Input
{
    public class XRAnimatedHand : MonoBehaviour
    {
        public const string LAYER_NAME_POINT = "Point Layer";
        public const string LAYER_NAME_THUMB = "Thumb Layer";
        public const string PARAM_NAME_FLEX = "Flex";

        public const float INPUT_RATE_CHANGE = 20.0f;

        [SerializeField] private Animator m_animator;

        [AutoSetFromParent]
        [SerializeField] private XRInputManager m_xrInputManager;
        [SerializeField] private bool m_isLeftHand;
        private int m_animLayerIndexPoint = -1;

        private int m_animLayerIndexThumb = -1;
        private int m_animParamIndexFlex = -1;
        private bool m_isGivingThumbsUp;

        private bool m_isPointing;
        private float m_pointBlend;
        private float m_thumbsUpBlend;

        private XRInputControlActions.Controller Actions => m_xrInputManager.GetActions(m_isLeftHand);

        protected virtual void Start()
        {
            m_animLayerIndexPoint = m_animator.GetLayerIndex(LAYER_NAME_POINT);
            m_animLayerIndexThumb = m_animator.GetLayerIndex(LAYER_NAME_THUMB);
            m_animParamIndexFlex = Animator.StringToHash(PARAM_NAME_FLEX);

            // just doing a query here seems to "wake up" the capacitive touch inputs
            var device = InputDevices.GetDeviceAtXRNode(m_isLeftHand ? XRNode.LeftHand : XRNode.RightHand);
            _ = InputHelpers.IsPressed(device, InputHelpers.Button.PrimaryTouch, out _);
        }

        protected virtual void Update()
        {
            UpdateCapTouchStates();
            UpdatePointingState();
            m_pointBlend = InputValueRateChange(m_isPointing, m_pointBlend);
            m_thumbsUpBlend = InputValueRateChange(m_isGivingThumbsUp, m_thumbsUpBlend);

            UpdateAnimStates();
        }

        private void UpdatePointingState()
        {
            m_isPointing = Actions.AxisIndexTrigger.action.ReadValue<float>() < 0.5f;
        }

        private void UpdateCapTouchStates()
        {
            m_isGivingThumbsUp = Actions.AnyPrimaryThumbButtonTouching < 0.5f;
        }

        /// <summary>
        ///     Based on InputValueRateChange from OVR Samples it ensures
        ///     the animation blending happens with controlled timing instead of instantly
        /// </summary>
        /// <param name="isDown">Direction of the animation</param>
        /// <param name="value">Value to change</param>
        /// <returns>The input value increased or decreased at a fixed rate</returns>
        private float InputValueRateChange(bool isDown, float value)
        {
            var rateDelta = Time.deltaTime * INPUT_RATE_CHANGE;
            var sign = isDown ? 1.0f : -1.0f;
            return Mathf.Clamp01(value + rateDelta * sign);
        }

        private void UpdateAnimStates()
        {
            // Flex
            // blend between open hand and fully closed fist
            var flex = Actions.AxisHandTrigger.action.ReadValue<float>();
            m_animator.SetFloat(m_animParamIndexFlex, flex);

            // Point
            m_animator.SetLayerWeight(m_animLayerIndexPoint, m_pointBlend);

            // Thumbs up
            m_animator.SetLayerWeight(m_animLayerIndexThumb, m_thumbsUpBlend);

            var pinch = Actions.AxisIndexTrigger.action.ReadValue<float>();
            m_animator.SetFloat("Pinch", pinch);
        }
    }
}

#endif
