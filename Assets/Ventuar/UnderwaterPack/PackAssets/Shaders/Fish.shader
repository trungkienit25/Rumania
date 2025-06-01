/**
 *  Colors and procedurally animates fish for the Low Poly Underwater Pack
 */

Shader "UnderwaterPack/Fish"
{
    Properties
    {
        /**
         *  FishAnimation.cs feeds info into PerRendererData variables.
         */
        
        // Main texturing properties
        [MainTexture]
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0

        // Toggle to visualize main distortion gradient.
        [HideInInspector][PerRendererData]_VisualizeGradient1("Visualize Main Wave Gradient", float) = 0

        // Toggle to visualize secondary distortion gradient.
        [HideInInspector][PerRendererData]_VisualizeGradient2("Visualize Secondary Wave Gradient", float) = 0
        
        // Length of main distortion wave.
        [HideInInspector][PerRendererData]_WaveLength1("Wave Length 1", float) = 1

        // Speed of main distortion wave.
        [HideInInspector][PerRendererData]_WaveSpeed1("Wave Speed 1", float) = 0.5

        // Amplitude of main distortion wave.
        [HideInInspector][PerRendererData]_WaveHeight1("Wave Height 1", float) = 0.2

        // The blending/fade between where the main distortion wave starts and stops.
        [HideInInspector][PerRendererData]_GradBlending1("Gradient Blending 1", float) = 1

        // Offset/displacement value of the main distortion gradient.
        [HideInInspector][PerRendererData]_GradOffset1("Gradient Offset 1", float) = 0

        // Variables below function identically to the ones above except apply to the secondary distortion wave.
        [HideInInspector][PerRendererData]_WaveLength2("Wave Length 2", float) = 1
        [HideInInspector][PerRendererData]_WaveSpeed2("Wave Speed 2", float) = 0.5
        [HideInInspector][PerRendererData]_WaveHeight2("Wave Height 2", float) = 0.2
        [HideInInspector][PerRendererData]_GradBlending2("Gradient Blending 2", float) = 1
        [HideInInspector][PerRendererData]_GradOffset2("Gradient Offset 2", float) = 0

        // Minimum clamp value for distortion gradient.
        [HideInInspector][PerRendererData]_ClampMin("Minimum Clamp", float) = -.1

        // Maximum clamp value for distortion gradient.
        [HideInInspector][PerRendererData]_ClampMax("Maximum Clamp", float) = 3

        // Identifier for main distortion axis. 0 is none, 1 is x, 2 is y, 3 is z.
        [HideInInspector][PerRendererData]_AxisNum1("Main Distortion Axis", float) = 3

        // Identifier for secondary distortion axis. 0 is none, 1 is x, 2 is y, 3 is z.
        [HideInInspector][PerRendererData]_AxisNum2("Secondary Distortion Axis", float) = 3

        // Wave gradient visualization colors.
        [HideInInspector]_Color1("Col 1", Color) = (0,0,0,1)
        [HideInInspector]_Color2("Col 2", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Cull Off

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;

        struct vertexInput 
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float4 tangent : TANGENT;

        };

        struct Input
        {
            float2 uv_MainTex;
            float3 objPos;
        };

        half _Glossiness;
        half _Metallic;

        float _VisualizeGradient1;
        float _VisualizeGradient2;

        float _WaveLength1;
        float _WaveSpeed1;
        float _WaveHeight1;
        float _GradBlending1;
        float _GradOffset1;

        float _WaveLength2;
        float _WaveSpeed2;
        float _WaveHeight2;
        float _GradBlending2;
        float _GradOffset2;

        float _ClampMin;
        float _ClampMax;

        int _AxisNum1;
        int _AxisNum2;

        fixed4 _Color1;
        fixed4 _Color2;

        void vert(inout appdata_full v, out Input o) 
        {
            const float PI = 3.14159;

            // Calculate main distortion based on the distortion gradient
            half gradient = clamp(lerp(0, 1, (v.vertex.x + _GradOffset1) * _GradBlending1), _ClampMin, _ClampMax);
            float vertX = ((v.vertex.x / _WaveLength1) + (_Time.y * _WaveSpeed1));
            float vertZ = ((v.vertex.z / _WaveLength1) + (_Time.y * _WaveSpeed1));
            float distortion = _WaveHeight1 * gradient * (sin(_Time.y + vertZ * 2 * PI) + cos(_Time.y + vertX * 2 * PI));

            // Set main distortion on defined axis
            if (_AxisNum1 == 1) 
            {
                v.vertex.x += distortion;
            } 
            else if (_AxisNum1 == 2) 
            {
                v.vertex.y += distortion;
            } 
            else if (_AxisNum1 == 3)
            {
                v.vertex.z += distortion;
            }
            
            // Calculate secondary distortion based on the inverted distortion gradient, to contrast the main distortion wave
            gradient = clamp(lerp(0, 1, (-v.vertex.x + _GradOffset2) * _GradBlending2), _ClampMin, _ClampMax);
            vertX = ((-v.vertex.x / _WaveLength2) + (_Time.y * _WaveSpeed2));
            vertZ = ((v.vertex.z / _WaveLength2) + (_Time.y * _WaveSpeed2));
            distortion = _WaveHeight2 * gradient * (sin(_Time.y + vertZ * 2 * PI) + cos(_Time.y + vertX * 2 * PI));

            // Set secondary distortion on defined axis
            if (_AxisNum2 == 1 && _AxisNum1 != 1) 
            {
                v.vertex.x += distortion;
            }
            else if (_AxisNum2 == 2 && _AxisNum1 != 2) 
            {
                v.vertex.y += distortion;
            }
            else if (_AxisNum2 == 3 && _AxisNum1 != 3) 
            {
                v.vertex.z += distortion;
            }

            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.objPos = v.vertex;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = _Color1;
            if (_VisualizeGradient1 == 1) 
            {
                // Primary distortion gradient visualization
                c = clamp(lerp(_Color1, _Color2, (IN.objPos.x + _GradOffset1) * _GradBlending1), _ClampMin, _ClampMax);
            } 
            else if (_VisualizeGradient2 == 1) 
            {
                // Secondary distortion gradient visualization
                c = clamp(lerp(_Color1, _Color2, (-IN.objPos.x + _GradOffset2) * _GradBlending2), _ClampMin, _ClampMax);
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
