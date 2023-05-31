// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;

namespace Discover.SpatialAnchors
{
    public interface ISpatialAnchorFileManager<TData> where TData : SpatialAnchorSaveData
    {
        public void WriteDataToFile(List<TData> dataList);
        public List<TData> ReadDataFromFile();
    }
}