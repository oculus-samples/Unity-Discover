#include "UnityCG.cginc"
#include "../../../../Scripts/ShaderUtils/AvatarCustom.cginc"
#include "AvatarCommon/AvatarShaderFramework.cginc"
#include "AvatarCommon/AvatarPropertiesMap.cginc"

// Define some variables for holding main tex and effects maps samples (if needed)
// through all functions
half4 mainTexSample;
half4 effectsMapSample;
float2 mainTexUV;
half3 vertexColor;

#if defined(_SHADER_TYPE_SOLID_COLOR)
    #include "AvatarSolidColor/AvatarSolidColorProperties.cginc"
    #include "AvatarSolidColor/AvatarSolidColorAlbedo.cginc"

    half3 GetAlbedo() {
        return SolidColorAlbedo(GetPrimaryColor().rgb);
    }

#elif defined(_SHADER_TYPE_SUBMESH) || defined(_SHADER_TYPE_TEXTURED)
    #include "AvatarTextured/AvatarTexturedProperties.cginc"
    #include "AvatarTextured/AvatarTexturedAlbedo.cginc"

#ifdef MATERIAL_MODE_TEXTURE
    #define SAMPLE_MAIN_TEX 1
#endif

    half3 GetAlbedo() {
        return TexturedAlbedo(mainTexSample, vertexColor, GetPrimaryColor().rgb);
    }

#elif defined(_SHADER_TYPE_SKIN)
    #include "AvatarSkin/AvatarSkinProperties.cginc"
    #include "AvatarSkin/AvatarSkinAlbedo.cginc"
    #include "AvatarSkin/AvatarSkinSurfaceFunctions.cginc"
    #include "AvatarSkin/AvatarSkinLighting.cginc"

    #define SAMPLE_MAIN_TEX 1
    #define SAMPLE_EFFECTS_MAP 1

    half3 GetAlbedo() {
        return SkinAlbedo(mainTexSample, vertexColor, GetTertiaryColor(), effectsMapSample.b);
    }

#elif defined(_SHADER_TYPE_HAIR)
    #include "AvatarHair/AvatarHairProperties.cginc"
    #include "AvatarHair/AvatarHairAlbedo.cginc"

    #define SAMPLE_EFFECTS_MAP 1

    half3 GetAlbedo() {
        return HairAlbedo(effectsMapSample, GetPrimaryColor().rgb, GetSecondaryColor().rgb);
    }

#elif defined(_SHADER_TYPE_LEFT_EYE) || defined(_SHADER_TYPE_RIGHT_EYE)
    #if defined(USE_HEAD_C)
        #include "AvatarEye/AvatarEyePropertiesHeadC.cginc"
        #include "AvatarEye/AvatarEyeAlbedoHeadC.cginc"
        #include "AvatarEye/AvatarEyeInterpolatorsHeadC.cginc"

        // Used when calculating main texture coordinates in pixel shader
        #define CALCULATE_MAIN_TEX_COORDS 1


        // Calculate per pixel texture coordinates
        float2 GetMainTexCoords(float2 inputUV) {
            #if defined(_SHADER_TYPE_LEFT_EYE)
                return GetNormalizedUVForLeftEye(inputUV);
                #else
                return GetNormalizedUVForRightEye(inputUV);
                #endif
        }

        half3 GetAlbedo() {
            return EyeAlbedo(mainTexSample, vertexColor, GetPrimaryColor().rgb, mainTexUV);
        }

    #else
        #include "AvatarEye/AvatarEyeProperties.cginc"
        #include "AvatarEye/AvatarEyeAlbedo.cginc"
        #include "AvatarEye/AvatarEyeInterpolators.cginc"


        half3 GetAlbedo() {
            return EyeAlbedo(mainTexSample, vertexColor, GetPrimaryColor().rgb);
        }
    #endif

    #define SAMPLE_MAIN_TEX 1


#endif

#if defined(_LIGHTING_SYSTEM_UNITY) || defined(_LIGHTING_SYSTEM_VERTEX_GI)
    #include "AvatarCommon/AvatarCommonSurfaceFields.cginc"
#if defined(_SHADER_TYPE_SUBMESH)
    #include "AvatarSubmesh/AvatarSubmeshLighting.cginc"
#else
    #include "AvatarCommon/AvatarCommonLighting.cginc"
