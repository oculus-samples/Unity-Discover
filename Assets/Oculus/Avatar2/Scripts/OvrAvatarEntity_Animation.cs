using System;
using System.Collections.Generic;

using Unity.Collections;

using UnityEngine;

namespace Oculus.Avatar2
{
    public interface IInterpolationValueProvider
    {
        // Will return a value between 0.0 and 1.0 (inclusive)
        float GetRenderInterpolationValue();
    }

    // Partial class intended to encapsulate "avatar animation" related functionality.
    // Mainly related to "morph targets" and "skinning"
    public partial class OvrAvatarEntity
    {
        private EntityAnimatorBase _entityAnimator;
        private IInterpolationValueProvider _interpolationValueProvider;

        #region Entity Animators

        private abstract class EntityAnimatorBase
        {
            protected readonly OvrAvatarEntity Entity;

            protected EntityAnimatorBase(OvrAvatarEntity entity)
            {
                Entity = entity;
            }

            private readonly List<PrimitiveRenderData> _addNewAnimationFrameCache = new List<PrimitiveRenderData>();
            public virtual void AddNewAnimationFrame(
                float timestamp,
                float deltaTime,
                in CAPI.ovrAvatar2Pose entityPose,
                in CAPI.ovrAvatar2EntityRenderState renderState)
            {
                // If a remote avatar is playing back streaming packet, update pose and morph targets.
                var isPlayingStream = !Entity.IsLocal && Entity.GetStreamingPlaybackState().HasValue;

                bool skeletalAnimation = isPlayingStream || Entity.HasAnyFeatures(UPDATE_POSE_FEATURES);
                bool morphAnimation = isPlayingStream || Entity.HasAnyFeatures(UPDATE_MOPRHS_FEATURES);

                if (skeletalAnimation || morphAnimation)
                {
                    Entity.BroadcastAnimationFrameStart();

                    // It's possible that the animation frame start call can complete an LOD transition
                    // which may disable animation, so re-query the animation enable state
                    // TODO: This call is not cheap
                    // - we should have the broadcast method report if a rebuild is necessary
                    _addNewAnimationFrameCache.Clear();
                    Entity.AppendPrimitivesWithAnimationEnabled(null, _addNewAnimationFrameCache);
                    if (skeletalAnimation)
                    {
                        Entity.SamplePose(in entityPose, in renderState, _addNewAnimationFrameCache);
                    }

                    if (morphAnimation)
                    {
                        Entity.SampleMorphTargets(_addNewAnimationFrameCache);
                    }
                }

                Entity.MonitorJoints(in entityPose);
            }

            public abstract void UpdateAnimationTime(float deltaTime, bool isAllAnimationDataValid);
        }

        private sealed class EntityAnimatorMotionSmoothing : EntityAnimatorBase, IInterpolationValueProvider
        {
            // Currently using as a double buffering setup with only two frames, FrameA and FrameB
            // No pending frames are stored, as new frames come in before previous render frames are finished,
            // old frames are dropped
            private static readonly int NUM_ANIMATION_FRAMES = 2;

            private sealed class AnimationFrameInfo
            {
                public float Timestamp { get; private set; }

                public bool IsValid { get; private set; }

                public void UpdateValues(float time)
                {
                    Timestamp = time;
                    IsValid = true;
                }
            }

            // In this implementation, no pending frames are held, only  2 "animation frames"
            // are held on to
            private readonly AnimationFrameInfo[] _animationFrameInfo = new AnimationFrameInfo[NUM_ANIMATION_FRAMES];
            private float _latestAnimationFrameTime;
            private int _nextAnimationFrameIndex;

            private bool _hasTwoValidAnimationFrames;

            private float _interpolationValue;

            private int EarliestAnimationFrameIndex => _nextAnimationFrameIndex;
            private int LatestAnimationFrameIndex => 1 - _nextAnimationFrameIndex;

            public EntityAnimatorMotionSmoothing(OvrAvatarEntity entity) : base(entity)
            {
                for (int i = 0; i < _animationFrameInfo.Length; i++)
                {
                    _animationFrameInfo[i] = new AnimationFrameInfo();
                }
            }

            public float GetRenderInterpolationValue()
            {
                return _interpolationValue;
            }

