/**
 *  Colors and procedurally animates plants and corals for the Low Poly Underwater Pack
 */

Shader "UnderwaterPack/PlantCoral"
{
    Properties
    {
        /**
         *  PlantCoralAnimation.cs feeds info into PerRendererData variables.
         */
        
        // Main texturing properties
        [MainTexture] 
        _MainTex("Albedo (RGB)", 2D) = "white" {}
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0

        // Toggle to visualize distortion gradient.
        [PerRendererData] _VisualizeGradient("Visualize Wave Gradient", float) = 0

        // Length of distortion wave.
        [PerRendererData] _WaveLength("Wave Length", float) = 4

        // Speed of distortion wave.
        [PerRendererData] _WaveSpeed("Wave Speed", float) = 0.2

        // Amplitude of distortion wave.
        [PerRendererData] _WaveHeight("Wave Height", float) = 0.045

        // The blending/fade between where the distortion wave starts and stops.
        [PerRendererData] _GradBlending("Gradient Blending", float) = 1

        // Offset/displacement value of the distortion gradient.
        [PerRendererData] _GradOffset("Gradient Offset", float) = 0

        // Minimum clamp value for distortion gradient.
        [PerRendererData] _ClampMin("Minimum Clamp",float) = -.1

        // Maximum clamp value for distortion gradient.
        [PerRendererData] _ClampMax("Maximum Clamp",float) = 3

        // Wave gradient visualization colors.
        [HideInInspector] _Color1("Col 1", Color) = (0,0,0,1)
        [HideInInspector] _Color2("Col 2", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        Cull Off

        CGPROGRAM
        
        #pragma multi_compile _ LOD_FADE_CROSSFADE
        
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows vertex:vert 
        //addshadow

        // Use shader model 3.5 target, to get nicer looking lighting
        #pragma target 3.5

        sampler2D _MainTex;

        struct vertexInput 
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float4 tangent : TANGENT;

        };

        struct Input
        {
            float4 screenPos;
            float2 uv_MainTex;
            float3 objPos;
        };

        half _Glossiness;
        half _Metallic;
        float _VisualizeGradient;
        float _WaveLength;
        float _WaveSpeed;
        float _WaveHeight;
        float _ClampMin;
        float _ClampMax;
        int _AxisNum;
        float _GradBlending;
        float _GradOffset;

        fixed4 _Color1;
        fixed4 _Color2;

        void vert(inout appdata_full v, out Input o) 
        {
            const float PI = 3.14159;

            // Calculate main distortion based on the distortion gradient
            half gradient = clamp(lerp(0, 1, (v.vertex.y + _GradOffset) * _GradBlending), _ClampMin, _ClampMax);
            float vertX = ((v.vertex.x / _WaveLength) + (_Time.y * _WaveSpeed));
            float vertZ = ((v.vertex.z / _WaveLength) + (_Time.y * _WaveSpeed));
            float distortion1 = _WaveHeight * gradient * (sin(_Time.y + vertZ * 2 * PI) + cos(_Time.y + vertX * 2 * PI));
            float distortion2 = _WaveHeight * gradient * (cos(_Time.y + vertZ * 2 * PI) + sin(_Time.y + vertX * 2 * PI));
            v.vertex.x += distortion1;
            v.vertex.z += distortion2;

            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.objPos = v.vertex;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
#ifdef LOD_FADE_CROSSFADE
            float2 vpos = IN.screenPos.xy / IN.screenPos.w * _ScreenParams.xy;
            UnityApplyDitherCrossFade(vpos);
#endif
            
            fixed4 c = _Color1;
            if (_VisualizeGradient == 1) 
            {
                // Primary distortion gradient visualization
                c = clamp(lerp(_Color1, _Color2, (IN.objPos.y + _GradOffset) * _GradBlending), _ClampMin, _ClampMax);
            }
            else 
            {
                // Regular texturing
                c = tex2D(_MainTex, IN.uv_MainTex);
            }

            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
