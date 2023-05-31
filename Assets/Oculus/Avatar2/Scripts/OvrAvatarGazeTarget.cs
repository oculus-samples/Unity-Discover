using UnityEngine;

/// @file OvrAvatarGazeTarget.cs

namespace Oculus.Avatar2
{
    ///
    /// Designates something the avatar can look at.
    /// When this component is enabled, it is added to the
    /// gaze target manager (in the avatar manager) which
    /// will provide its position to the avatar behavior
    /// engine every frame to calculate where the avatar
    /// should look.
    ///
    /// The example below shows how to make a gaze target for the avatar's left hand.
    /// @code
    /// OvrAvatarEntity entity;
    /// Transform jointTransform = entity.GetSkeletonTransform(CAPI.ovrAvatar2JointType.LeftHandIndexProximal);
    /// GameObject gazeTargetObj = new GameObject("lookatlefthand");
    /// OvrAvatarGazeTarget gazeTarget = gazeTargetObj.AddComponent<OvrAvatarGazeTarget>();
    /// gazeTarget.TargetType = CAPI.ovrAvatar2GazeTargetType.AvatarHand;
    /// gazeTargetObj.transform.SetParent(jointTransform, false);
    /// @endcode
    ///
    /// @see OvrAvatarGazeTargetManager
    /// @see OvrAvatarManager.GazeTargetManager
    ///
    public class OvrAvatarGazeTarget : MonoBehaviour
    {
        private static CAPI.ovrAvatar2Id nextId;

        [SerializeField]
        private CAPI.ovrAvatar2GazeTargetType _targetType = CAPI.ovrAvatar2GazeTargetType.Object;

        private bool _targetCreated;
        private Transform _transform;

        internal bool Dirty => _transform.hasChanged;

        internal CAPI.ovrAvatar2GazeTarget Target { get; private set; }
        internal CAPI.ovrAvatar2Vector3f NativePosition => ((CAPI.ovrAvatar2Vector3f)Position).ConvertSpace();

        /// Unique identifier for gaze target.
        public CAPI.ovrAvatar2Id Id => Target.id;

        /// Current position of gaze target.
        public Vector3 Position => _transform.position;

        /// Type of gaze target (avatar head, hand, moving or static object).
        public CAPI.ovrAvatar2GazeTargetType TargetType
        {
            get => _targetType;
            set
            {
                if (value != Target.type)
                {
                    _targetType = value;
                    OnTypeChanged();
                }
            }
        }

        #region Unity Events

        protected virtual void Awake()
        {
            _transform = transform;
            Target = new CAPI.ovrAvatar2GazeTarget
            {
                id = nextId++,
                type = _targetType
            };
        }

        protected virtual void OnEnable()
        {
            CreateTarget();
        }

        protected virtual void OnDisable()
        {
            DestroyTarget();
        }

        protected virtual void OnValidate()
        {
            // Trigger type changes.
            TargetType = _targetType;
        }

        #endregion

        #region Private Methods

        private void DestroyTarget()
        {
            // This object may be destroyed after the manager.
            var mgr = OvrAvatarManager.Instance;
            if (mgr)
            {
                mgr.GazeTargetManager.RemoveTarget(this);
            }
            _targetCreated = false;
        }

        private void CreateTarget()
        {
            var newTarget = Target;
            newTarget.type = _targetType;
            newTarget.worldPosition = NativePosition;
            Target = newTarget;
            _targetCreated = OvrAvatarManager.Instance.GazeTargetManager.AddTarget(this);
        }

        private void OnTypeChanged()
        {
            // Targets have to be recreated when changing type
            if (_targetCreated)
            {
                DestroyTarget();
                CreateTarget();
            }
        }


        #endregion

        internal void MarkClean()
        {
            _transform.hasChanged = false;
        }
    }
}