            public override void AddNewAnimationFrame(
                float timestamp,
                float deltaTime,
                in CAPI.ovrAvatar2Pose entityPose,
                in CAPI.ovrAvatar2EntityRenderState renderState)
            {
                base.AddNewAnimationFrame(timestamp, deltaTime, entityPose, renderState);
                AddNewAnimationFrameTime(timestamp, deltaTime);
            }

            public override void UpdateAnimationTime(float deltaTime, bool isAllAnimationDataValid)
            {
                CalculateInterpolationValue(deltaTime, isAllAnimationDataValid);
            }

            private void AddNewAnimationFrameTime(float timestamp, float deltaTime)
            {
                // In this implementation, there are no historical/pending frames on top of the "render frames"
                // (the frames currently rendered/interpolated between).
                // Note the time of the frame to be added
                _animationFrameInfo[_nextAnimationFrameIndex].UpdateValues(timestamp);

                // Advance/ping pong frame index
                _nextAnimationFrameIndex =
                    1 - _nextAnimationFrameIndex; // due to there only being 2 frames, this will ping pong

                if (!_hasTwoValidAnimationFrames && _animationFrameInfo[1].IsValid)
                {
                    _hasTwoValidAnimationFrames = true;
                }

                if (_hasTwoValidAnimationFrames)
                {
                    var earliestFrame = _animationFrameInfo[EarliestAnimationFrameIndex];

                    // Fast forward/rewind render frame time to be the earliest frame's timestamp minus the delta.
                    // This has two effects:
                    // 1) If the frame generation frequency changes to be faster (i.e. frames at 0, 1, 1.5),
                    //    then this logic "fast forwards" the render time which may cause a jump in animation, but
                    //    keeps the "interpolation window" (the time that fake animation data is generated) to
                    //    be the smallest possible.
                    // 2) If the frame generate frequency slows down (i.e. frames at 0, 0.5, 2), then this logic
                    //    "rewinds" the render time which will cause the animation to not skip any of the animation
                    //    window
                    _latestAnimationFrameTime = earliestFrame.Timestamp - deltaTime;
                }
            }

            private void CalculateInterpolationValue(float delta, bool isAllAnimationDataValid)
            {
                // Can only advance if there are 2 or more valid render frames
                if (!_hasTwoValidAnimationFrames)
                {
                    _interpolationValue = 0.0f;
                    return;
                }

                _latestAnimationFrameTime += delta;

                // For "motion smoothing" any OvrAvatarSkinnedRenderable subclass that is going to be rendered,
                // ideally, will be rendered with "completely valid render data". Unfortunately that might not be
                // the case (at the moment, until LOD transitions happen differently).
                // The "joint monitoring" however is done on a per entity
                // basis (the renderables all share a common single skeleton).
                // For both the joint monitor and the renderables to all have the same interpolation value,
                // they will all pull from the same source/get passed the same value instead of calculating
                // it themselves (which will also save computation).
                // Given these facts, there needs to be some coupling so that the calculation of the interpolation
                // value knows if all of the renderables being rendered in a given frame have complete valid data.

                // If all renderables' data is completely valid, then interpolation value can be calculated as normal, otherwise, it will
                // be clamped to 1.0
                if (!isAllAnimationDataValid)
                {
                    // Not all skinned renderables have "completely valid animation data".
                    // Return 1.0 so that the renderables + joints are rendering their
                    // latest (and only guaranteed valid) animation frame
                    _interpolationValue = 1.0f;
                    return;
                }

                float t0 = _animationFrameInfo[EarliestAnimationFrameIndex].Timestamp;
                float t1 = _animationFrameInfo[LatestAnimationFrameIndex].Timestamp;

                // InverseLerp clamps to 0 to 1
                _interpolationValue = Mathf.InverseLerp(t0, t1, _latestAnimationFrameTime);
            }
        }

        private sealed class EntityAnimatorDefault : EntityAnimatorBase
        {
            public EntityAnimatorDefault(OvrAvatarEntity entity) : base(entity)
            {
            }

            public override void UpdateAnimationTime(float deltaTime, bool isAllAnimationDataValid)
            {
                // Intentionally empty
            }
        }

        #endregion

        #region Runtime

