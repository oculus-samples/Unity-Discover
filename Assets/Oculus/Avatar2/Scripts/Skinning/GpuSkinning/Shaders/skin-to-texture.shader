// SkinToTexture shader:
// - skins default model verts to a skeleton and encodes pos/norm into textures

Shader "Avatar/SkinToTexture"
{
    Properties
    {
        [ShowIfKeyword(OVR_SKINNING_QUALITY_1_BONE, OVR_SKINNING_QUALITY_2_BONES, OVR_SKINNING_QUALITY_4_BONES)]
        [NoScaleOffset] u_JointsTex("Joints Source Texture", 2DArray) = "black" {}

        [ShowIfKeyword(OVR_HAS_MORPH_TARGETS, OVR_HAS_MORPH_TARGETS_INDIRECTION_TEXTURE)]
        [NoScaleOffset] u_CombinedMorphTargetsTex("MorphTargets Combined Texture", 2DArray) = "black" {}

        [ShowIfKeyword(OVR_HAS_MORPH_TARGETS_INDIRECTION_TEXTURE)]
        [NoScaleOffset] u_IndirectionTex("Vert Indirection Texture", 2DArray) = "black" {}

        [NoScaleOffset] u_NeutralPoseTex("Neutral Pose Texture", 2DArray) = "black" {}

        // Will set "HAS_JOINTS" shader keyword when set.
        [Toggle(OVR_HAS_MORPH_TARGETS_INDIRECTION_TEXTURE)] _HAS_MORPH_TARGETS ("Has MT?", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderQueue"="Geometry" }
        LOD 100

        Pass
        {
            Cull Off
            Blend One Zero
            ZTest Off
            ZWrite Off
            ZClip Off

            CGPROGRAM
            #pragma vertex VertShader
            #pragma fragment FragShader

            #pragma require compute

            #pragma multi_compile ___ OVR_HAS_MORPH_TARGETS OVR_HAS_MORPH_TARGETS_INDIRECTION_TEXTURE
            #pragma multi_compile ___ OVR_SKINNING_QUALITY_1_BONE OVR_SKINNING_QUALITY_2_BONES OVR_SKINNING_QUALITY_4_BONES
            #pragma multi_compile ___ OVR_OUTPUT_SCALE_BIAS

            #include "UnityCG.cginc"

            #define OVR_HAS_JOINTS (defined(OVR_SKINNING_QUALITY_1_BONE) || defined(OVR_SKINNING_QUALITY_2_BONES) || defined(OVR_SKINNING_QUALITY_4_BONES))

            #define OVR_TEXTURE_PRECISION_FLOAT // hard coding this for now to test perf

            // NOTE: According to Unity documentation here https://docs.unity3d.com/Manual/SL-DataTypesAndPrecision.html
            // The standard declaration of Texture2DArray yields the following
            // "For mobile platforms, these translate into “low precision samplers”, i.e. the textures are expected to
            // have low precision data in them."
            // Upon shader inspection, the declarations become "uniform mediump sampler2DArray" which
            // is 16-bit precision. This is not desired as some of the data in the textures is
            // expected to have 32-bit precision. So, for mobile platforms, make an option for explicitly
            // setting 32-bit precision
            #if defined(SHADER_API_MOBILE) && defined(OVR_TEXTURE_PRECISION_FLOAT)
                #define OVR_DECLARE_TEX2DARRAY(tex) Texture2DArray_float tex; SamplerState sampler##tex
            #else
                #define OVR_DECLARE_TEX2DARRAY(tex) UNITY_DECLARE_TEX2DARRAY(tex)
            #endif

            struct appdata {
                float4 a_Position : POSITION;
                float2 a_UV1 : TEXCOORD0;
                float4 a_Color : COLOR;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float3 v_NeutralPoseTexUv : TEXCOORD0;
#if defined(OVR_HAS_JOINTS)
                float3 v_JointsTexUv : TEXCOORD1;
                nointerpolation uint f_JointsStartIndex : TEXCOORD2;
#endif

#if defined(OVR_HAS_MORPH_TARGETS_INDIRECTION_TEXTURE)
                // w is index
                float3 v_IndirectionTexUv : TEXCOORD3;
#elif defined(OVR_HAS_MORPH_TARGETS)
                float3 v_CombinedMorphTargetsUv: TEXCOORD3;
#endif
            };

            struct PerBlockData {
              float4 neutralPoseTexUvRect;
              float4 indicesAndSlices;

#if defined(OVR_HAS_JOINTS)
              float4 jointsTexUvRect;
#endif

#if defined(OVR_HAS_MORPH_TARGETS_INDIRECTION_TEXTURE)
              float4 indirectionTexUvRect;
#elif defined(OVR_HAS_MORPH_TARGETS)
              float4 combinedMorphTargetsUvRect;
#endif
            };

            struct JointData
            {
                float4x4 transform;
                float4x4 normalTransform;
            };

            // Textures
            OVR_DECLARE_TEX2DARRAY( u_NeutralPoseTex );

#if defined(OVR_HAS_JOINTS)
            OVR_DECLARE_TEX2DARRAY( u_JointsTex );
#endif

#if defined(OVR_HAS_MORPH_TARGETS) || defined(OVR_HAS_MORPH_TARGETS_INDIRECTION_TEXTURE)
            OVR_DECLARE_TEX2DARRAY( u_CombinedMorphTargetsTex );
#if defined(OVR_HAS_MORPH_TARGETS_INDIRECTION_TEXTURE)
            OVR_DECLARE_TEX2DARRAY( u_IndirectionTex );
#endif
#endif

            uniform StructuredBuffer<PerBlockData> u_BlockData;
            uniform StructuredBuffer<JointData> u_JointMatrices;

            uniform float u_BlockEnabled;

#if OVR_OUTPUT_SCALE_BIAS
            uniform float2 u_OutputScaleBias;
#endif

            uniform int u_JointOffset;

            float3 getUvForTexture(float4 uvRect, float texSlice, float2 a_UV1)
            {
              float3 result;

              // Use the attribute UV as a interpolation value
              // between the min and max
              float minU = uvRect.x;
              float rangeU = uvRect.z;
              float minV = uvRect.y;
              float rangeV = uvRect.w;

              result.x = minU + (rangeU * a_UV1.x);
              result.y = minV + (rangeV * a_UV1.y);
              result.z = texSlice;

              return result;
            }

            v2f VertShader(appdata vIn)
            {
              v2f output;

              float4 pos = float4(vIn.a_Position.xyz, 1.0);
#if UNITY_UV_STARTS_AT_TOP
              // Unity is trying to be "helpful" and unify DX v OGL coordinates,
              // Unfortunately it does a very poor job and just makes things worse
              // Because our quad center is (0,0) this effectively flips them vertically
              pos.y = -pos.y;
#endif

              int blockIndex = int(vIn.a_Color.r);
              // Generate degnerate triangle if not enabled
              output.pos = pos * step(0.0, u_BlockEnabled);

              // Grab blockdata from array by index
              PerBlockData blockData = u_BlockData[blockIndex];

              // Create UV for neutral pose tex
              output.v_NeutralPoseTexUv = getUvForTexture(blockData.neutralPoseTexUvRect, blockData.indicesAndSlices.g, vIn.a_UV1).xyz;

#if defined(OVR_HAS_JOINTS)
              // Create UV for joints texture
              output.v_JointsTexUv = getUvForTexture(blockData.jointsTexUvRect, blockData.indicesAndSlices.b, vIn.a_UV1).xyz;

              // Pass a float (non interpolated) joints start index
              output.f_JointsStartIndex = uint(blockData.indicesAndSlices.r);
#endif

#if defined(OVR_HAS_MORPH_TARGETS_INDIRECTION_TEXTURE)
              // Shader model doesn't allow vertex texture fetch - boo
              output.v_IndirectionTexUv = getUvForTexture(blockData.indirectionTexUvRect, blockData.indicesAndSlices.a, vIn.a_UV1);
              //output.v_IndirectionTexUv.w = blockData.indicesAndSlices.a;
#elif defined(OVR_HAS_MORPH_TARGETS)
              output.v_CombinedMorphTargetsUv = getUvForTexture(blockData.combinedMorphTargetsUvRect, blockData.indicesAndSlices.a, vIn.a_UV1);
#endif

              return output;
            }

            float4x4 GetJointMatrixForAttribute(uint jointIndex, bool isAttributeNormal) {
              uint jIndex = jointIndex + u_JointOffset;
              return isAttributeNormal ? u_JointMatrices[jIndex].normalTransform
                                       : u_JointMatrices[jIndex].transform;
            }

            float4 FragShader(v2f input) : SV_Target
            {
                // Sample from neutral pose tex to get the "base line"
                float4 attributeData = UNITY_SAMPLE_TEX2DARRAY(u_NeutralPoseTex, input.v_NeutralPoseTexUv.xyz);
                bool isPosition = attributeData.a == 1.0;
                bool isNormal = attributeData.a == 0.0;
                bool isTangent = !isPosition && !isNormal;

                // Tangents have either a 0.25 or 0.75 stored in alpha, so convert that
                // to -1 or 1
                float tangentOutputAlpha = step(0.5, attributeData.a) * 2.0 - 1.0;
                attributeData.a = isPosition ? 1.0 : 0.0;

                float outputAlpha = isTangent ? tangentOutputAlpha : attributeData.a;

                // If morph targets are available, apply morph targets, normalizing for non
                // position attributes
#if defined(OVR_HAS_MORPH_TARGETS) || defined(OVR_HAS_MORPH_TARGETS_INDIRECTION_TEXTURE)

  #if defined(OVR_HAS_MORPH_TARGETS_INDIRECTION_TEXTURE)
                float3 morphTargetUv = UNITY_SAMPLE_TEX2DARRAY(u_IndirectionTex, input.v_IndirectionTexUv.xyz).xyz;
  #elif defined(OVR_HAS_MORPH_TARGETS)
                float3 morphTargetUv = input.v_CombinedMorphTargetsUv;
  #endif // OVR_HAS_MORPH_TARGETS

                float3 morphTargetDelta = UNITY_SAMPLE_TEX2DARRAY(u_CombinedMorphTargetsTex, morphTargetUv.xyz).xyz;

                attributeData.xyz += morphTargetDelta;
                // Apply delta and normalize if needed
                attributeData.xyz = isPosition ? attributeData.xyz : normalize(attributeData.xyz);
#endif // defined(OVR_HAS_MORPH_TARGETS) || defined(OVR_HAS_MORPH_TARGETS_INDIRECTION_TEXTURE)

                // If joints are available multiply by matrices
                #if defined(OVR_HAS_JOINTS)
                    float4 skinningData = UNITY_SAMPLE_TEX2DARRAY(u_JointsTex, input.v_JointsTexUv.xyz);
                    float4 boneIndices = floor(skinningData);
                    float4 boneWeights = (skinningData - boneIndices) * 2.0; // * 2 here because the weights are stored in range of 0 -> 0.5

                    boneIndices = boneIndices + input.f_JointsStartIndex;

                    // The weights used should all sum up to 1.0 to prevent distortion
                    // In cases where the encoded data encodes up to 4 weights, but
                    // less are used, the weights may have to be recalculated
                    float sumOfWeights = boneWeights.x;
                    #if defined(OVR_SKINNING_QUALITY_2_BONES) || defined(OVR_SKINNING_QUALITY_4_BONES)
                        sumOfWeights += boneWeights.y;
                        #if defined(OVR_SKINNING_QUALITY_4_BONES)
                            sumOfWeights += boneWeights.z + boneWeights.w;
                        #endif // defined(OVR_SKINNING_QUALITY_4_BONES)
                    #endif // defined(OVR_SKINNING_QUALITY_2_BONES) || defined(OVR_SKINNING_QUALITY_4_BONES)

                    boneWeights /= sumOfWeights;

                    float4x4 blendedMatrix = GetJointMatrixForAttribute(boneIndices.x, isNormal) * boneWeights.x;
                    #if defined(OVR_SKINNING_QUALITY_2_BONES) || defined(OVR_SKINNING_QUALITY_4_BONES)
                        [branch]
                        if(boneWeights.y > 0) {
                            blendedMatrix += GetJointMatrixForAttribute(boneIndices.y, isNormal) * boneWeights.y;
                        }
                        #if defined(OVR_SKINNING_QUALITY_4_BONES)
                            [branch]
                            if(boneWeights.z > 0) {
                                blendedMatrix += GetJointMatrixForAttribute(boneIndices.z, isNormal) * boneWeights.z;
                            }

                            [branch]
                            if(boneWeights.w > 0) {
                                blendedMatrix += GetJointMatrixForAttribute(boneIndices.w, isNormal) * boneWeights.w;
                            }
                        #endif // OVR_SKINNING_QUALITY_4_BONES
                    #endif // defined(OVR_SKINNING_QUALITY_2_BONES) || defined(OVR_SKINNING_QUALITY_4_BONES)

                    attributeData = mul(blendedMatrix, attributeData);
                #endif // OVR_HAS_JOINTS

                float4 output;
                output.rgb = attributeData.rgb;
                output.a = outputAlpha;

                #if OVR_OUTPUT_SCALE_BIAS
                    output = output * u_OutputScaleBias.x + u_OutputScaleBias.y;
                #endif

                return output;
            }

            ENDCG
        } // Pass
    } // SubShader
} // Shader