#endif

    ///////////////////////////////
    // Surface Related Functions //
    ///////////////////////////////

    struct AvatarComponentSurfaceOutput {
        AVATAR_COMMON_SURFACE_FIELDS

        // Skin component needs more fields
        #if defined(_SHADER_TYPE_SKIN)
            SURFACE_ADDITIONAL_FIELDS_SKIN
        #endif

        #if defined(_SHADER_TYPE_SUBMESH)
            float SubMeshType;
        #endif
    };

    void Surf(v2f IN, inout AvatarComponentSurfaceOutput o) {

        // All shader types have properties map
        half4 props = SamplePropertiesMap(IN.propertiesMapUV, IN.ormt);

        #if defined(SAMPLE_MAIN_TEX)
            #if defined(CALCULATE_MAIN_TEX_COORDS)
                mainTexUV = GetMainTexCoords(IN.uv);
                mainTexSample = SampleMainTex(mainTexUV);
            #else
                mainTexUV = IN.uv;
                mainTexSample = SampleMainTex(IN.uv);
            #endif
        #endif

        #ifdef MATERIAL_MODE_VERTEX
          vertexColor = IN.color.rgb;
        #endif

        #if defined(SAMPLE_EFFECTS_MAP)
            effectsMapSample = SampleEffectsMap(IN.effectsMapUV);
        #endif

        SET_AVATAR_SHADER_SURFACE_COMMON_FIELDS(
            o,
            GetAlbedo(),
            props.g, // Roughness in green channel
            props.b, // Metallic in blue channel
            1.0, // Alpha of 1 for now
            props.r, // Occluson in red channel
            OffsetAndScaleMinimumDiffuse(_MinDiffuse)) // convert min diffuse to [-1,1]

        #if defined(_SHADER_TYPE_SUBMESH)
            o.SubMeshType = IN.color.a;
        #endif

        // Skin has some additional fields that need populating
        #if defined(_SHADER_TYPE_SKIN)
            half backlightScale = _BacklightScale * (1.0 - effectsMapSample.g); /* Backlight scale i.e. "Should this pixel use backlight" in green channel of effects map */
            o.Thickness = effectsMapSample.r; /* Thickness stored in red channel of effects map */
            o.BacklightScale = backlightScale;
            o.TranslucencyColor = GetPrimaryColor();
            o.BacklightColor = GetSecondaryColor();
        #endif
    }

    // Surf function has different signatures depending on lighting system
    #if defined(_LIGHTING_SYSTEM_VERTEX_GI)
        void Surf(v2f IN, inout AvatarComponentSurfaceOutput o, vgi_frag_tmp tmp) {
            SET_AVATAR_SHADER_SURFACE_NORMAL_FIELD(o, tmp.normal);
            Surf(IN, o);
            }
    #endif

    ///////////////////////
    // Lighting Function //
    ///////////////////////

    half4 LightingAvatarComponent(AvatarComponentSurfaceOutput s, half3 viewDir, AvatarShaderGlobalIllumination gi) {
        AVATAR_SHADER_DECLARE_COMMON_LIGHTING_PARAMS(albedo, normal, perceptualRoughness, perceptualSmoothness, metallic, alpha, minDiffuse, s)

#if defined(_SHADER_TYPE_SUBMESH)
        float subMeshType = s.SubMeshType;
#endif

        half4 c = 0.;

        #if defined(_SHADER_TYPE_SKIN)
            c.rgb = AvatarSkinLighting(
                albedo,
                normal,
                viewDir,
                minDiffuse,
                perceptualRoughness,
                perceptualSmoothness,
                metallic,
                s.Thickness,
                s.BacklightScale,
                s.TranslucencyColor,
                s.BacklightColor,
                s.Occlusion,
                gi);
        #elif defined(_SHADER_TYPE_SUBMESH)
            c.rgb = AvatarLightingSubmesh(
                albedo,
                normal,
                subMeshType,
                viewDir,
                minDiffuse,
                perceptualRoughness,
                perceptualSmoothness,
                metallic,
                directOcclusion,
                gi);
        #else
            // Common PBS lighting
            c.rgb = AvatarLightingCommon(
                albedo,
                normal,
                viewDir,
                minDiffuse,
                perceptualRoughness,
                perceptualSmoothness,
                metallic,
                directOcclusion,
                gi);
        #endif

        #if defined(DESAT)
            c.rgb = Desat(c.rgb);
        #endif

        #if defined(DEBUG_TINT)
            c.rgb *= _DebugTint.rgb;
        #endif

        c.a = alpha;
        return c;
    }
