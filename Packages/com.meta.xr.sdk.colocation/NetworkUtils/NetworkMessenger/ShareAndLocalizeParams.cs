// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Unity.Collections;

namespace ColocationPackage {
  public struct ShareAndLocalizeParams {
    public ulong oculusIdAnchorOwner;
    public ulong oculusIdAnchorRequester;
    public Guid headsetIdAnchorRequester;
    public FixedString64Bytes uuid;
    public bool anchorFlowSucceeded;

    public ShareAndLocalizeParams(
        ulong oculusIdAnchorOwner, ulong oculusIdAnchorRequester, Guid headsetIdAnchorRequester, string uuid) {
      this.oculusIdAnchorOwner = oculusIdAnchorOwner;
      this.oculusIdAnchorRequester = oculusIdAnchorRequester;
      this.headsetIdAnchorRequester = headsetIdAnchorRequester;
      this.uuid = uuid;
      anchorFlowSucceeded = true;
    }

    public override string ToString() {
      return
        $"oculusIdAnchorOwner: {oculusIdAnchorOwner}, oculusIdAnchorRequester: {oculusIdAnchorRequester}, " +
        $"headsetIdAnchorRequester: {headsetIdAnchorRequester}, uuid: {uuid}, " +
        $"anchorFlowSucceeded: {anchorFlowSucceeded}";
    }
  }
}

