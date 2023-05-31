using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Oculus.Skinning
{
    // This class is required to workaround a bug/feature? when using Vulkan. If there are
    // ByteAddressBuffers or StructuredBuffers that exist in the shader (even if not used at runtime), they
    // must have their buffers set with something. This also causes issues with the Unity Editor that can't
    // be worked around here.
    internal class DummySkinningBufferPropertySetter : IDisposable
    {
        private static AttributePropertyIds _propertyIds = default;

        private ComputeBuffer _dummyBuffer;

        // Dummy buffers method
        public DummySkinningBufferPropertySetter()
        {
            CheckPropertyIdInit();

            _dummyBuffer = new ComputeBuffer(1, sizeof(uint));
        }

        public void SetComputeSkinningBuffersInMatBlock(MaterialPropertyBlock matBlock)
        {
            matBlock.SetBuffer(_propertyIds.ComputeSkinnerPositionBuffer, _dummyBuffer);
            matBlock.SetBuffer(_propertyIds.ComputeSkinnerFrenetBuffer, _dummyBuffer);
        }

        public void Dispose()
        {
            _dummyBuffer.Dispose();
        }

        private static void CheckPropertyIdInit()
        {
            if (!_propertyIds.IsValid)
            {
                _propertyIds = new AttributePropertyIds(AttributePropertyIds.InitMethod.PropertyToId);
            }
        }

        //////////////////////////
        // AttributePropertyIds //
        //////////////////////////
        private struct AttributePropertyIds
        {
            public readonly int ComputeSkinnerPositionBuffer;
            public readonly int ComputeSkinnerFrenetBuffer;

            // These will both be 0 if default initialized, otherwise they are guaranteed unique
            public bool IsValid => ComputeSkinnerPositionBuffer != ComputeSkinnerFrenetBuffer;

            public enum InitMethod { PropertyToId }
            public AttributePropertyIds(InitMethod initMethod)
            {
                ComputeSkinnerPositionBuffer = Shader.PropertyToID("_OvrPositionBuffer");
                ComputeSkinnerFrenetBuffer = Shader.PropertyToID("_OvrFrenetBuffer");
            }
        }
    }
}
