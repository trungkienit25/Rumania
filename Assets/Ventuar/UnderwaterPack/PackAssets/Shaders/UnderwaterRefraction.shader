/**
 *  Copyright (c) 2018 Peter Olthof, Peer Play
 *  http://www.peerplay.nl, info AT peerplay.nl 
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *  
 *  Proper credit for noiseSimplex.cginc can be found in its respective file
 *
 *  ------------------------
 *
 *  Creates an underwater refraction image effect for the Low Poly Underwater Pack
 */

Shader "UnderwaterPack/UnderwaterRefraction"
{
    Properties
    {
        [MainTexture]
        _MainTex("Texture", 2D) = "white" {}

        // Controls the size of the distortion noise
        _NoiseScale("Noise Scale", float) = 1

        // Controls the spacing/density of the distortion noise
        _NoiseFrequency("Noise Frequency", float) = 1

        // Controls the speed of the distortion noise
        _NoiseSpeed("Noise Speed", float) = 1

        // Controls by how much each pixel gets distorted by
        _PixelOffset("Pixel Offset", float) = 0.005

        // How far from the camera the effect starts
        _DepthStart("Depth Start", float) = 1

        // How far from the camera the effect ends
        _DepthDistance("Depth Distance", float) = 1
    }

    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "./Cginc/noiseSimplex.cginc"

            float _NoiseScale;
            float _NoiseFrequency;
            float _NoiseSpeed;
            float _PixelOffset;
            float _DepthStart;
            float _DepthDistance;
            sampler2D _CameraDepthTexture;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 scrPos : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.scrPos = ComputeScreenPos(o.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;

            fixed4 frag(v2f i) : COLOR
            {
                // Calculates the depth of each pixel in the scene relative to the defined depth properties
                //float depthValue = Linear01Depth(tex2Dproj(_CameraDepthTexture, UNITY_PROJ_COORD(i.scrPos)).r) * _ProjectionParams.z;
                //depthValue = 1 - saturate((depthValue - _DepthStart) / _DepthDistance);

                // Calculates the distortion value of each pixel
                float3 screenPos = float3(i.scrPos.x, i.scrPos.y, 0) * _NoiseFrequency;
                screenPos.z += _Time.y * _NoiseSpeed;
                float noise = _NoiseScale * ((snoise(screenPos) + 1) / 2);

                // Convert noise into a direction
                float4 noiseToDirection = float4(cos(noise * UNITY_PI * 2), sin(noise * UNITY_PI * 2), 0, 0);
                fixed4 col = tex2Dproj(_MainTex, i.scrPos + normalize(noiseToDirection) * _PixelOffset);
                return col;
            }
            ENDCG
        }
    }
}
