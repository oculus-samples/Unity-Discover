using System;
using System.Collections.Generic;

/// @file OvrAvatarGazeTargetManager.cs

namespace Oculus.Avatar2
{
    ///
    /// Handles the set of gaze targets the avatar can look at.
    /// Typically this is another avatars head, avatar hands
    /// or an object in the scene. This class collects the
    /// positions of each of these targets every frame to
    /// calculate what the avatar should look at.
    ///
    /// A gaze target is added to this set when an instance
    /// of @ref OvrAvatarGazeTarget is created. Typically this
    /// is when the @ref OvrAvatarGazeTarget component is enabled.
    /// The gaze target is removed from the set when the
    /// component is disabled.
    ///
    /// @see OvrAvatarGazeTarget
    /// @see OvrAvatarEntity.GetGazePosition
    ///
    public class OvrAvatarGazeTargetManager
    {
        private readonly List<OvrAvatarGazeTarget> _gazeTargets = new List<OvrAvatarGazeTarget>();
        private CAPI.ovrAvatar2GazeTarget[] _targetsToUpdate;

        internal bool AddTarget(OvrAvatarGazeTarget target)
        {
            var created = CreateGazeTarget(target.Target);
            if (created)
            {
                _gazeTargets.Add(target);
            }

            return created;
        }

        internal void RemoveTarget(OvrAvatarGazeTarget target)
        {
            var index = _gazeTargets.IndexOf(target);
            if (index >= 0)
            {
                var lastIndex = _gazeTargets.Count - 1;
                _gazeTargets[index] = _gazeTargets[lastIndex];
                _gazeTargets.RemoveAt(lastIndex);
            }

            DestroyGazeTarget(target.Target);
        }

        internal void Update()
        {
            if (_targetsToUpdate == null || _targetsToUpdate.Length < _gazeTargets.Capacity)
            {
                _targetsToUpdate = new CAPI.ovrAvatar2GazeTarget[_gazeTargets.Capacity];
            }

            var updateCount = 0;
            foreach (var target in _gazeTargets)
            {
                if (target.Dirty)
                {
                    target.MarkClean();
                    _targetsToUpdate[updateCount] = target.Target;
                    _targetsToUpdate[updateCount].worldPosition =
                       target.NativePosition;
                    updateCount++;
                }
            }

            if (updateCount > 0)
            {
                unsafe
                {
                    fixed (CAPI.ovrAvatar2GazeTarget* targets = _targetsToUpdate)
                    {
                        var result = CAPI.ovrAvatar2Behavior_UpdateGazeTargetPositions(targets, updateCount);
                        if (result != CAPI.ovrAvatar2Result.Success)
                        {
                            OvrAvatarLog.LogWarning("Could not update gazeTargets");
                        }
                    }
                }
            }
        }

        private static bool CreateGazeTarget(CAPI.ovrAvatar2GazeTarget target)
        {
            unsafe
            {
                CAPI.ovrAvatar2GazeTarget* pTarget = &target;
                var result = CAPI.ovrAvatar2Behavior_CreateGazeTargets((IntPtr)pTarget, 1);
                if (result != CAPI.ovrAvatar2Result.Success)
                {
                    OvrAvatarLog.LogWarning("Could not create gaze target");
                }

                return result == CAPI.ovrAvatar2Result.Success;
            }
        }

        private static bool DestroyGazeTarget(CAPI.ovrAvatar2GazeTarget target)
        {
            unsafe
            {
                CAPI.ovrAvatar2GazeTarget* pTarget = &target;
                var result = CAPI.ovrAvatar2Behavior_DestroyGazeTargets((IntPtr)pTarget, 1);
                if (result != CAPI.ovrAvatar2Result.Success)
                {
                    OvrAvatarLog.LogWarning("Could not destroy gaze target");
                }

                return result == CAPI.ovrAvatar2Result.Success;
            }
        }
    }
}
