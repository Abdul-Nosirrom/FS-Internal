Shader "Hidden/DebugView"
{
    SubShader
   {
       Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
       ZWrite Off Cull Off
       Pass
       {
           Name "BlitWithMaterialPass"

           HLSLPROGRAM
           #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
           #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

           #pragma vertex Vert
           #pragma fragment Frag

           int _DebugViewMode;
           
           // Out frag function takes as input a struct that contains the screen space coordinate we are going to use to sample our texture. It also writes to SV_Target0, this has to match the index set in the UseTextureFragment(sourceTexture, 0, …) we defined in our render pass script.   
           float3 Frag(Varyings input) : SV_Target0
           {
                // this is needed so we account XR platform differences in how they handle texture arrays
               UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

               // sample the texture using the SAMPLE_TEXTURE2D_X_LOD
               float2 uv = input.texcoord.xy;
               float3 col = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearRepeat, uv, _BlitMipLevel);
               
               switch (_DebugViewMode)
                {
                    case 1: // Depth
                        col = Linear01Depth(col.r, _ZBufferParams); // Use red channel for depth
                        break;
                    case 2: // Normals
                        col = (col + 1)/2; // Use red and green channels for normals
                        break;
                    case 8: // Motion Vectors
                        col = float4(col.rg * 2.0 - 1.0, 0.0, 1.0); // Convert to motion vector range
                        break;
                    default: // None
                        col = float3(1,0,1);
                        break;
                }


                return col;
           }

           ENDHLSL
       }
   }
}