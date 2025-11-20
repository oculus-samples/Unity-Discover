// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Meta.XR.Samples;

namespace Discover.SpatialAnchors
{
    [Serializable]
    [MetaCodeSample("Discover")]
    public class SpatialAnchorSaveData
    {
        public Guid AnchorUuid;
        public string Name;
    }
}