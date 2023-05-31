// Upgrade NOTE: upgraded instancing buffer 'customVenuesLobby_HalloweenCenterpiece' to new syntax.

// Made with Amplify Shader Editor
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "custom/VenuesLobby_HalloweenCenterpiece"
{
	Properties
	{
		_FlameFlickerSpeed("Flame Flicker Speed", Float) = 0.28
		_Scale("Scale", Vector) = (8,50,50,0)
		_hallwayCenterpieceHalloween_bc("hallwayCenterpieceHalloween_bc", 2D) = "white" {}
		[HideInInspector] _texcoord( "", 2D ) = "white" {}

	}
	
	SubShader
	{
		
		
		Tags { "RenderType"="Opaque" }
	LOD 100

		CGINCLUDE
		#pragma target 3.0
		ENDCG
		Blend Off
		AlphaToMask Off
		Cull Back
		ColorMask RGBA
		ZWrite On
		ZTest LEqual
		Offset 0 , 0
		
		
		
		Pass
		{
			Name "Unlit"

			// Use this with Universal Rendering Pipeline (URP)
			// Tags{ "LightMode" = "LightweightForward"}
			// Use this without Universal Rendering Pipeline (URP)
			Tags { "LightMode"="ForwardBase" }

			CGPROGRAM

			

			#ifndef UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX
			//only defining to not throw compilation error over Unity 5.5
			#define UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input)
			#endif
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"
			#include "UnityShaderVariables.cginc"
			#define ASE_NEEDS_VERT_COLOR
			#define ASE_NEEDS_FRAG_COLOR
			#define ASE_NEEDS_FRAG_WORLD_POSITION


			struct appdata
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct v2f
			{
				float4 vertex : SV_POSITION;
				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				float3 worldPos : TEXCOORD0;
				#endif
				float4 ase_texcoord1 : TEXCOORD1;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			uniform sampler2D _hallwayCenterpieceHalloween_bc;
			SamplerState sampler_hallwayCenterpieceHalloween_bc;
			UNITY_INSTANCING_BUFFER_START(customVenuesLobby_HalloweenCenterpiece)
				UNITY_DEFINE_INSTANCED_PROP(float4, _hallwayCenterpieceHalloween_bc_ST)
#define _hallwayCenterpieceHalloween_bc_ST_arr customVenuesLobby_HalloweenCenterpiece
				UNITY_DEFINE_INSTANCED_PROP(float3, _Scale)
#define _Scale_arr customVenuesLobby_HalloweenCenterpiece
				UNITY_DEFINE_INSTANCED_PROP(float, _FlameFlickerSpeed)
#define _FlameFlickerSpeed_arr customVenuesLobby_HalloweenCenterpiece
			UNITY_INSTANCING_BUFFER_END(customVenuesLobby_HalloweenCenterpiece)
			float3 mod3D289( float3 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 mod3D289( float4 x ) { return x - floor( x / 289.0 ) * 289.0; }
			float4 permute( float4 x ) { return mod3D289( ( x * 34.0 + 1.0 ) * x ); }
			float4 taylorInvSqrt( float4 r ) { return 1.79284291400159 - r * 0.85373472095314; }
			float snoise( float3 v )
			{
				const float2 C = float2( 1.0 / 6.0, 1.0 / 3.0 );
				float3 i = floor( v + dot( v, C.yyy ) );
				float3 x0 = v - i + dot( i, C.xxx );
				float3 g = step( x0.yzx, x0.xyz );
				float3 l = 1.0 - g;
				float3 i1 = min( g.xyz, l.zxy );
				float3 i2 = max( g.xyz, l.zxy );
				float3 x1 = x0 - i1 + C.xxx;
				float3 x2 = x0 - i2 + C.yyy;
				float3 x3 = x0 - 0.5;
				i = mod3D289( i);
				float4 p = permute( permute( permute( i.z + float4( 0.0, i1.z, i2.z, 1.0 ) ) + i.y + float4( 0.0, i1.y, i2.y, 1.0 ) ) + i.x + float4( 0.0, i1.x, i2.x, 1.0 ) );
				float4 j = p - 49.0 * floor( p / 49.0 );  // mod(p,7*7)
				float4 x_ = floor( j / 7.0 );
				float4 y_ = floor( j - 7.0 * x_ );  // mod(j,N)
				float4 x = ( x_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 y = ( y_ * 2.0 + 0.5 ) / 7.0 - 1.0;
				float4 h = 1.0 - abs( x ) - abs( y );
				float4 b0 = float4( x.xy, y.xy );
				float4 b1 = float4( x.zw, y.zw );
				float4 s0 = floor( b0 ) * 2.0 + 1.0;
				float4 s1 = floor( b1 ) * 2.0 + 1.0;
				float4 sh = -step( h, 0.0 );
				float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
				float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
				float3 g0 = float3( a0.xy, h.x );
				float3 g1 = float3( a0.zw, h.y );
				float3 g2 = float3( a1.xy, h.z );
				float3 g3 = float3( a1.zw, h.w );
				float4 norm = taylorInvSqrt( float4( dot( g0, g0 ), dot( g1, g1 ), dot( g2, g2 ), dot( g3, g3 ) ) );
				g0 *= norm.x;
				g1 *= norm.y;
				g2 *= norm.z;
				g3 *= norm.w;
				float4 m = max( 0.6 - float4( dot( x0, x0 ), dot( x1, x1 ), dot( x2, x2 ), dot( x3, x3 ) ), 0.0 );
				m = m* m;
				m = m* m;
				float4 px = float4( dot( x0, g0 ), dot( x1, g1 ), dot( x2, g2 ), dot( x3, g3 ) );
				return 42.0 * dot( m, px);
			}
			
			//https://www.shadertoy.com/view/XdXGW8
			float2 GradientNoiseDir( float2 x )
			{
				const float2 k = float2( 0.3183099, 0.3678794 );
				x = x * k + k.yx;
				return -1.0 + 2.0 * frac( 16.0 * k * frac( x.x * x.y * ( x.x + x.y ) ) );
			}
			
			float GradientNoise( float2 UV, float Scale )
			{
				float2 p = UV * Scale;
				float2 i = floor( p );
				float2 f = frac( p );
				float2 u = f * f * ( 3.0 - 2.0 * f );
				return lerp( lerp( dot( GradientNoiseDir( i + float2( 0.0, 0.0 ) ), f - float2( 0.0, 0.0 ) ),
						dot( GradientNoiseDir( i + float2( 1.0, 0.0 ) ), f - float2( 1.0, 0.0 ) ), u.x ),
						lerp( dot( GradientNoiseDir( i + float2( 0.0, 1.0 ) ), f - float2( 0.0, 1.0 ) ),
						dot( GradientNoiseDir( i + float2( 1.0, 1.0 ) ), f - float2( 1.0, 1.0 ) ), u.x ), u.y );
			}
			

			
			v2f vert ( appdata v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				UNITY_TRANSFER_INSTANCE_ID(v, o);

				float _FlameFlickerSpeed_Instance = UNITY_ACCESS_INSTANCED_PROP(_FlameFlickerSpeed_arr, _FlameFlickerSpeed);
				float2 appendResult47 = (float2(_FlameFlickerSpeed_Instance , _FlameFlickerSpeed_Instance));
				float3 ase_worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				float2 panner46 = ( 1.0 * _Time.y * appendResult47 + ase_worldPos.xy);
				float3 _Scale_Instance = UNITY_ACCESS_INSTANCED_PROP(_Scale_arr, _Scale);
				float simplePerlin3D3 = snoise( float3( panner46 ,  0.0 )*_Scale_Instance.x );
				simplePerlin3D3 = simplePerlin3D3*0.5 + 0.5;
				float lerpResult60 = lerp( 0.0 , ( simplePerlin3D3 / 150.0 ) , v.color.a);
				float3 temp_cast_3 = (lerpResult60).xxx;
				
				o.ase_texcoord1.xy = v.ase_texcoord.xy;
				o.ase_color = v.color;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				o.ase_texcoord1.zw = 0;
				float3 vertexValue = float3(0, 0, 0);
				#if ASE_ABSOLUTE_VERTEX_POS
				vertexValue = v.vertex.xyz;
				#endif
				vertexValue = temp_cast_3;
				#if ASE_ABSOLUTE_VERTEX_POS
				v.vertex.xyz = vertexValue;
				#else
				v.vertex.xyz += vertexValue;
				#endif
				o.vertex = UnityObjectToClipPos(v.vertex);

				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				#endif
				return o;
			}
			
			fixed4 frag (v2f i ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
				fixed4 finalColor;
				#ifdef ASE_NEEDS_FRAG_WORLD_POSITION
				float3 WorldPosition = i.worldPos;
				#endif
				float4 _hallwayCenterpieceHalloween_bc_ST_Instance = UNITY_ACCESS_INSTANCED_PROP(_hallwayCenterpieceHalloween_bc_ST_arr, _hallwayCenterpieceHalloween_bc_ST);
				float2 uv_hallwayCenterpieceHalloween_bc = i.ase_texcoord1.xy * _hallwayCenterpieceHalloween_bc_ST_Instance.xy + _hallwayCenterpieceHalloween_bc_ST_Instance.zw;
				float4 tex2DNode70 = tex2D( _hallwayCenterpieceHalloween_bc, uv_hallwayCenterpieceHalloween_bc );
				float temp_output_99_0 = ( 1.3 * tex2DNode70.a );
				float4 color88 = IsGammaSpace() ? float4(0.1176471,0.8509804,0.172549,1) : float4(0.01298304,0.6938719,0.02518685,1);
				float4 color2 = IsGammaSpace() ? float4(0.9339623,0.7375837,0.3392221,0) : float4(0.8562991,0.5033876,0.094183,0);
				float4 lerpResult89 = lerp( ( temp_output_99_0 * color88 ) , ( color2 * temp_output_99_0 ) , i.ase_color.r);
				float lerpResult97 = lerp( -20.0 , -12.0 , i.ase_color.r);
				float2 appendResult98 = (float2(0.0 , lerpResult97));
				float2 panner73 = ( 1.0 * _Time.y * appendResult98 + WorldPosition.xy);
				float lerpResult102 = lerp( -0.1 , -0.25 , i.ase_color.r);
				float gradientNoise74 = GradientNoise(panner73,lerpResult102);
				gradientNoise74 = gradientNoise74*0.5 + 0.5;
				float4 lerpResult75 = lerp( tex2DNode70 , ( tex2DNode70 + lerpResult89 ) , gradientNoise74);
				
				
				finalColor = lerpResult75;
				finalColor.a = 1.0f;
				return finalColor;
			}
			ENDCG
		}
	}
	CustomEditor "ASEMaterialInspector"
	
	
}
/*ASEBEGIN
Version=18500
2.666667;146;1636;671;1284.421;1107.462;2.059661;True;True
Node;AmplifyShaderEditor.VertexColorNode;59;-520.7559,514.5302;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SamplerNode;70;-684.084,-923.1321;Inherit;True;Property;_hallwayCenterpieceHalloween_bc;hallwayCenterpieceHalloween_bc;2;0;Create;True;0;0;False;0;False;-1;765eb242ebe51234f85fcf1ce015d038;6052dd3abb528fd4daca3cce03d9069b;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;100;-635.7979,-687.4479;Inherit;False;Constant;_Float0;Float 0;5;0;Create;True;0;0;False;0;False;1.3;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;88;-474.5434,-675.8533;Inherit;False;Constant;_Color1;Color 1;0;0;Create;True;0;0;False;0;False;0.1176471,0.8509804,0.172549,1;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;99;-335.7977,-860.4481;Inherit;False;2;2;0;FLOAT;0.7490196;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;2;-614.1295,-1094.349;Inherit;False;Constant;_Color0;Color 0;0;0;Create;True;0;0;False;0;False;0.9339623,0.7375837,0.3392221,0;0,0,0,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.LerpOp;97;-363.2632,78.53415;Inherit;False;3;0;FLOAT;-20;False;1;FLOAT;-12;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;48;-598.8521,993.535;Inherit;False;InstancedProperty;_FlameFlickerSpeed;Flame Flicker Speed;0;0;Create;True;0;0;False;0;False;0.28;0.28;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;77;-182.4068,-1003.263;Inherit;True;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.WorldPosInputsNode;5;-431.3358,824.8111;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;87;-101.3785,-736.1888;Inherit;True;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.DynamicAppendNode;98;-187.0618,34.37659;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.DynamicAppendNode;47;-389.7454,981.4828;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.WorldPosInputsNode;71;-243.3006,-119.7851;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.LerpOp;89;189.7045,-728.7795;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.PannerNode;73;-42.35918,-56.64176;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;0,-5;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.Vector3Node;52;-179.0262,654.6486;Inherit;False;InstancedProperty;_Scale;Scale;1;0;Create;True;0;0;False;0;False;8,50,50;0.2,50,50;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.LerpOp;102;-23.88721,104.3595;Inherit;False;3;0;FLOAT;-0.1;False;1;FLOAT;-0.25;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PannerNode;46;-197.6858,824.0949;Inherit;False;3;0;FLOAT2;0,0;False;2;FLOAT2;50,50;False;1;FLOAT;1;False;1;FLOAT2;0
Node;AmplifyShaderEditor.NoiseGeneratorNode;74;205.533,-226.8826;Inherit;True;Gradient;True;False;2;0;FLOAT2;1,1;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.NoiseGeneratorNode;3;54.56548,621.7202;Inherit;True;Simplex3D;True;False;2;0;FLOAT3;1,1,1;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;76;616.7811,-606.245;Inherit;True;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;31;327.3663,590.9765;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;150;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;75;859.756,-272.9042;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.LerpOp;60;597.5072,567.2776;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;80;469.007,-215.3068;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.SwitchNode;81;1157.219,-440.9215;Inherit;False;1;2;8;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT;0;False;7;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;0;1337.29,-40.08469;Float;False;True;-1;2;ASEMaterialInspector;100;1;custom/VenuesLobby_HalloweenCenterpiece;0770190933193b94aaa3065e307002fa;True;Unlit;0;0;Unlit;2;True;0;1;False;-1;0;False;-1;0;1;False;-1;0;False;-1;True;0;False;-1;0;False;-1;False;False;False;False;False;False;True;0;False;-1;True;0;False;-1;True;True;True;True;True;0;False;-1;False;False;False;True;False;255;False;-1;255;False;-1;255;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;7;False;-1;1;False;-1;1;False;-1;1;False;-1;True;1;False;-1;True;3;False;-1;True;True;0;False;-1;0;False;-1;True;1;RenderType=Opaque=RenderType;True;2;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=ForwardBase;False;0;;0;0;Standard;1;Vertex Position,InvertActionOnDeselection;1;0;1;True;False;;False;0
WireConnection;99;0;100;0
WireConnection;99;1;70;4
WireConnection;97;2;59;1
WireConnection;77;0;2;0
WireConnection;77;1;99;0
WireConnection;87;0;99;0
WireConnection;87;1;88;0
WireConnection;98;1;97;0
WireConnection;47;0;48;0
WireConnection;47;1;48;0
WireConnection;89;0;87;0
WireConnection;89;1;77;0
WireConnection;89;2;59;1
WireConnection;73;0;71;0
WireConnection;73;2;98;0
WireConnection;102;2;59;1
WireConnection;46;0;5;0
WireConnection;46;2;47;0
WireConnection;74;0;73;0
WireConnection;74;1;102;0
WireConnection;3;0;46;0
WireConnection;3;1;52;0
WireConnection;76;0;70;0
WireConnection;76;1;89;0
WireConnection;31;0;3;0
WireConnection;75;0;70;0
WireConnection;75;1;76;0
WireConnection;75;2;74;0
WireConnection;60;1;31;0
WireConnection;60;2;59;4
WireConnection;80;0;74;0
WireConnection;81;0;70;0
WireConnection;81;1;75;0
WireConnection;0;0;81;0
WireConnection;0;1;60;0
ASEEND*/
//CHKSM=9128CB2C5F4AA0697FADB1FAA25559EAEACD4EAA