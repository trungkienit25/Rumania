/**
 *  Colors, shades, and animates the water in the Low Poly Underwater Pack. Coloring partially derived from https://roystan.net/articles/toon-water.html
 */

Shader "UnderwaterPack/Water"
{
	Properties
	{
		/*
			Water.cs feeds info into PerRendererData variables
		*/

		// What color the water will sample when the surface below is at its deepest.
		[HideInInspector][PerRendererData]_Color1("Depth Color", Color) = (0.086, 0.407, 1, 0.749)

		// What color the water will sample when the surface below is shallow.
		[HideInInspector][PerRendererData]_Color2("Shallow Color", Color) = (0.325, 0.807, 0.971, 0.725)

		// Color to draw the water foam.
		[HideInInspector][PerRendererData]_Color3("Foam Color", Color) = (1,1,1,1)

		// Maximum distance which the surface below the water will affect the depth and shallow color blending.
		[HideInInspector][PerRendererData]_DepthDistance("Depth Distance", float) = 1

		// Value to control how smooth the blend between the depth and shallow colors are.
		[HideInInspector][PerRendererData]_DepthBlend("Depth Blending", Range(0,10)) = 1

		// How "shiny" the mesh is. Setting this value to 0 will not get rid of specularity entirely - set _SpecIntensity to 0 as well to achieve this.
		[HideInInspector][PerRendererData]_Specularity("Specularity", Range(0,50)) = 20

		// Strength / brightness of the specular effect.
		[HideInInspector][PerRendererData]_SpecIntensity("Specular Intensity", Range(0,1)) = .6

		// Noise texture used to generate foam.
		[HideInInspector][PerRendererData]_FoamNoise("Foam Noise", 2D) = "black" {}

		// Speed in UVs per second which the noise will scroll. Only the xy components are used.
		[HideInInspector][PerRendererData]_FoamScrollSpeed("Foam Scroll Speed", float) = 0.0

		// Values in the noise texture above this cutoff are rendered on the surface.
		[HideInInspector][PerRendererData]_FoamNoiseCutoff("Foam Noise Cutoff", Range(0, 1)) = 0.777

		// Controls the distance that surfaces below the water will contribute to foam being rendered. Only the xy components are used.
		[HideInInspector][PerRendererData]_FoamMinMaxDist("Foam Min Max Distance", Vector) = (.04, .4, 0, 0)

		// Red and green channels of this texture are used to offset the foam noise texture to create distortion in the foam.
		[HideInInspector][PerRendererData]_FoamDistortion("Foam Distortion Texture", 2D) = "white" {}

		// Amplifies the foam distortion effect given by the foam distortion texture.
		[HideInInspector][PerRendererData]_FoamDistortionAmount("Foam Distortion Amount", Range(0, 1)) = 0.27

		// Controls the direction of the waves. Only the xy components are used.
		_WaveDir("Wave Direction", Vector) = (1,1,0,0)

		// Controls the height of the primary waves.
		[HideInInspector][PerRendererData]_Amplitude1("Wave Amplitude 1", float) = .25

		// Controls how frequently a primary wave appears. Lower values spread waves further out and vice versa.
		[HideInInspector][PerRendererData]_Frequency1("Wave Frequency 1", float) = 1.08

		// "Controls how fast primary waves move.
		[HideInInspector][PerRendererData]_Speed1("Wave Speed 1", float) = .25

		// Mirrored properties for secondary overlayed waves
		[HideInInspector][PerRendererData]_Amplitude2("Wave Amplitude 2", float) = .25
		[HideInInspector][PerRendererData]_Frequency2("Wave Frequency 2", float) = 1.08
		[HideInInspector][PerRendererData]_Speed2("Wave Speed 2", float) = .25

		// Controls amplification of the displacement noise in the vertex displacement calculations.
		[HideInInspector][PerRendererData]_NoiseAmplitude("Noise Amplitude", float) = 1

		// How fast the displacement noise scrolls along the mesh.
		[HideInInspector][PerRendererData]_NoiseSpeed("Noise Speed", float) = 1

		// The scale/density of the displacement noise.
		[HideInInspector][PerRendererData]_NoiseScale("Noise Scale", float) = 1

		// Size of the water grid mesh, used to scale wavelength and noise mapping
		[HideInInspector][PerRendererData]_WaterSize("Water Grid Size", float) = 1

		// Toggle to disable waves and foam entirely. This and the other 3 toggles below are intended to be used for visualization purposes
		[HideInInspector][PerRendererData]_VisualizeWaves("Visualize Waves", float) = 1

		// Toggle to disable the base waves
		[HideInInspector][PerRendererData]_VisualizeBaseWaves("Visualize Base Waves", float) = 1

		// Toggle to disable the noise displacement
		[HideInInspector][PerRendererData]_VisualizeWaveNoise("Visualize Wave Noise", float) = 1

		// Toggle to disable foam rendering
		[HideInInspector][PerRendererData]_VisualizeWaveFoam("Visualize Wave Foam", float) = 1
	}

	CGINCLUDE
	#include "UnityCG.cginc"
	#include "./Cginc/noiseSimplex.cginc"

	float _VisualizeWaves;
	float _VisualizeBaseWaves;
	float _VisualizeWaveNoise;

	float _NoiseAmplitude;
	float _NoiseSpeed;
	float _NoiseScale;
	float _Amplitude1;
	float _Amplitude2;
	float _Frequency1;
	float _Frequency2;
	float _Speed1;
	float _Speed2;
	float _WaterSize;
	float4 _WaveDir;

	// Wave calculations, including base wave and noise displacement, are stored here 
	float4 calculateWave(float4 v) 
	{		
		// Trig function input coefficient to account for proper wave frequency.
		float k1 = 2 * (UNITY_PI / (1 / _Frequency1));
		float k2 = 2 * UNITY_PI / (1 / _Frequency2);
		float2 dir = normalize(_WaveDir);

		float f1 = 0;
		float f2 = 0;
		if (_VisualizeBaseWaves == 1) 
		{
			// Equation for trig function input that accounts for the size of the water mesh and wave direction
			f1 = k1 * (((v.x * dir.x + v.z * dir.y) / (_WaterSize * 2)) - ((_Speed1 / 10) * _Time.y));
			f2 = k2 * (((v.x * -dir.x + v.z * dir.y) / (_WaterSize * 2)) - ((_Speed2 / 10) * _Time.y));
		}

		float noise = 0;
		if (_VisualizeWaveNoise == 1) 
		{
			// Sample the _WaveNoise texture along the xz axis with respect to the size of the water mesh to add random noise distortion
			// The simplex noise code was taken from https://gist.github.com/fadookie/25adf86ae7e2753d717c. Proper license info included in noiseSimplex.cginc.
			noise = snoise
			(
				float4
				(
					(v.x / ((_WaterSize * _NoiseScale) / 50)) + (_Time.y * _NoiseSpeed / 5),
					(v.z / ((_WaterSize * _NoiseScale) / 50)) + (_Time.y * _NoiseSpeed / 5), 0, 0
				)
			);
		}

		float a1 = _Amplitude1 * _WaterSize * _Frequency1 / (k1 * 100);
		float a2 = _Amplitude2 * _WaterSize * _Frequency2 / (k2 * 100);
		if (_VisualizeWaves == 1) 
		{
			// Sine wave based y axis movement, accounting for both the base wave and noise. 2 of them overlayed for more wave randomness
			v.y = (a1 * sin(f1)) + (a2 * sin(f2)) + (noise * _NoiseAmplitude);
			// Transformation along xz axis to convert the wave into a primitive Gerstner wave
			if (_Amplitude1 > 0)
			{
				v.x += a1 * cos(f1) / (_Amplitude1 * 5) * dir.x;	
				v.z += a1 * cos(f1) / (_Amplitude1 * 5) * dir.y;
			}
			
		}

		return v;
	}
	ENDCG

	SubShader
	{
		Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "DisableBatching" = "true" }

		// Vertex displacement and coloring
		Pass
		{
			Tags { "LightMode" = "ForwardBase" }
			
			// Transparent "normal" blending.
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite Off
			Cull Off

			CGPROGRAM
			#define SMOOTHSTEP_AA 0.02

			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog

			#include "./Cginc/Shadows.cginc"
			#include "UnityCG.cginc"
			#include "UnityLightingCommon.cginc"
			#include "./Cginc/noiseSimplex.cginc"

			// Blends two colors using the same algorithm that our shader is using to blend with the screen. 
			// This is usually called "normal blending", and is similar to how software like Photoshop blends two layers.
			float4 alphaBlend(float4 top, float4 bottom)
			{
				float3 color = (top.rgb * top.a) + (bottom.rgb * (1 - top.a));
				float alpha = top.a + bottom.a * (1 - top.a);

				return float4(color, alpha);
			}

			struct appdata
			{
				float4 vertex : POSITION;
				float4 uv : TEXCOORD0;
				float3 normal : NORMAL;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 noiseUV : TEXCOORD0;
				float2 distortUV : TEXCOORD1;
				float4 screenPosition : TEXCOORD2;
				float4 worldPosition : TEXCOORD3;
				float3 viewNormal : NORMAL;
				
				UNITY_FOG_COORDS(4)
			};

			sampler2D _FoamNoise;
			float4 _FoamNoise_ST;

			sampler2D _FoamDistortion;
			float4 _FoamDistortion_ST;

			float _FoamScrollSpeed;


			v2f vert(appdata v)
			{
				v2f o;

				v.vertex = calculateWave(v.vertex);

				o.vertex = UnityObjectToClipPos(v.vertex);
				o.screenPosition = ComputeScreenPos(o.vertex);
				o.distortUV = TRANSFORM_TEX(v.uv, _FoamDistortion);
				o.noiseUV = TRANSFORM_TEX(v.uv, _FoamNoise);
				o.viewNormal = COMPUTE_VIEW_NORMAL;
				o.worldPosition = mul(unity_ObjectToWorld, v.vertex);

				UNITY_TRANSFER_FOG(o, o.vertex);

				return o;
			}

			float4 _Color2;
			float4 _Color1;
			float4 _Color3;

			float _DepthDistance;
			float _DepthBlend;
			float _FoamMaxDistance;
			float _FoamMinDistance;
			float4 _FoamMinMaxDist;

			float _FoamNoiseCutoff;
			float _FoamDistortionAmount;

			sampler2D _CameraDepthTexture;
			sampler2D _CameraNormalsTexture;

			float _Specularity;
			float _SpecIntensity;

			float _VisualizeWaveFoam;

			float4 frag(v2f i) : SV_Target
			{
				/** 
				 *	DEPTH
				 */

				// Retrieve the current depth value of the surface behind the pixel we are currently rendering.
				float existingDepth01 = tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPosition)).r;
				// Convert the depth from non-linear 0...1 range to linear depth, in Unity units.
				float existingDepthLinear = LinearEyeDepth(existingDepth01);

				// Difference, in Unity units, between the water's surface and the object behind it.
				float depthDifference = existingDepthLinear - i.screenPosition.w;

				// Calculate the color of the water based on the depth using our two gradient colors.
				float waterDepthDifference01 = saturate(depthDifference / _DepthDistance);
				float4 waterColor = lerp(_Color2, _Color1, pow(waterDepthDifference01, _DepthBlend));

				// Retrieve the view-space normal of the surface behind the pixel we are currently rendering.
				float3 existingNormal = tex2Dproj(_CameraNormalsTexture, UNITY_PROJ_COORD(i.screenPosition));

				/**
				 *	FOAM
				 */

				// Modulate the amount of foam we display based on the difference between the normals of our water surface and the object behind it.
				// Larger differences allow for extra foam to attempt to keep the overall amount consistent.
				float3 normalDot = saturate(dot(existingNormal, i.viewNormal));
				float foamDistance = lerp(_FoamMinMaxDist.y, _FoamMinMaxDist.x, normalDot);
				float foamDepthDifference01 = saturate(depthDifference / foamDistance);

				float surfaceNoiseCutoff = foamDepthDifference01 * _FoamNoiseCutoff;

				float2 distortSample = (tex2D(_FoamDistortion, i.distortUV).xy/* * 2 - 1*/) * _FoamDistortionAmount;

				// Distort the noise UV based off the RG channels (using xy here) of the distortion texture.
				// Also offset it by time, scaled by the scroll speed.
				float2 dir = normalize(_WaveDir);
				float2 noiseUV = float2(((i.noiseUV.x * -dir.x) + _Time.y * _FoamScrollSpeed) + distortSample.x,
				((i.noiseUV.y * -dir.y) + _Time.y * _FoamScrollSpeed) + distortSample.y);
				float surfaceNoiseSample = tex2D(_FoamNoise, noiseUV).r;

				// Use smoothstep to ensure we get some anti-aliasing in the transition from foam to surface.
				float surfaceNoise = smoothstep(surfaceNoiseCutoff - SMOOTHSTEP_AA, surfaceNoiseCutoff + SMOOTHSTEP_AA, surfaceNoiseSample);

				float4 surfaceNoiseColor = _Color3;
				surfaceNoiseColor.a *= surfaceNoise;

				/**
				 *	COLORING / SHADING
				 */

				// Compute the normal from the interpolated world position.
				half3 normal = normalize(cross(ddy(i.worldPosition), ddx(i.worldPosition)));

				// Helps define low poly edges in final lighting calculation
				half nl = saturate(dot(normal, _WorldSpaceLightPos0.xyz));

				/**
				 *	Unity uses the camera depth texture to cast main directional light shadows, which means transparent shaders, such as this one that do not write to the depth texture, 
				 *	cannot recieve directional light shadows the regular way. To work around this, we sample shadow map references using "Shadows.cginc," which isn't perfect, but is the closest 
				 *	we can get to rendering directional surface shadows. The code was taken from https://github.com/Gaxil/Unity-InteriorMapping. Proper license info included in Shadows.cginc.
				 * 
				 *	Adding the wave amplitude to the y value of the world position helps prevent incorrect shadow artifacts appearing in wave valleys, and 
				 *	doesn't appear to affect regular shadow casting in too significant of a way
				 */
				float k = 2 * UNITY_PI / (1 / _Frequency1);
				float shadowAttenuation = GetSunShadowsAttenuation_PCF5x5(i.worldPosition + float4(0, _Amplitude1 / k, 0, 0), i.worldPosition.w, .1);

				float4 col = waterColor;
				if (_VisualizeWaveFoam == 1) 
				{
					// Use normal alpha blending to combine the foam with the surface.
					col = alphaBlend(surfaceNoiseColor, waterColor);
				}

				// Calculate final lighting with light, shadow, and ambient information
				float3 lighting = shadowAttenuation * _LightColor0.rgb * nl + ShadeSH9(half4(normal, 1));
				col.rgb *= lighting;

				/**
				 *	SPECULARITY
				 */
				
				// Find the view direction of the camera
				float3 viewDirection = normalize(_WorldSpaceCameraPos - i.worldPosition.xyz);

				// Compute specular information - "101 - (_Specularity*2) taken from _Specularity property range and done solely for ease of use in editor
				float specularLight = pow(max(0, dot(reflect(-_WorldSpaceLightPos0.xyz, normal), viewDirection)), 101 - (_Specularity * 2));
				float3 specularReflection = _LightColor0.rgb * _SpecIntensity * specularLight;
				//specularReflection *= lighting;

				// Combine specular and color information to get final output
				float4 c = float4(col + specularReflection, col.a);

				UNITY_APPLY_FOG(i.fogCoord, c);

				return c;
			}
			ENDCG
		}

		// Primary shadow caster
		Pass
		{
			Tags { "LightMode" = "ShadowCaster" }

			CGPROGRAM
			#pragma vertex ProcessVertex
			#pragma fragment ProcessFragment
			#pragma multi_compile_shadowcaster
			#include "UnityCG.cginc"

			struct v2f
			{
				V2F_SHADOW_CASTER;
			};

			v2f ProcessVertex(appdata_base v)
			{
				v2f o;

				// Compute the same vertex information as main shader to accurately project underwater shadows
				v.vertex = calculateWave(v.vertex);

				o.pos = UnityObjectToClipPos(v.vertex);
				TRANSFER_SHADOW_CASTER(o);
				return o;
			}

			float4 ProcessFragment(v2f i) : SV_Target
			{
				SHADOW_CASTER_FRAGMENT(i);
			}
			ENDCG
		}

		// Additional light sources. These lights are unable to cast shadows
		Pass{
			Blend One One
			Tags {  "Queue" = "Transparent" "LightMode" = "ForwardAdd" }
			CGPROGRAM

			#pragma vertex vert 
			#pragma fragment frag 
			#pragma multi_compile_fwdadd_fullshadows 
			#pragma multi_compile_instancing

			#include "UnityCG.cginc" 
			#include "AutoLight.cginc"

			sampler2D _MainTex;
			float4 _MainTex_ST;

			uniform float4 _LightColor0;

			struct v2f 
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;

				LIGHTING_COORDS(1, 2)
			};

			v2f vert(appdata_base v) 
			{
				v2f o;

				// Compute the same vertex information as main shader to accurately project additional lights onto displaced vertices
				v.vertex = calculateWave(v.vertex);

				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);

				TRANSFER_VERTEX_TO_FRAGMENT(o);
				return o;
			}

			fixed4 frag(v2f i) : COLOR
			{
				float attenuation = LIGHT_ATTENUATION(i);

				return attenuation * _LightColor0;
			}
			ENDCG
		}
	}
}