using Oculus.Skinning.GpuSkinning;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace Oculus.Avatar2
{
    public sealed class OvrAvatarGpuSkinningController : System.IDisposable
    {
        // Avoid skinning more avatars than technically feasible
        public const uint MaxGpuSkinnedAvatars = MaxSkinnedAvatarsPerFrame * 8;

        // Avoid skinning more avatars than GPU resources are preallocated for
        public const uint MaxSkinnedAvatarsPerFrame = 32;

        private const int NumExpectedAvatars = 16;

        private readonly List<OvrGpuMorphTargetsCombiner> _activeCombinerList = new List<OvrGpuMorphTargetsCombiner>(NumExpectedAvatars);
        private readonly List<IOvrGpuSkinner> _activeSkinnerList = new List<IOvrGpuSkinner>(NumExpectedAvatars);
        private readonly List<OvrComputeMeshAnimator> _activeAnimators = new List<OvrComputeMeshAnimator>(NumExpectedAvatars);

        private OvrComputeBufferPool bufferPool = new OvrComputeBufferPool();

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool isMainThread) { bufferPool.Dispose(); }

        ~OvrAvatarGpuSkinningController()
        {
            Dispose(false);
        }

        internal void AddActiveCombiner(OvrGpuMorphTargetsCombiner combiner)
        {
            AddGpuSkinningElement(_activeCombinerList, combiner);
        }

        internal void AddActiveSkinner(IOvrGpuSkinner skinner)
        {
            AddGpuSkinningElement(_activeSkinnerList, skinner);
        }

        internal void AddActivateComputeAnimator(OvrComputeMeshAnimator meshAnimator)
        {
            AddGpuSkinningElement(_activeAnimators, meshAnimator);
        }

        // This behaviour is manually updated at a specific time during OvrAvatarManager::Update()
        // to prevent issues with Unity script update ordering
        internal void UpdateInternal()
        {
            Profiler.BeginSample("OvrAvatarGpuSkinningController::UpdateInternal");

            if (_activeCombinerList.Count > 0)
            {
                Profiler.BeginSample("OvrAvatarGpuSkinningController.CombinerCalls");
                foreach (var combiner in _activeCombinerList)
                {
                    combiner.CombineMorphTargetWithCurrentWeights();
                }
                _activeCombinerList.Clear();
                Profiler.EndSample(); // "OvrAvatarGpuSkinningController.CombinerCalls"
            }

            if (_activeSkinnerList.Count > 0)
            {
                Profiler.BeginSample("OvrAvatarGpuSkinningController.SkinnerCalls");
                foreach (var skinner in _activeSkinnerList)
                {
                    skinner.UpdateOutputTexture();
                }
                _activeSkinnerList.Clear();
                Profiler.EndSample(); // "OvrAvatarGpuSkinningController.SkinnerCalls"
            }

            if (_activeAnimators.Count > 0)
            {
                Profiler.BeginSample("OvrAvatarGpuSkinningController.AnimatorDispatches");
                foreach (var animator in _activeAnimators)
                {
                    animator.DispatchAndUpdateOutputs();
                }
                _activeAnimators.Clear();
                Profiler.EndSample(); // "OvrAvatarGpuSkinningController.AnimatorDispatches"
            }

            Profiler.EndSample();
        }

        private void AddGpuSkinningElement<T>(List<T> list, T element) where T : class
        {
            Debug.Assert(element != null);
            Debug.Assert(!list.Contains(element));
            list.Add(element);
        }

        internal void StartFrame()
        {
            bufferPool.StartFrame();
        }

        internal void EndFrame()
        {
            bufferPool.EndFrame();
        }

        internal OvrComputeBufferPool.EntryJoints GetNextEntryJoints()
        {
            return bufferPool.GetNextEntryJoints();
        }

        internal ComputeBuffer GetJointBuffer()
        {
            return bufferPool.GetJointBuffer();
        }

        internal ComputeBuffer GetWeightsBuffer()
        {
            return bufferPool.GetWeightsBuffer();
        }

        internal OvrComputeBufferPool.EntryWeights GetNextEntryWeights(int numMorphTargets)
        {
            return bufferPool.GetNextEntryWeights(numMorphTargets);
        }
    }
}
