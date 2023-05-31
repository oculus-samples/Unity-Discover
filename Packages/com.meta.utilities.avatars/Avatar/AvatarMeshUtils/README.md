# Avatar Mesh Utilities

This directory contains general utilities that provide useful data structures and methods related to an Avatar's meshes.

|Utility|Description|
|-|-|
|[AvatarMeshArmInfo](./AvatarMeshArmInfo.cs)|Static class encapsulating methods that allows other scripts retrieve information about a player avatar's arm mesh from an AvatarMeshQuery.|
|[AvatarMeshCache](./AvatarMeshCache.cs)|A singleton that caches Avatar mesh data for quick access elsewhere; also contains callbacks invoked when the mesh is fully loaded.|
|[AvatarMeshQuery](./AvatarMeshQuery.cs)|Allows scripts to access an avatar entity's mesh data, such as vertices and bones.|
