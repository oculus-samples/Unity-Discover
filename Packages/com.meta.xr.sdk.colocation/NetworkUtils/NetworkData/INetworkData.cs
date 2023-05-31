// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using Unity.Collections;

namespace ColocationPackage {
  public interface INetworkData {
    public void AddPlayer(Player player);
    public void RemovePlayer(Player player);
    public Player? GetPlayer(ulong oculusId);
    public List<Player> GetAllPlayers();

    public Player? GetFirstPlayerInColocationGroup(uint colocationGroup);

    public void AddAnchor(Anchor anchor);
    public void RemoveAnchor(Anchor anchor);

    public Anchor? GetAnchor(FixedString64Bytes uuid);
    public List<Anchor> GetAllAnchors();

    public uint GetColocationGroupCount();

    public void IncrementColocationGroupCount();
  }
}
