// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using Unity.Collections;

namespace ColocationPackage {
  [Serializable]
  public struct Anchor : IEquatable<Anchor> {
    public bool isAlignmentAnchor;
    public FixedString64Bytes uuid;
    public ulong ownerOculusId;
    public uint colocationGroupId;

    public Anchor(bool isAlignmentAnchor, FixedString64Bytes uuid, ulong ownerOculusId, uint colocationGroupId) {
      this.isAlignmentAnchor = isAlignmentAnchor;
      this.uuid = uuid;
      this.ownerOculusId = ownerOculusId;
      this.colocationGroupId = colocationGroupId;
    }

    public bool Equals(Anchor other) {
      return isAlignmentAnchor == other.isAlignmentAnchor
             && uuid == other.uuid
             && ownerOculusId == other.ownerOculusId
             && colocationGroupId == other.colocationGroupId;
    }
  }
}
