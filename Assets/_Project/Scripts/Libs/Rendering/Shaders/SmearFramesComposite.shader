Shader "Hidden/PostProcess/SmearFramesComposite"
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
            Name "Smear Frames Composite"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            // Out frag function takes as input a struct that contains the screen space coordinate we are going to use to sample our texture. It also writes to SV_Target0, this has to match the index set in the UseTextureFragment(sourceTexture, 0, …) we defined in our render pass script.   
            float4 Frag(Varyings input) : SV_Target0
            {
                // sample the texture using the SAMPLE_TEXTURE2D_X_LOD
                float2 uv = input.texcoord.xy;
                float4 col = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, uv, 0);
                float grayscale = dot(col.rgb, float3(0.299, 0.587, 0.114));
                grayscale = step(0.2, grayscale) * 1.0;
                //return float4(grayscale, 0, 0, 1);
                return float4(float3(0.8,0.1,0.5), grayscale);//1-length(col.rgb));
            }
            ENDHLSL
        }
        Pass
        {
            // This pass essentially darkes the previous frame to create a smearing effect when combined with the current frame in the first pass.
            Name "Smear Frames Temporal Accumulation"
            
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            TEXTURE2D(_CurrentSmearFrame);
            TEXTURE2D(_PrevSmearFrame);

            // TEMPORAL REPROJECTION SO COOL!!!!!! I LOVE LYING!!!!!
            float4 Frag(Varyings input) : SV_Target0
            {
                float2 uv = input.texcoord.xy;

                // 1. Sample current smear frame
                float4 currentSmearCol = SAMPLE_TEXTURE2D(_CurrentSmearFrame, sampler_LinearClamp, uv);

                // 2. Reconstruct world position from depth
                float depth = SampleSceneDepth(uv);
                float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);

                // 3. Reproject to previous frame's screen space based on world position of current pixel
                float4 prevClipPos = mul(_PrevViewProjMatrix, float4(worldPos, 1.0));
                prevClipPos /= prevClipPos.w;

                // 4. Convert prev clip pos to uvs
                //float2 prevUV = GetNormalizedScreenSpaceUV(prevClipPos);
                float2 prevUV = prevClipPos.xy * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                    prevUV.y = 1.0 - prevUV.y;
                #endif
                //return float3(prevUV, 0);

                // 5. Sample previous accumulation w/ bounds check - if out of bounds, return black
                float4 prevAccum = float4(0,0,0,0);
                //if (all(prevUV >= 0.0) && all(prevUV <= 1.0))
                {
                    prevAccum = SAMPLE_TEXTURE2D(_PrevSmearFrame, sampler_LinearClamp, prevUV);
                }

                // 6. Accumulate w/ decay
                prevAccum = lerp(prevAccum, 0, unity_DeltaTime.z * 5);
                float4 result = prevAccum + currentSmearCol;

                return float4(saturate(result).rgb, saturate(result.a));
            }
            ENDHLSL
        }
    }
}