        // Loop over all skinned renderables, updating the "animation enabled" state and keeping track of which ones
        // are animation enabled
        private void AppendPrimitivesWithAnimationEnabled(
            List<OvrAvatarSkinnedRenderable> skinnedOut, List<PrimitiveRenderData> renderDataOut)
        {
            foreach (var primRenderables in _visiblePrimitiveRenderers)
            {
                foreach (var primRenderable in primRenderables)
                {
                    var skinnedRenderable = primRenderable.skinnedRenderable;
                    // TODO: Remove this expensive `GameObject.==` check
                    if (skinnedRenderable == null || !skinnedRenderable.enabled) { continue; }

                    if (skinnedRenderable.IsAnimationEnabled)
                    {
                        skinnedOut?.Add(skinnedRenderable);
                        renderDataOut?.Add(primRenderable);
                    }
                }
            }
        }


        private void SampleSkinningOrigin(in CAPI.ovrAvatar2PrimitiveRenderState primState,
            out CAPI.ovrAvatar2Transform skinningOrigin)
        {
            skinningOrigin = primState.skinningOrigin;
#if !OVR_AVATAR_ENABLE_CLIENT_XFORM

            // HACK: Mirror rendering transforms to fixup coordinate system errors
            skinningOrigin.scale.z *= -1f;
            skinningOrigin = skinningOrigin.ConvertSpace();
#endif
        }

        private readonly List<OvrAvatarSkinnedRenderable> _broadcastAnimationFrameStartCache
            = new List<OvrAvatarSkinnedRenderable>();
        private void BroadcastAnimationFrameStart()
        {
            // Due to the fact that calling AnimationFrameUpdate may trigger
            // some LOD transitions which in turn, may alter the IsAnimationEnabled
            // value of a OvrAvatarSkinnedRenderable, this function can't
            // used any cached lists of "animation enabled" renderables and instead
            // needs to check IsAnimationEnabled before calling AnimationFrameUpdate
            // Loop over all skinned renderables, updating the "animation enabled" state and keeping track of which ones
            // are animation enabled
            _broadcastAnimationFrameStartCache.Clear();
            AppendPrimitivesWithAnimationEnabled(_broadcastAnimationFrameStartCache, null);
            foreach (var skinnedRenderable in _broadcastAnimationFrameStartCache)
            {
                try
                {
                    skinnedRenderable.AnimationFrameUpdate();
                }
                catch (Exception e)
                {
                    OvrAvatarLog.LogException("AnimationFrameUpdate", e, logScope, this);
                }
            }
        }

