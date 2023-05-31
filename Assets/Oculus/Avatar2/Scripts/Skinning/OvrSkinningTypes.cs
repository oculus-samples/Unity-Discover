using System;

using Oculus.Avatar2;

/// @#file OvrSkinningTypes.cs

namespace Oculus.Skinning
{
    ///
    /// Types used for skinning.
    ///
    public static class OvrSkinningTypes
    {
        public class Handle : IEquatable<Handle>
        {
            private const int kInvalidValue = -1;

            public static readonly Handle kInvalidHandle = new Handle();

            private Handle()
            {
                _val = kInvalidValue;
            }

            public Handle(int val)
            {
                _val = val;
            }

            public int GetValue()
            {
                return _val;
            }

            public bool IsValid()
            {
                return _val > kInvalidValue;
            }

            public bool Equals(Handle other)
            {
                if (other is null)
                    return false;

                return _val == other._val;
            }

            // TODO: Should be explicit
            public static implicit operator Handle(CAPI.ovrGpuSkinningHandle h) => new Handle((int)h);
            public static implicit operator CAPI.ovrGpuSkinningHandle(Handle h) => (CAPI.ovrGpuSkinningHandle)h.GetValue();

            public override bool Equals(object obj) => Equals(obj as Handle);
            public override int GetHashCode() => _val;

            public override string ToString() => $"Handle with value of {_val.ToString()}";

            private readonly int _val;
        }

        ///
        /// Specifies the skinning quality.
        /// This is the number of bones used to influence
        /// each vertex in the skin.
        ///
        public enum SkinningQuality
        {
            /// Error code
            Invalid = 0,

            ///Use 1 bone to deform a single vertex. (The most important bones will be used).
            Bone1 = 1,

            /// Use 2 bones to deform a single vertex. (The most important bones will be used).
            Bone2 = 2,

            /// Use 4 bones to deform a single vertex.
            Bone4 = 4,
        }
    }
}
