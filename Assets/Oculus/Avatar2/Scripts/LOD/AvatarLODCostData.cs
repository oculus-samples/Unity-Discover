using Debug = UnityEngine.Debug;

namespace Oculus.Avatar2
{
    public struct AvatarLODCostData
    {
        /// Number of vertices in avatar mesh.
        public readonly uint meshVertexCount;
        // TODO: Deprecate, use triCount instead
        /// Number of vertices in the morph targets.
        public readonly uint morphVertexCount;
        /// Number of triangles in the avatar mesh.
        public readonly uint renderTriangleCount;
        // TODO: Include number of skinned bones + num morph targets

        private AvatarLODCostData(uint meshVertCount, uint morphVertCount, uint triCount)
        {
            meshVertexCount = meshVertCount;
            morphVertexCount = morphVertCount;
            renderTriangleCount = triCount;
        }
        internal AvatarLODCostData(OvrAvatarPrimitive prim)
            : this(prim.meshVertexCount, prim.morphVertexCount, prim.triCount) { }
        ///
        /// Add the second LOD cost to the first and return
        /// the combined cost of both LODs.
        ///
        /// @param total    first LodCostData to add.
        /// @param add      second LodCostData to add.
        /// @returns LodCostData with total cost of both LODs.
        // TODO: inplace Increment/Decrement would be useful
        public static AvatarLODCostData Sum(in AvatarLODCostData total, in AvatarLODCostData add)
        {
            return new AvatarLODCostData(
                total.meshVertexCount + add.meshVertexCount,
                total.morphVertexCount + add.morphVertexCount,
                total.renderTriangleCount + add.renderTriangleCount
            );
        }

        ///
        /// Subtract the second LOD cost from the first and return
        /// the difference between the LODs.
        ///
        /// @param total    LodCostData to subtract from.
        /// @param sub      LodCostData to subtract.
        /// @returns LodCostData with different between LODs.
        public static AvatarLODCostData Subtract(in AvatarLODCostData total, in AvatarLODCostData sub)
        {
            Debug.Assert(total.meshVertexCount >= sub.meshVertexCount);
            return new AvatarLODCostData(
                total.meshVertexCount - sub.meshVertexCount,
                total.morphVertexCount - sub.morphVertexCount,
                total.renderTriangleCount - sub.renderTriangleCount
            );
        }
    }
}
