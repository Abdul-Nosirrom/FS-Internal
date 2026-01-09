Shader "Hidden/Effect/WorldGrid"
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
            Name "WorldGridPass"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            float3 ReconstructWorldPosition(float2 uv)
            {
                float depth = SampleSceneDepth(uv);
                return ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
            }

            float PositionFactor(float posAxis)
            {
                posAxis = abs(frac(posAxis) - 0.5) * 2.0;
                return step(0.9, posAxis);
                return smoothstep(0.9, 1, posAxis);
            }

            // Out frag function takes as input a struct that contains the screen space coordinate we are going to use to sample our texture. It also writes to SV_Target0, this has to match the index set in the UseTextureFragment(sourceTexture, 0, …) we defined in our render pass script.   
            float3 Frag(Varyings input) : SV_Target0
            {
                // sample the texture using the SAMPLE_TEXTURE2D_X_LOD
                float2 uv = input.texcoord.xy;

                float3 worldPos = ReconstructWorldPosition(uv);

                // Grid lines along 1m interval at widths of 0.1m (X is red, Z is blue, Y is green)

                // Smoothstep from frac of 0.9-0.1 to get anti-aliased lines
                float xFactor = PositionFactor(worldPos.x);
                float zFactor = PositionFactor(worldPos.z);
                float yFactor = PositionFactor(worldPos.y);

                return 0.8f * float3(xFactor, 0, zFactor);
            }
            ENDHLSL
        }
    }
}