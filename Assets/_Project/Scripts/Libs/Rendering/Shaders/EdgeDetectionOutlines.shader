Shader "Hidden/EdgeDetection"
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
            Name "EdgeDetectionPass"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            
            #pragma vertex Vert
            #pragma fragment Frag

            float _thickness;

            float _normalsThreshold;
            float _depthThreshold;

            float _fogFactor;

            
            float3x3 _sobelKernelY = float3x3(
                -1, 0, 1,
                -2, 0, 2,
                -1, 0, 1
            );
            float3x3 _sobelKernelX = float3x3(
                -1, -2, -1,
                0, 0, 0,
                1, 2, 1
            );
            // Precomputed 3x3 offsets for efficiency
            static const float2 offsets[9] = {
                float2(-1, -1), float2(0, -1), float2(1, -1),
                float2(-1,  0), float2(0,  0), float2(1,  0),
                float2(-1,  1), float2(0,  1), float2(1,  1)
            };


            float DepthSample(float2 uv, float2 offset)
            {
                return Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv + _CameraDepthTexture_TexelSize.xy * offset), _ZBufferParams);
            }

            float3 NormalsSample(float2 uv, float2 offset)
            {
                return SAMPLE_TEXTURE2D(_CameraNormalsTexture, sampler_CameraNormalsTexture, uv + _CameraNormalsTexture_TexelSize * offset);
            }

            float RobertsCross(float2 uv)
            {
                // Depth based RB
                float rawDepth = DepthSample(uv, float2(0,0));

                float thicknessFactor = saturate(rawDepth/50);
               // _thickness = lerp(_thickness, _thickness * 0.5f, thicknessFactor);
                //_thickness /= max(1, rawDepth/100);

                float depth = rawDepth;// * _depthThreshold;
                
                float dTR = DepthSample(uv, _thickness * float2(1,1));
                float dTL = DepthSample(uv, _thickness * float2(-1,1));
                float dBR = DepthSample(uv, _thickness * float2(1,-1));
                float dBL = DepthSample(uv, _thickness * float2(-1,-1));

                float rbC1 = (dTR - dBL) * (dTR - dBL);
                float rbC2 = (dTL - dBR) * (dTL - dBR);
                float res = sqrt(rbC1 + rbC2);

                // Normals based RB
                float3 nTR = NormalsSample(uv, _thickness * float2(1,1));
                float3 nTL = NormalsSample(uv, _thickness * float2(-1,1));
                float3 nBR = NormalsSample(uv, _thickness * float2(1,-1));
                float3 nBL = NormalsSample(uv, _thickness * float2(-1,-1));
                float nC1 = dot((nTR - nBL), (nTR - nBL));
                float nC2 = dot((nTL - nBR), (nTL - nBR));
//                return max(sqrt(nC1+nC2), saturate(res));
                
                float nRes = sqrt(nC1 + nC2);
                nRes = 1 - step(_normalsThreshold, nRes);
                res = 1 - step(depth, res);

                return max(0.5, min(nRes, res));
            }
            
            // Out frag function takes as input a struct that contains the screen space coordinate we are going to use to sample our texture. It also writes to SV_Target0, this has to match the index set in the UseTextureFragment(sourceTexture, 0, …) we defined in our render pass script.   
            float3 Frag(Varyings input) : SV_Target0
            {
               // _thickness = _thickness / 1000;
                // sample the texture using the SAMPLE_TEXTURE2D_X_LOD
                float2 uv = input.texcoord.xy;
                float3 col = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, uv, _BlitMipLevel);
                
                float eyeDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_CameraDepthTexture, uv), _ZBufferParams);
                
                float3 fogColor = float3(241, 191, 203)/255;
                
                // fog near
                float fogNear = 60;
                float fogFar = 150;
                float fogFactor = saturate((eyeDepth - fogNear) / (fogFar - fogNear));
                // inverse square fog
                fogFactor = pow(fogFactor, 2);
                col = lerp(col, fogColor, _fogFactor * fogFactor);
                return RobertsCross(uv) * col;
                
                // float xSob = 0;
                // float ySob = 0;
                //
                // float2 depthSobel = 0;
                // float3 normalsSobel = 0;
                //
                // [unroll]
                // for (int i = 0; i < 9; i++)
                // {
                //     float2 kernel = float2(_sobelKernelX[i / 3][i % 3], _sobelKernelY[i / 3][i % 3]);
                //     float2 offsetDir = offsets[i] * _thickness;
                //
                //     float depthSample = DepthSample(uv, offsetDir);
                //
                //     depthSobel += kernel * depthSample;
                // }
                //
                //
                // //depthSobel = depthSobel;
                // float derivMag =  length(depthSobel);
                // return step(derivMag, _depthThreshold) * col;
                // return col * (1 - step(_depthThreshold, derivMag));
                // return col * float3(depthSobel, 0);
            }
            ENDHLSL
        }
    }
}