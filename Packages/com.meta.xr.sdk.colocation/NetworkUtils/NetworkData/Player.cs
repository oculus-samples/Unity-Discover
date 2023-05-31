// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;

namespace ColocationPackage {
  [Serializable]
  public struct Player : IEquatable<Player> {
    public ulong oculusId;
    public uint colocationGroupId;

    public Player(ulong oculusId, uint colocationGroupId) {
      this.oculusId = oculusId;
      this.colocationGroupId = colocationGroupId;
    }

    public bool Equals(Player other) {
      return oculusId == other.oculusId && colocationGroupId == other.colocationGroupId;
    }
  }
}
