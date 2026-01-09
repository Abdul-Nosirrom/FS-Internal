Shader "Hidden/Noise"
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
            Name "NoisePass"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Library/Noise/Noise2D.hlsl"
            #include "Library/Noise/Noise3D.hlsl"
            #include "Library/SDFs/SDF3D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Library/Transforms.hlsl"
            #include "Library/Random.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            // Scale - divide position, multiply result
            // Usage: sdCircle(scale(p, 2.0), 0.1) * 2.0
            //        ^ don't forget to multiply the distance back!
            float2 scale(float2 p, float s)
            {
                return p / s;
            }
            float2 sdRepeat2D(float2 position, float2 spacing)
            {
                return position - spacing * round(position / spacing);
            }

            float map(float3 pos)
            {
                pos = sdf3DRepeatLimited(pos, 25, 100);
                float3 origin = float3(0,0,0);//GetCameraPositionWS() - GetViewForwardDir() * 10;
                float a = sdf3DBox(pos - origin, float3(4, 1, 4));
                float b = sdf3DSphere(pos - (origin + float3(0,1,0) * sin(_Time.y) * 4), 1.5f);
                return sdfOpSmoothUnion(a, b, 2);
                // float hex = sdf2DHexagon(rotate(pos, _Time.y), 0.2);
                // float t = sdf2DCircleWave(pos - float2(_Time.y * 0.35, 0), 0.5f, 0.1f);
                // float cap = sdf2DRhombus(pos - 0.5, 0.15f);
                // float cap2 = sdf2DRhombus(pos - 0.5 + 0.5*float2(sin(_Time.y * 0.7), 0), 0.25);
                // float cap12 = sdfOpSmoothIntersect(cap, cap2, 0.2);
                // float box = sdf2DBox(pos - 0.5 + float2(0, cos(_Time.y )) * 0.5, 0.1);
                // return smoothstep(0.999, 1, 1-sdfOpSmoothUnion(box, cap12, 0.5));
                // return sdfOpSmoothUnion(cap, sdfOpSmoothUnion(hex, t, 0.25), 0.5);
                // return hex;
            }

            // Gradient gives us the normal
            float3 calcNormal(float3 p)
            {
                float2 e = float2(0.0001, 0.0);
                return normalize(float3(
                    map(p + e.xyy) - map(p - e.xyy),
                    map(p + e.yxy) - map(p - e.yxy),
                    map(p + e.yyx) - map(p - e.yyx)
                ));
            }

            #include "Library/Noise/NoiseLib.hlsl"
            // Out frag function takes as input a struct that contains the screen space coordinate we are going to use to sample our texture. It also writes to SV_Target0, this has to match the index set in the UseTextureFragment(sourceTexture, 0, …) we defined in our render pass script.   
            float3 Frag(Varyings input) : SV_Target0
            {
                float3 shallow = float3(0.3, 0.9, 0.8);  // teal-green tint
                float3 deep = float3(0.05, 0.2, 0.5);    // deep navy
                
                float2 noiseUV = input.texcoord;
                float2 uvOffset = fbm_perlin_2d(noiseUV + _Time.y * 0.1f, 4);//fbm_perlin_2d(noiseUV + _Time.y * 0.1, 4);//BitangentNoise4D(float4(4*noiseUV, 0, _Time.y * 0.1));//curl_noise_worley_2d(noiseUV, 8);
                noiseUV += uvOffset * 0.05f * perlin_2d_01(noiseUV, 8);
                float noise = worley_edge_2d(noiseUV, 6, sin(_Time.y * 0.5f));
                noise = 1 - smoothstep(0.05, 0.04, noise);
                //return float3(noiseUV, 0);
                return lerp(shallow, deep, noise);
                float3 r0, rd;
                sdfRayDirection(input.texcoord, r0, rd);
                
                // Raymarch
                float maxSteps = 128;
                float t = 0.0;
                float minDist = FLT_MAX;
                for (int s = 0; s < maxSteps; s++)
                {
                    float3 p = r0 + rd * t;
                    float dS = map(p);
                    minDist = min(dS, minDist);
                    if (dS < 0.001) break;
                    if (t > 100) break;
                    t += dS;
                }

                float3 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);

                if (t < 100)
                {
                    float depthAtHit = LinearEyeDepth(SampleSceneDepth(input.texcoord), _ZBufferParams);
                    float3 p = r0 + rd * t;
                    float rayDepth = -TransformWorldToView(p).z;
                    float d = map(p);
                    float edge = smoothstep(0, 0.02, -d);

                    if (depthAtHit > rayDepth)
                    {
                        float3 normalsWS = calcNormal(p);
                        //return normalsWS;
                        float NoL = (dot(normalsWS, normalize(float3(1, 1, 1))) + 1.f) / 2.0f;
                        float sdfCol = NoL * NoL;

                        return lerp(sdfCol, sceneColor, edge);
                    }
                }

                return sceneColor;
            }
            ENDHLSL
        }
    }
}