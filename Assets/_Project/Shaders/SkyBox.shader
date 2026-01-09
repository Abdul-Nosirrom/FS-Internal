Shader "FS/Environment/SkyGradient"
{
    Properties
    {
        [Header(Sky Color)]
        _SkyGradient("Sky Gradient", 2D) = "white" {}
        
        [Header(Stars)]
        _StarNoise("Star Noise", 2D) = "white" {}
        [HDR] _StarColor("Star Color", Color) = (1, 1, 1, 1)
        
        [Header(Clouds)]
        _Clouds("Clouds", 2D) = "white" {}
        _CloudsSpeed("Clouds Speed", Color) = (0.1, 0.6, 0, 0) // X: horizontal speed, Y: vertical speed
        
        [Header(Sun)]
        _SunSize("Sun Size", Range(0, 1)) = 0.02
        [HDR] _SunColor("Sun Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Skybox"
        }
        Cull Off ZWrite Off
        
        Pass
        {
            Name "Skybox Gradient"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"


            TEXTURE2D(_SkyGradient);
            SAMPLER(sampler_SkyGradient);

            TEXTURE2D(_StarNoise);
            SAMPLER(sampler_StarNoise);

            TEXTURE2D(_Clouds);
            SAMPLER(sampler_Clouds);

            CBUFFER_START(UnityPerMaterial)

            float4 _SkyGradient_ST;
            float4 _StarNoise_ST;
            float4 _Clouds_ST;

            float2 _CloudsSpeed;
            
            float4 _StarColor;
            float _SunSize;
            float4 _SunColor;
            
            CBUFFER_END
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 uv : TEXCOORD0;
            };

            struct v2f
            {
                float3 viewDir : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.viewDir = v.uv;
                return o;
            }

            float2 ViewToSphericalNorm(float3 V)
            {
                float theta = atan2(V.z, V.x)/(2*PI) + 0.5;
                float phi = (V.y + 1)/2;
                return float2(theta, phi);
            }

            // UVs for applying texture to top hemisphere. Appears as 2 flat planes on the vertical
            float2 XZProjection(float3 viewDir)
            {
                viewDir = normalize(viewDir);
                return float2(viewDir.x, viewDir.z) / viewDir.y;
            }

            float3 SampleClouds(Texture2D cloudsTex, sampler cloudsSampler, float2 cloudsUV, float3 viewDir, float2 tilingSpeed)
            {
                cloudsUV.y += _Time.y * tilingSpeed; // Animate clouds
                float3 cloudsColor = SAMPLE_TEXTURE2D(cloudsTex, cloudsSampler, cloudsUV).rgb;
                cloudsColor = lerp(0, cloudsColor, saturate(viewDir.y + 0.2));
                cloudsColor = smoothstep(0.1, 0.6, cloudsColor) * 0.3;
                return cloudsColor;
            }
            
            float3 frag(v2f i) : SV_Target
            {
                float2 starUV = TRANSFORM_TEX(XZProjection(i.viewDir), _StarNoise);
                float starNoise = SAMPLE_TEXTURE2D(_StarNoise, sampler_StarNoise, starUV).r;
                float3 starVal =  smoothstep(0.4, 1, starNoise) * _StarColor * saturate(i.viewDir.y);
                
                float2 cloudsUV = TRANSFORM_TEX(XZProjection(i.viewDir), _Clouds);
                float3 cloudsColor = SampleClouds(_Clouds, sampler_Clouds, cloudsUV, i.viewDir, _CloudsSpeed.xy);

                float2 uv = ViewToSphericalNorm(i.viewDir);
                uv.y = 1 - uv.y; // Invert Y to match texture coordinates
                float3 color = SAMPLE_TEXTURE2D(_SkyGradient, sampler_SkyGradient, uv.y) + starVal + cloudsColor;

                //color *= InterleavedGradientNoise(uv, 0);

                float3 sunDir = GetMainLight().direction;
                float sunFactor = smoothstep(0, _SunSize, distance(sunDir, i.viewDir));

                // Fade sun factor based on Y value
                float sunPolar = sunDir.y;
                sunPolar = smoothstep(0, 0.25, sunPolar);
                //return sunPolar;
                sunFactor = lerp(1, sunFactor, sunPolar);
                return lerp(_SunColor, color, sunFactor);
            }
            ENDHLSL
        }
    }
}