// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using Fusion;
using Meta.XR.Samples;

namespace Discover.Utilities
{
    [MetaCodeSample("Discover")]
    public class NetworkMultiton<T> : NetworkBehaviour where T : NetworkMultiton<T>
    {
        protected static readonly HashSet<T> InternalInstances = new();

        public struct ReadOnlyList : IEnumerable<T>
        {
            public HashSet<T>.Enumerator GetEnumerator() => InternalInstances.GetEnumerator();
            public int Count => InternalInstances.Count;
            IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public static ReadOnlyList Instances => new();

        protected void Awake()
        {
            if (!enabled)
                return;
            _ = InternalInstances.Add((T)this);
        }

        protected void OnEnable()
        {
            _ = InternalInstances.Add((T)this);
        }

        protected void OnDisable()
        {
            _ = InternalInstances.Remove((T)this);
        }

        protected void OnDestroy()
        {
            _ = InternalInstances.Remove((T)this);
        }
    }
}