#endif

//////////////////
// Interpolator //
//////////////////

void Interpolate(float2 texcoord, inout v2f o) {
    // _MainTex texture coordinates computations (if applicable)
    #if defined(_SHADER_TYPE_LEFT_EYE) && !defined(USE_HEAD_C)
        o.uv.xy = GetNormalizedUVForLeftEye(texcoord);
        o.propertiesMapUV.xy = texcoord; // properties map and main tex have same UVs
    #elif defined(_SHADER_TYPE_RIGHT_EYE) && !defined(USE_HEAD_C)
        o.uv.xy = GetNormalizedUVForRightEye(texcoord);
        o.propertiesMapUV.xy = texcoord; // properties map and main tex have same UVs
    #endif
}

#if defined(_LIGHTING_SYSTEM_VERTEX_GI)
    // "Generic" interpolation function that fits the function
    // prototype to be valled from the vertex shader for vertex GI lighting system
    void Interpolate(appdata v, vgi_vert_tmp tmp, inout v2f o) {
        // Call specific interpolation function for this shader
        Interpolate(v.uv, o);
    }
#else
    // "Generic" interpolation function that fits the function
    // prototype to be valled from the vertex shader for non-vertex GI lighting system
    void Interpolate(appdata v, OvrVertexData vertexData, inout v2f o) {
        // Call specific interpolation function for this shader
        Interpolate(v.uv, o);
    }
#endif

struct Light
{
    float3 direction;
    float range;

    float3 color;
    float intensity;

    float3 position;
    float innerConeCos;

    float outerConeCos;
    int type;

    float2 padding;
};

struct MaterialProperties
{
    half3 albedo;
    half3 normal;
    half minDiffuse;
    half smoothness;
    half metallic;
    half occlusion;
    half roughness;
    half directOcclusion;
    half oneMinusReflectivity;
    half3 specColor;
    half3 diffColor;
};

MaterialProperties GetMaterialProperties(v2f IN)
{
    MaterialProperties props;
    AVATAR_SHADER_FRAG_INIT_AND_CALL_SURFACE(Surf, AvatarComponentSurfaceOutput);

    props.albedo = GET_AVATAR_SHADER_SURFACE_ALBEDO_FIELD(o);
    props.normal = GET_AVATAR_SHADER_SURFACE_NORMAL_FIELD(o);
    props.minDiffuse = GET_AVATAR_SHADER_SURFACE_MIN_DIFFUSE_FIELD(o);
    props.smoothness = GET_AVATAR_SHADER_SURFACE_SMOOTHNESS_FIELD(o);
    props.metallic = GET_AVATAR_SHADER_SURFACE_METALLIC_FIELD(o);
    props.occlusion = GET_AVATAR_SHADER_SURFACE_OCCLUSION_FIELD(o);
    props.roughness = GET_AVATAR_SHADER_SURFACE_ROUGHNESS_FIELD(o);
    props.directOcclusion = lerp(1.0f, props.occlusion, _DirectOcclusionEffect);

    half3 specColor;
    half oneMinusReflectivity;
    props.diffColor = DiffuseAndSpecularFromMetallic(
        props.albedo,
        props.metallic,
        specColor, /*out*/
        oneMinusReflectivity); /*out*/

    props.specColor = specColor;
    props.oneMinusReflectivity = oneMinusReflectivity;

    return props;
}

half3 DirectAvatarLighting(
    half3 viewDir,
    MaterialProperties props,
    Light light)
{
    float3 halfDirection = Unity_SafeNormalize(light.direction + viewDir);
    half NdotV = saturate(dot(props.normal, viewDir));
    float NdotH = saturate(dot(props.normal, halfDirection));
    float LdotH = saturate(dot(light.direction, halfDirection));
    half rawNdotL = clamp(dot(props.normal, light.direction), -1.0, 1.0); // -1 to 1
    half NdotL = saturate(rawNdotL); // 0 to 1

    half3 directDiffuse = DirectDiffuseLightingWithOcclusion(
        props.diffColor,
        rawNdotL,
        props.minDiffuse,
        props.directOcclusion);

    half3 directSpecular = DirectSpecularLightingWithOcclusion(
        props.specColor,
        NdotH,
        LdotH,
        NdotL,
        props.roughness,
        props.directOcclusion);

    return (directDiffuse + directSpecular) * light.color;
}

