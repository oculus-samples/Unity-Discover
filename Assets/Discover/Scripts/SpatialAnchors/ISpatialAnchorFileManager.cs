// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using Meta.XR.Samples;

namespace Discover.SpatialAnchors
{
    [MetaCodeSample("Discover")]
    public interface ISpatialAnchorFileManager<TData> where TData : SpatialAnchorSaveData
    {
        public void WriteDataToFile(List<TData> dataList);
        public List<TData> ReadDataFromFile();
    }
}