        private void SamplePrimitivesSkinningOrigin(in CAPI.ovrAvatar2EntityRenderState renderState)
        {
            if (!HasAnyFeatures(CAPI.ovrAvatar2EntityFeatures.Rendering_Prims))
            {
                return;
            }

            var renderStateIndices = new NativeArray<UInt32>((int)renderState.primitiveCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var renderStates = new NativeArray<CAPI.ovrAvatar2PrimitiveRenderState>((int)renderState.primitiveCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            unchecked
            {
                for (uint i = 0; i < renderState.primitiveCount; ++i)
                {
                    renderStateIndices[(int)i] = i;
                }
            }

            unsafe
            {
                var result = CAPI.ovrAvatar2Render_GetPrimitiveRenderStatesByIndex(entityId, renderStateIndices.GetPtr<UInt32>(), renderStates.GetPtr<CAPI.ovrAvatar2PrimitiveRenderState>(),
                    renderState.primitiveCount);
                if (result != CAPI.ovrAvatar2Result.Success)
                {
                    OvrAvatarLog.LogError($"GetPrimitiveRenderStatesByIndex Error: {result}", logScope, this);
                    return;
                }
            }

            for (uint i = 0; i < renderState.primitiveCount; ++i)
            {
                var primState = renderStates[(int)i];
                SampleSkinningOrigin(in primState, out var skinningOrigin);

                var primRenderables = _primitiveRenderables[primState.id];
                foreach (var primRend in primRenderables)
                {
                    var skinnedRenderable = primRend.skinnedRenderable;
                    if (skinnedRenderable is null)
                    {
                        // Non-skinned renderables just apply the transform
                        var t = primRend.renderable.transform;
                        t.ApplyOvrTransform(skinningOrigin);
                    }
                    else
                    {
                        // Otherwise call function on skinned renderable.
                        // Why does this needs to be called for all renderables
                        // but UpdateJointMatrices is only called on "visible renderers"?
                        // It would make sense if they were updated together
                        skinnedRenderable.UpdateSkinningOrigin(skinningOrigin);
                    }
                }
            }
        }

        private void SamplePose(
            in CAPI.ovrAvatar2Pose entityPose,
            in CAPI.ovrAvatar2EntityRenderState renderState,
            in List<PrimitiveRenderData> animatablePrimitives)
        {
            SamplePrimitivesSkinningOrigin(renderState);

            OvrAvatarLog.AssertConstMessage(entityPose.jointCount == SkeletonJointCount
                , "entity pose does not match skeleton.", logScope, this);

            // Are all SkinnedRenderables able to update without using Unity.Transform?
            bool needsFullTransformUpdate = false;
            // TODO: This will result in redundant skinningMatrices query in UpdateJointMatrices
            foreach (var primRenderable in animatablePrimitives)
            {
                var skinnedRenderable = primRenderable.skinnedRenderable;
                Debug.Assert(skinnedRenderable != null);
                Debug.Assert(skinnedRenderable.IsAnimationEnabled);

                var primitive = primRenderable.primitive;
                needsFullTransformUpdate |=
                    !skinnedRenderable.UpdateJointMatrices(entityId, primitive, primRenderable.instanceId);
            }

            needsFullTransformUpdate |= (_debugDrawing.drawSkelHierarchy ||
                                         _debugDrawing.drawSkelHierarchyInGame ||
                                         _debugDrawing.drawSkinTransformsInGame);

            // If JointMonitoring is enabled, it will update transforms
            if (_jointMonitor == null)
            {
                if (needsFullTransformUpdate && _jointMonitor == null)
                {
                    for (uint i = 0; i < entityPose.jointCount; ++i)
                    {
                        UpdateSkeletonTransformAtIndex(in entityPose, i);
                    }
                }
                else
                {
                    foreach (var skeletonIdx in _unityUpdateJointIndices)
                    {
                        UpdateSkeletonTransformAtIndex(in entityPose, skeletonIdx);
                    }
                }
            }
            else
            {
                Debug.Assert(_unityUpdateJointIndices.Length == 0);
            }
        }

        private void UpdateSkeletonTransformAtIndex(in CAPI.ovrAvatar2Pose entityPose, uint skeletonIdx)
        {
            var jointUnityTx = GetSkeletonTxByIndex(skeletonIdx);

            unsafe
            {
                CAPI.ovrAvatar2Transform* jointTransform = entityPose.localTransforms + skeletonIdx;
                if ((*jointTransform).IsNan()) return;

                var jointParentIndex = entityPose.GetParentIndex(skeletonIdx);
#if OVR_AVATAR_ENABLE_CLIENT_XFORM
                jointUnityTx.ApplyOvrTransform(jointTransform);
#else
                if (jointParentIndex != -1)
                {
                    jointUnityTx.ApplyOvrTransform(jointTransform);
                }
                else
                {
                    // HACK: Mirror rendering transforms across Z to fixup coordinate system errors
                    // Copy provided transform, we should not modify the source array
                    var flipScaleZ = *jointTransform;
                    flipScaleZ.scale.z = -flipScaleZ.scale.z;
                    jointUnityTx.ApplyOvrTransform(in flipScaleZ);
                }
#endif
            }
        }

        private void SampleMorphTargets(in List<PrimitiveRenderData> animatablePrimitives)
        {
            foreach (var primRenderable in animatablePrimitives)
            {
                var skinnedRenderable = primRenderable.skinnedRenderable;
                Debug.Assert(skinnedRenderable != null);
                Debug.Assert(skinnedRenderable.IsAnimationEnabled);

                var primitive = primRenderable.primitive;

                if (primitive.morphTargetCount == 0) { continue; }

                var instanceId = primRenderable.instanceId;
                UInt32 morphTargetCount = primitive.morphTargetCount;
                UInt32 bufferSize = sizeof(float) * morphTargetCount;
                using var weightsBufferHandle = skinnedRenderable.CheckoutMorphTargetBuffer(morphTargetCount);
                var result =
                    CAPI.ovrAvatar2Render_GetMorphTargetWeights(entityId, instanceId, weightsBufferHandle.BufferPtr,
                        bufferSize);
                if (result.IsSuccess())
                {
                    skinnedRenderable.MorphTargetBufferUpdated(weightsBufferHandle);
                }
                else
                {
                    OvrAvatarLog.LogError(
                        $"Error: GetMorphTargetWeights {result} for ID {primitive.assetId}, instance {primRenderable.instanceId}",
                        logScope);
                }
            }
        }

        #endregion
    }
}