Light GetUnityLight(float3 worldPos, float3 lightDir, fixed atten)
{
    Light l;
    float3 pos = _WorldSpaceLightPos0;
    float3 toLight = _WorldSpaceLightPos0.xyz - worldPos;
    float3 pointdir = normalize(toLight);
    float attenuation = 1 / (1 + dot(pointdir, pointdir));

    // instead of using the original attentuation algorithm we utilize the common Unity pattern with vertex shader calculation
    // l.type = _WorldSpaceLightPos0.w < 0.5 ? LightType_Directional : LightType_Point;
    //l.type = LightType_Directional;

#if defined(DIRECTIONAL)
        l.direction = lightDir;
        l.range = -1.0;
        l.color = _LightColor0; // includes both the color and amplitude, not normalized
        l.intensity = 5.0;  // 1.0 in Unity is 5klux, also 0.2 intensity in Unity = 1.0 intensity in GLTF, this will be modulated by the color anyways
        l.position = pos;
        l.innerConeCos = 1.0;
        l.outerConeCos = 0.5;
#else
    float3 lightCoord = mul(unity_WorldToLight, float4(worldPos, 1)).xyz;
    float range = length(toLight) / length(lightCoord);

    l.direction = lightDir;
    l.range = range;
    l.color = _LightColor0 * atten; // includes both the color and amplitude, not normalized
    l.intensity = 5.0; // 1.0 in Unity is 5klux, also 0.2 intensity in Unity = 1.0 intensity in GLTF, this will be modulated by the color anyways
    l.position = pos;
    l.innerConeCos = 1.0;
    l.outerConeCos = 0.5;
#endif

    return l;
}

//////////////////
//  ForwadBase  //
//////////////////

v2f vertForwardBase(appdata v)
{
    v2f o = AvatarShaderVertInit(v);
    OvrVertexData vertexData = AvatarShaderVertTransform(v, o);
    AvatarShaderVertLighting(v, o);
    Interpolate(v, vertexData, o);
    return o;
}

fixed4 fragForwardBase(v2f IN) : SV_Target
{
    AvatarShaderFragInit(IN);
    float3 worldPos = AvatarShaderFragGetWorldPos(IN);
    fixed3 lightDir = AvatarShaderFragGetLightDir(worldPos);
    float3 worldViewDir = AvatarShaderFragGetWorldViewDir(worldPos);
    AVATAR_SHADER_FRAG_INIT_AND_CALL_SURFACE(Surf, AvatarComponentSurfaceOutput);
    AVATAR_SHADER_FRAG_LIGHTING(LightingAvatarComponent);
}

//////////////////
//   ForwadAdd  //
//////////////////

v2f vertForwardAdd(appdata v)
{
    v2f o = AvatarShaderVertInit(v);
    OvrVertexData vertexData = AvatarShaderVertTransform(v, o);
    AvatarShaderVertLighting(v, o);
    Interpolate(v, vertexData, o);
    return o;
}

fixed4 fragForwardAdd(v2f IN) : SV_Target
{
    AvatarShaderFragInit(IN);
    float3 worldPos = AvatarShaderFragGetWorldPos(IN);
    fixed3 lightDir = AvatarShaderFragGetLightDir(worldPos);
    float3 worldViewDir = AvatarShaderFragGetWorldViewDir(worldPos);

    MaterialProperties props = GetMaterialProperties(IN);

    /* compute lighting & shadowing factor */
    UNITY_LIGHT_ATTENUATION(atten, IN, worldPos)
    /* setup lighting environment */
    Light light = GetUnityLight(worldPos, lightDir, atten);
    /* realtime lighting: call lighting function */
    fixed4 color = 0;
    color.rgb += DirectAvatarLighting(worldViewDir, props, light);
    /* apply fog */
    UNITY_APPLY_FOG(_unity_fogCoord, color);

    color.rgb = OverideColorWithDebug(color.rgb, IN);
    UNITY_OPAQUE_ALPHA(color.a);

    return color;
}
