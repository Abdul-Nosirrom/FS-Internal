Shader "FreeSkies/Effects/SpeedLines"
{
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "SpeedLines"
            Blend SrcAlpha  OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            float _PlayerSpeed;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            // From David Hoskins (MIT licensed): https://www.shadertoy.com/view/4djSRW
            float3 hash33(float3 p3) {
	            p3 = frac(p3 * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yxz + 33.33);
                return frac((p3.xxy + p3.yxx) * p3.zyx) - 0.5;
            }

            // From Nikita Miropolskiy (MIT licensed): https://www.shadertoy.com/view/XsX3zB
            float simplex3d(float3 p) {
	             float3 s = floor(p + dot(p, 1.0 / 3.0));
	             float3 x = p - s + dot(s, 1.0 / 6.0);
	             float3 e = step(0, x - x.yzx);
	             float3 i1 = e * (1.0 - e.zxy);
	             float3 i2 = 1.0 - e.zxy * (1.0 - e);
	             float3 x1 = x - i1 + 1.0 / 6.0;
	             float3 x2 = x - i2 + 1.0 / 3.0;
	             float3 x3 = x - 0.5;
	             float4 w = max(0.6 - float4(dot(x, x), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
	             w *= w;
	             return dot(float4(dot(hash33(s), x), 
                                 dot(hash33(s + i1), x1), 
                                 dot(hash33(s + i2), x2),  
                                 dot(hash33(s + 1.0), x3)) * w * w, 52);
            }
            
            #include "../Library/Noise/Noise2D.hlsl"

            float4 frag(v2f i) : SV_Target
            {
                float2 screenUV = i.screenPos.xy / 8;
                screenUV = screenUV + _Time.y * 0.5f; // Slightly animate noise over time
                
                float2 lineUVs = i.uv;
                // Stretch it along 1 axist
                lineUVs.y = frac(lineUVs.y + 5*_Time.y);
                
                // Quadratic gradient mask
                float mask = (i.uv.y - 0.5) * 2;
                mask *= mask;
                mask = 1-mask;
                
                // Speed opacity
                float speedMask = smoothstep(16, 22, _PlayerSpeed);
                
                // Width modulo
                float widthMod = 0 * smoothstep(0.7, 1, i.uv.y);
                
                // Sample noise
                float rawNoise = fbm_perlin_2d_01(lineUVs, float2(64, 1));
                float steppedNoise = step(0.65 + widthMod, rawNoise);
                float4 result = steppedNoise * float4(1,1,1,1);
                result.a *= mask * 0.125f * speedMask;
                return result;
                return float4(i.uv, 0, 1);

               // i.uv.x += length(noiseSample) * 0.005f;
                float p = 0.5 + i.uv.x;// * min(length(i.uv), 0.05);
                float3 p3 = 13.0 * float3(p, 0, _Time.y * 0.015);// + float3(0, 0, _Time.y * 0.025);
                float noise = simplex3d(p3 * 16.0) * 0.5 + 0.5;
                float y = 1 - abs(i.uv.y - 0.5) * 2.0;
                float dist = abs(clamp(y / 12.0, 0.0, 1.0) * noise * 2.0 - 1.0);
                const float e = 0.3;
                float stepped = smoothstep(e - 0.5, e + 0.5, noise * (1.0 - pow(dist, 4.0)));
                float final = smoothstep(e - 0.05, e + 0.05, noise * stepped);
                float intensity = smoothstep(16, 22, _PlayerSpeed) * 0.6f;
                return final * float4(1, 1, 1, intensity * 0.25f);

                float2 noiseSample = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, screenUV);
               //noiseSample = lerp(noiseSample, 1, i.uv.y);

                i.uv.x += noiseSample * 0.02f;
                
                //float intensity = smoothstep(6, 12, _PlayerSpeed) * 0.6f;
                
                // Create animated speed lines
                float time = _Time.y * 2.0;
                float offset = frac(i.uv.y + time);
                
                // Sharp vertical lines that repeat around cylinder
                float lines = frac(i.uv.x * 20);
                lines = step(0.92, lines);
                
                // Fade lines based on vertical position for motion blur effect
                float fade = 1;// smoothstep(0.0, 0.3, offset) * smoothstep(1.0, 0.7, offset);
                
                // Fade out towards top and bottom edges (uv.y = 0 and 1)
                float edgeFade = smoothstep(0.0, 0.15, i.uv.y) * smoothstep(1.0, 0.85, i.uv.y);
                
                // Combine
                float speedLines = lines * fade * edgeFade;
                float3 col = speedLines;
                
                return float4(col, intensity * 0.25f * speedLines);
            }
            ENDHLSL
        }
    }
}