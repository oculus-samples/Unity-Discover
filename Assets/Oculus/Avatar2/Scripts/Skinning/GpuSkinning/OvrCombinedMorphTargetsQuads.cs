#define OVR_GPU_PACK_TANGENT_INFO

using System;
using UnityEngine;

namespace Oculus.Skinning.GpuSkinning
{
    public static class OvrCombinedMorphTargetsQuads
    {
        private const int NUM_VERTS_PER_MORPH_TARGET = 4; // 1 quad per
        private const int NUM_INDICES_PER_MORPH_TARGET = 6; // 1 quad per
        private const float Z_POSITION = 0.75f;

        public static void ExpandMeshToFitQuads(Mesh existingMesh, int additionalNumMorphTargets)
        {
            Vector3[] verts = existingMesh.vertices;
            Vector2[] uvs = existingMesh.uv;
#if OVR_GPU_PACK_TANGENT_INFO
            Vector4[] info = existingMesh.tangents;
#endif
            Color[] colors = existingMesh.colors;
            int[] indices = existingMesh.triangles;

            int oldNumVerts = verts.Length;
            int newNumVerts = verts.Length + (additionalNumMorphTargets * NUM_VERTS_PER_MORPH_TARGET);
            int oldNumIndices = indices.Length;
            int newNumIndices = indices.Length + (additionalNumMorphTargets * NUM_INDICES_PER_MORPH_TARGET);

            Array.Resize(ref verts, newNumVerts);
            Array.Resize(ref uvs, newNumVerts);
            Array.Resize(ref colors, newNumVerts);
#if OVR_GPU_PACK_TANGENT_INFO
            Array.Resize(ref info, newNumVerts);
#endif
            Array.Resize(ref indices, newNumIndices);

            for (
              int vertIndex = oldNumVerts, indicesIndex = oldNumIndices;
              vertIndex < newNumVerts;
              vertIndex += NUM_VERTS_PER_MORPH_TARGET, indicesIndex += NUM_INDICES_PER_MORPH_TARGET)
            {
                indices[indicesIndex + 0] = vertIndex + 0;
                indices[indicesIndex + 1] = vertIndex + 2;
                indices[indicesIndex + 2] = vertIndex + 1;
                indices[indicesIndex + 3] = vertIndex + 2;
                indices[indicesIndex + 4] = vertIndex + 3;
                indices[indicesIndex + 5] = vertIndex + 1;
            }

            // Unity documentation says resizing the vertices will also resize colors, uvs, etc.
            existingMesh.vertices = verts;
            existingMesh.uv = uvs;
#if OVR_GPU_PACK_TANGENT_INFO
            existingMesh.tangents = info;
#endif
            existingMesh.colors = colors;
            existingMesh.triangles = indices;
        }

