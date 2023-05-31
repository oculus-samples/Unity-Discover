Shader "Venues/Environment/CheapoPBR"
{
	Properties
	{
		_Color ("Color Tint", Color) = (1, 1, 1, 1)
		[NoScaleOffset] _MainTex ("Base Texture || (A) for Metallic", 2D) = "white" {}

		_Metallic ("Global Metallic", Range(0, 1)) = 0.0
		_Roughness ("Global Roughness", Range(0, 1)) = 0.5

		[Header(Emission)]
		[Toggle(ENABLE_EMISSION)] _EnableEmission("Enable Emission", Float) = 1
		[HideIf(ENABLE_EMISSION, false)][NoScaleOffset] _EmissionTex("Emission Texture || (A) for Roughness", 2D) = "white" {}
		[HideIf(ENABLE_EMISSION, false)] _EmissionIntensity("Emission Intensity", Range(0, 2)) = 1

		[Header(Environment)]
		[Toggle(ENABLE_AMBIENT)] _EnableAmbient("Enable Video Illumination", Float) = 1
		[HideIf(ENABLE_AMBIENT, false)] _AmbientIntensity("Video Illumination Intensity", Range(0, 2)) = 1
		[Toggle(ENABLE_REFLECTIONS)] _EnableReflections("Enable Reflections", Float) = 1
		[HideIf(ENABLE_REFLECTIONS, false)][NoScaleOffset] _ReflectionCubemap("Reflection Cubemap", Cube) = "white" {}
		[HideIf(ENABLE_REFLECTIONS, false)]_ReflectionIntensity("Reflection Intensity", Range(0, 2)) = 1
		[Toggle(ENABLE_FOG)] _EnableFog("Enable Fog", Float) = 1

		[Header(Rim Light)]
		[HDR] _RimColor ("Rim Tint", Color) = (0.5, 0.5, 0.5, 1)
		_RimWidth ("Rim Width", Range(0, 1)) = 0.4
		_RimWeight ("Rim Weight", Range(0.0001, 2)) = 0.55

	}
	SubShader
	{
		// Use this with Universal Rendering Pipeline (URP)
		// Tags{ "RenderType"="Opaque" "LightMode" = "LightweightForward"}
		// Use this without Universal Rendering Pipeline (URP)
		Tags { "RenderType"="Opaque" "LightMode" = "ForwardBase"}

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog

			#include "UnityCG.cginc"

			#pragma shader_feature ENABLE_EMISSION
			#pragma shader_feature ENABLE_AMBIENT
			#pragma shader_feature ENABLE_REFLECTIONS
			#pragma shader_feature ENABLE_FOG

			struct appdata
			{
				half4 vertex : POSITION;
				half2 uv : TEXCOORD0;
				half3 normal : NORMAL;
				half4 vertColor : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				half2 uv : TEXCOORD0;
				half4 vertex : SV_POSITION;
				half4 vertColor : COLOR;
				half3 reflection : TEXCOORD1;
				half3 rim : TEXCOORD2;
				UNITY_FOG_COORDS(3)
				UNITY_VERTEX_OUTPUT_STEREO
			};

			half4 _Color;
			sampler2D _MainTex;

			#if ENABLE_EMISSION
				sampler2D _EmissionTex;
			#endif

			#if ENABLE_REFLECTIONS
				UNITY_DECLARE_TEXCUBE(_ReflectionCubemap);
			#endif

			half _AmbientIntensity, _EmissionIntensity, _ReflectionIntensity;
			half _Metallic, _Roughness;

			half4 _RimColor;
			half _RimWidth, _RimWeight;


			v2f vert (appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;

				// Vertex color & tint
				o.vertColor = v.vertColor * _Color;

				// Reflections
				half3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				half3 worldNormal = UnityObjectToWorldNormal(v.normal);
				half3 worldViewDir = normalize(UnityWorldSpaceViewDir(worldPos)); //Direction of ray from the camera towards the object surface
				o.reflection = reflect(-worldViewDir, worldNormal); // Direction of ray after hitting the surface of object
				_Roughness *= (1.7 - (0.7 * _Roughness)); // some standard shader pbr thing

				#if ENABLE_AMBIENT
					// Probes for video illumination
					o.vertColor.rgb *= ShadeSH9(float4(worldNormal, 1)) * _AmbientIntensity;
				#endif

				// Rim
				o.rim = pow(smoothstep(_RimWidth, 0, dot(worldNormal, worldViewDir)), 1 / _RimWeight);
				o.rim *= _RimColor;

				#ifdef ENABLE_FOG
					UNITY_TRANSFER_FOG(o, o.vertex);
				#endif

				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				// Main Texture
				fixed4 mainTex = tex2D(_MainTex, i.uv);
				fixed3 col = mainTex.rgb;
				_Metallic = _Metallic * mainTex.a; // metallic value stored in main texture alpha

				#if ENABLE_EMISSION
					fixed4 emission = tex2D(_EmissionTex, i.uv);
					_Roughness = _Roughness * emission.a; // roughness value stored in emissive texture alpha
				#endif

				// Vertex color, tint, & ambient probes, metallic overrides all
				col *= lerp(i.vertColor.rgb, 1, _Metallic);

				// Reflections & Rim Light
				half3 rimMultiplier = half3(1., 1., 1.);
				#if ENABLE_REFLECTIONS
					half4 refl = UNITY_SAMPLE_TEXCUBE_LOD(_ReflectionCubemap, i.reflection, _Roughness * 6);
					col = lerp(col, col * (refl.rgb * _ReflectionIntensity), (_Metallic * 0.75) + 0.25);
					rimMultiplier = refl.rgb;
				#endif
				col += i.rim * (1.2 - _Roughness) * rimMultiplier;

				// Emission
				#if ENABLE_EMISSION
					col += emission.rgb * _EmissionIntensity;
				#endif

				// Fog
				#ifdef ENABLE_FOG
					UNITY_APPLY_FOG(i.fogCoord, col);
				#endif

				return fixed4(col, 1);
			}
			ENDCG
		}
	}
}
