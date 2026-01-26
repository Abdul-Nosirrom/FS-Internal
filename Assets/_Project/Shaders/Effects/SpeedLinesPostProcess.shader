Shader "FreeSkies/Effects/SpeedLines"
{
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"
        }
        ZWrite Off Cull Off
        Pass
        {
            Name "SpeedLinesPass"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "../Library/Transforms.hlsl"
            #include "../Library/Noise/Noise2D.hlsl"
            #include "../Library/UVHelpers.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

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
            
            // Out frag function takes as input a struct that contains the screen space coordinate we are going to use to sample our texture. It also writes to SV_Target0, this has to match the index set in the UseTextureFragment(sourceTexture, 0, …) we defined in our render pass script.   
            float3 Frag(Varyings input) : SV_Target0
            {
                float3 col = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, input.texcoord.xy, _BlitMipLevel);
                return col;
                float time = _Time.y * 2;
                // sample the texture using the SAMPLE_TEXTURE2D_X_LOD
                float2 uv = input.texcoord.xy - 0.5;
                uv *= 2;
                //float mr = min(_BlitTextureSize.x, _BlitTextureSize.y);
                //uv = (uv * 2 - _BlitTextureSize.xy) / mr * 0.5;
                //uv = uv - 0.5;
                //uv = AspectCorrectUV(uv);
                float2 p = 0.5f + normalize(uv) + min(length(uv), 0.05);
                float3 p3 = 13 * float3(p.xy, 0) + float3(0, 0, time * 0.025f);
                float noise = simplex3d(p3 * 32) * 0.5f + 0.5f;
                float dist = abs(clamp(length(uv)/12, 0, 1) * noise * 2 - 1);
                const float e = 0.3;
                float stepped = smoothstep(e - 0.5, e + 0.5, noise * (1 - pow(dist, 4)));
                float final = smoothstep(e - 0.05, e + 0.05, noise * stepped);
                return col + final;
                float radialMask = smoothstep(0.35f, 0.75f, length(uv));
                
                // Radial Lines
                float lineLengthNoise = fbm_perlin_2d_01(input.texcoord, 16) * 0.25f;
                float uvAngle = atan(uv.x / uv.y);
                float linesRadialWidth = (1 - radialMask) * 0.125f + lineLengthNoise;
                float lines = smoothstep(0.75f + linesRadialWidth, 0.8f + linesRadialWidth, frac(uvAngle * 12));
                
                float speedLines = radialMask * lines;
                
                return col + speedLines * 0.1f;
            }
            ENDHLSL
        }
    }
}