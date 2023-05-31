using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oculus.Skinning
{
    public class OvrHandleGenerator
    {
        public OvrHandleGenerator()
        {
            _freeHandles = new HashSet<OvrSkinningTypes.Handle>();
            _maxHandleValSeen = -1;
        }

        public OvrSkinningTypes.Handle GetHandle()
        {
            if (_freeHandles.Count == 0)
            {
                return new OvrSkinningTypes.Handle(++_maxHandleValSeen);
            }

            return _freeHandles.First();
        }

        public void ReleaseHandle(OvrSkinningTypes.Handle handle)
        {
            _freeHandles.Add(handle);
        }

        private readonly HashSet<OvrSkinningTypes.Handle> _freeHandles;
        private int _maxHandleValSeen;
    }
}
