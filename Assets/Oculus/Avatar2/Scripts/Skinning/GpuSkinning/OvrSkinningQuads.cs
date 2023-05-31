using System;
using UnityEngine;

namespace Oculus.Skinning.GpuSkinning
{
    public static class OvrSkinningQuads
    {
        private const int NUM_VERTS_PER_QUAD = 4; // 1 quad per
        private const int NUM_INDICES_PER_QUAD = 6; // 1 quad per
        private const float Z_POSITION = 0.75f;

        public static void ExpandMeshToFitQuad(Mesh existingMesh)
        {
            Vector3[] verts = existingMesh.vertices;
            Vector2[] uvs = existingMesh.uv;
            Color[] colors = existingMesh.colors;
            int[] indices = existingMesh.triangles;

            int oldNumVerts = verts.Length;
            int newNumVerts = verts.Length + NUM_VERTS_PER_QUAD;
            int oldNumIndices = indices.Length;
            int newNumIndices = indices.Length + NUM_INDICES_PER_QUAD;

            Array.Resize(ref verts, newNumVerts);
            Array.Resize(ref uvs, newNumVerts);
            Array.Resize(ref colors, newNumVerts);
            Array.Resize(ref indices, newNumIndices);

            indices[oldNumIndices + 0] = oldNumVerts + 0;
            indices[oldNumIndices + 1] = oldNumVerts + 2;
            indices[oldNumIndices + 2] = oldNumVerts + 1;
            indices[oldNumIndices + 3] = oldNumVerts + 2;
            indices[oldNumIndices + 4] = oldNumVerts + 3;
            indices[oldNumIndices + 5] = oldNumVerts + 1;

            // Unity documentation says resizing the vertices will also resize colors, uvs, etc.
            existingMesh.vertices = verts;
            existingMesh.uv = uvs;
            existingMesh.colors = colors;
            existingMesh.triangles = indices;
        }

        public static void UpdateQuadInMesh(
          int meshVertexStartIndex,
          int blockIndex,
          RectInt texelRectInOutputTex,
          int outputTexWidth,
          int outputTexHeight,
          Mesh existingMesh)
        {
            float invTexWidth = 1.0f / outputTexWidth;
            float invTexHeight = 1.0f / outputTexHeight;

            // Transform the "row and column" into clip space [-1 to 1] for the rectangle origins
            // 4 positions per quad rect change with blend shapes)
            Vector3[] quadPositions = new Vector3[NUM_VERTS_PER_QUAD];

            // Convert from "texels" to clip space
            // origin
            quadPositions[0] = new Vector3(texelRectInOutputTex.xMin * invTexWidth, texelRectInOutputTex.yMin * invTexHeight, Z_POSITION) * 2.0f - Vector3.one;
            // "x corner"
            quadPositions[1] = new Vector3(texelRectInOutputTex.xMax * invTexWidth, texelRectInOutputTex.yMin * invTexHeight, Z_POSITION) * 2.0f - Vector3.one;
            // "y corner"
            quadPositions[2] = new Vector3(texelRectInOutputTex.xMin * invTexWidth, texelRectInOutputTex.yMax * invTexHeight, Z_POSITION) * 2.0f - Vector3.one;
            // "opposite corner"
            quadPositions[3] = new Vector3(texelRectInOutputTex.xMax * invTexWidth, texelRectInOutputTex.yMax * invTexHeight, Z_POSITION) * 2.0f - Vector3.one;

            Vector3[] existingVerts = existingMesh.vertices;
            Vector2[] existingUvs = existingMesh.uv;
            Color[] existingColors = existingMesh.colors;

            // Now, add the quad

            // Just encode block index, there is too much information to encode into 4 channel color,
            // so instead the shader will just use a buffer to store the data
            Color encodedValues = new Color(blockIndex, 0.0f, 0.0f, 0.0f);

            existingVerts[meshVertexStartIndex + 0] = quadPositions[0];
            existingVerts[meshVertexStartIndex + 1] = quadPositions[1];
            existingVerts[meshVertexStartIndex + 2] = quadPositions[2];
            existingVerts[meshVertexStartIndex + 3] = quadPositions[3];

            existingUvs[meshVertexStartIndex + 0] = new Vector2(0.0f, 0.0f);
            existingUvs[meshVertexStartIndex + 1] = new Vector2(1.0f, 0.0f);
            existingUvs[meshVertexStartIndex + 2] = new Vector2(0.0f, 1.0f);
            existingUvs[meshVertexStartIndex + 3] = new Vector2(1.0f, 1.0f);

            existingColors[meshVertexStartIndex + 0] = encodedValues;
            existingColors[meshVertexStartIndex + 1] = encodedValues;
            existingColors[meshVertexStartIndex + 2] = encodedValues;
            existingColors[meshVertexStartIndex + 3] = encodedValues;

            // Update mesh
            existingMesh.vertices = existingVerts;
            existingMesh.uv = existingUvs;
            existingMesh.colors = existingColors;
        }
    }
}
