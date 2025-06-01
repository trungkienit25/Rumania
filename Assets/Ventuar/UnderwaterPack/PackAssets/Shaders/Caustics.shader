// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

/**
 *  Draws world-space caustics on objects for the Low Poly Underwater Pack
 */

Shader "UnderwaterPack/Caustics"
{
    Properties
    {
        // Main texturing properties
        [MainTexture]
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        
        [Header(Caustics)]

        // The world-space y level at which the caustics texturing starts
        _CausticsStartLevel ("Caustics Start Level", float) = 0.0

        // Amoung of caustics blending/fade between from the start level
        _CausticsBlending ("Caustics Blending", float) = 1.0

        // The texture for caustics
        _CausticsTex ("Caustics Texture", 2D) = "white" {}

        // The color of the caustics
        _CausticsColor("Caustics Color", Color) = (1,1,1,1)

        // How strong/bright the caustics are
        _CausticsIntensity("Caustics Intensity", float) = 1

        // Scale of the caustics texture for both the primary and secondary overlay respectively
        _CausticsScale ("Caustics Scale 1 and 2", Vector) = (80,90,0,0)

        // Scroll speed of the caustics texture for both the primary and secondary overlay respectively
        _CausticsSpeed ("Caustics Speed 1 and 2", Vector) = (.2,.5,0,0)

        // Scroll direction of the caustics texture for both the primary and secondary overlay respectively
        _CausticsDir ("Caustics Direction 1 and 2", Vector) = (1,1,-1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest" }
        LOD 200

        CGPROGRAM
        
        #pragma multi_compile _ LOD_FADE_CROSSFADE
        
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows 

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float4 screenPos;
            float3 worldNormal;
        };

        half _Glossiness;
        half _Metallic;
        float _CausticsStartLevel;
        float _CausticsBlending;
        float4 _CausticsColor;
        float _CausticsIntensity;
        sampler2D _CausticsTex;
        float4 _CausticsTex_ST;
        float2 _CausticsScale;
        float2 _CausticsSpeed;
        float4 _CausticsDir;

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
        // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        // Samples and calculates caustics with variable scale, speed, and direction
        fixed3 causticsSample(Input i, float scale, float speed, float2 d)
        {
            // Ensures caustics are overlayed correctly on all object normals
            float2 uv;
            if (abs(i.worldNormal.y) > 0.5)
            {
                uv = i.worldPos.xz;
            }
            else if (abs(i.worldNormal.x) > 0.5)
            {
                uv = i.worldPos.yz;
            }
            else
            {
                uv = i.worldPos.xy;
            }

            float fadeFactor = min(1.0f, (_CausticsStartLevel - i.worldPos.y) / _CausticsBlending);

            uv *= scale / 1000;

            float2 dir = normalize(d);
            float2 causticsUV = float2((uv.x * -dir.x) + (speed / 10) * _Time.y, (uv.y * -dir.y) + (speed / 10) * _Time.y);
            fixed3 caustics = tex2D(_CausticsTex, causticsUV * _CausticsTex_ST.xy + _CausticsTex_ST.zw);
            return caustics;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {   
#ifdef LOD_FADE_CROSSFADE
            float2 vpos = IN.screenPos.xy / IN.screenPos.w * _ScreenParams.xy;
            UnityApplyDitherCrossFade(vpos);
#endif

            // Albedo comes from a texture atlas
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex);
            o.Albedo = c.rgb;

            // Caustics
            if (IN.worldPos.y < _CausticsStartLevel)
            {
                float fadeFactor = min(1.0f, (_CausticsStartLevel - IN.worldPos.y) / _CausticsBlending);

                // Sample caustics for primary and secondary overlays
                fixed3 c1 = causticsSample(IN, _CausticsScale.x, _CausticsSpeed.x, _CausticsDir.xy);
                fixed3 c2 = causticsSample(IN, _CausticsScale.y, _CausticsSpeed.y, _CausticsDir.zw);
                
                // Only project caustics if being hit by light
                half3 normal = normalize(cross(ddy(IN.worldPos), ddx(IN.worldPos)));
                float causticProj = max(0, dot(reflect(-_WorldSpaceLightPos0, normal), normal));

                // Custom logic for if the camera is in deferred. _WorldSpaceLightPos0 is bugged/inconsistent in the built-in deferred rendering path, so we just make the direction fixed.
                /*
                 *  CHANGE THE CAUSTICS PROJECTION ANGLE HERE
                 */
#if UNITY_PASS_DEFERRED
                float3 projAngle = float3(0,-.5f,.2f);
                causticProj = max(0, dot(reflect(projAngle, normal), normal));
#endif
                
                causticProj = clamp(causticProj, 0, 1);

                o.Albedo.rgb += min(c1, c2) * fadeFactor * _CausticsColor * causticProj * _CausticsIntensity;
            }

            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