        public static void UpdateQuadsInMesh(
          int meshVertexStartIndex,
          int blockIndex,
          int morphTargetStartIndex,
          RectInt texelRectInCombinedTex,
          int combinedTexWidth,
          int combinedTexHeight,
          RectInt texelRectInSource,
          int texelSliceInSource,
          int sourceTexWidth,
          int sourceTexHeight,
          int numMorphTargets,
          Mesh existingMesh)
        {
            float invTexWidth = 1.0f / combinedTexWidth;
            float invTexHeight = 1.0f / combinedTexHeight;

            float invSourceTexWidth = 1.0f / sourceTexWidth;
            float invSourceTexHeight = 1.0f / sourceTexHeight;

            // Transform the "row and column" into clip space [-1 to 1] for the rectangle origins
            // 4 positions per quad rect change with blend shapes)

            // Each morph target will share same quad positions
            Vector3[] quadPositions = new Vector3[NUM_VERTS_PER_MORPH_TARGET];

            // Convert from "texels" to clip space
            // origin
            quadPositions[0] = new Vector3(texelRectInCombinedTex.xMin * invTexWidth, texelRectInCombinedTex.yMin * invTexHeight, Z_POSITION) * 2.0f - Vector3.one;
            // "x corner"
            quadPositions[1] = new Vector3(texelRectInCombinedTex.xMax * invTexWidth, texelRectInCombinedTex.yMin * invTexHeight, Z_POSITION) * 2.0f - Vector3.one;
            // "y corner"
            quadPositions[2] = new Vector3(texelRectInCombinedTex.xMin * invTexWidth, texelRectInCombinedTex.yMax * invTexHeight, Z_POSITION) * 2.0f - Vector3.one;
            // "opposite corner"
            quadPositions[3] = new Vector3(texelRectInCombinedTex.xMax * invTexWidth, texelRectInCombinedTex.yMax * invTexHeight, Z_POSITION) * 2.0f - Vector3.one;

            var existingVerts = existingMesh.vertices;
            var existingUvs = existingMesh.uv;
            var existingColors = existingMesh.colors;

#if OVR_GPU_PACK_TANGENT_INFO
            var existingInfo = existingMesh.tangents;
#endif

            int heightForSingleMorphTarget = texelRectInSource.height / numMorphTargets; // only calculate this once and cache off
            float uvHeightForSingleTarget = heightForSingleMorphTarget * invSourceTexHeight;

            // Now, for each morph target add new quads (all of which have same positions, differing
            // texture coordinates and color)
            Color encodedValues = new Color(blockIndex, morphTargetStartIndex, 0f, texelSliceInSource);
#if OVR_GPU_PACK_TANGENT_INFO
            Vector4 encodedInfo = new Vector4(blockIndex, morphTargetStartIndex, 0f, texelSliceInSource);
#endif


            Rect texelRectInSourceForTarget = new Rect(
                texelRectInSource.xMin * invSourceTexWidth,
                texelRectInSource.yMin * invSourceTexHeight,
                texelRectInSource.width * invSourceTexWidth,
                uvHeightForSingleTarget);

            for (
              int targetIndex = 0, vertIndex = meshVertexStartIndex;
              targetIndex < numMorphTargets;
              targetIndex++, vertIndex += NUM_VERTS_PER_MORPH_TARGET)
            {
                int combinedTargetIndex = targetIndex + morphTargetStartIndex;
                encodedValues.g = combinedTargetIndex;
#if OVR_GPU_PACK_TANGENT_INFO
                encodedInfo.y = combinedTargetIndex;
#endif

                existingVerts[vertIndex + 0] = quadPositions[0];
                existingVerts[vertIndex + 1] = quadPositions[1];
                existingVerts[vertIndex + 2] = quadPositions[2];
                existingVerts[vertIndex + 3] = quadPositions[3];

                existingUvs[vertIndex + 0] = new Vector2(
                  texelRectInSourceForTarget.xMin,
                  texelRectInSourceForTarget.yMin);

                existingUvs[vertIndex + 1] = new Vector2(
                  texelRectInSourceForTarget.xMax,
                  texelRectInSourceForTarget.yMin);

                existingUvs[vertIndex + 2] = new Vector2(
                  texelRectInSourceForTarget.xMin,
                  texelRectInSourceForTarget.yMax);

                existingUvs[vertIndex + 3] = new Vector2(
                  texelRectInSourceForTarget.xMax,
                  texelRectInSourceForTarget.yMax);

                existingColors[vertIndex + 0] = encodedValues;
                existingColors[vertIndex + 1] = encodedValues;
                existingColors[vertIndex + 2] = encodedValues;
                existingColors[vertIndex + 3] = encodedValues;

#if OVR_GPU_PACK_TANGENT_INFO
                existingInfo[vertIndex + 0] = encodedInfo;
                existingInfo[vertIndex + 1] = encodedInfo;
                existingInfo[vertIndex + 2] = encodedInfo;
                existingInfo[vertIndex + 3] = encodedInfo;
#endif

                texelRectInSourceForTarget.y += uvHeightForSingleTarget;
            }

            // Update mesh
            existingMesh.vertices = existingVerts;
            existingMesh.uv = existingUvs;

#if OVR_GPU_PACK_TANGENT_INFO
            existingMesh.tangents = existingInfo;
#endif

            existingMesh.colors = existingColors;
        }
    }
}
