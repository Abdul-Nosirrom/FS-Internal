Shader "Hidden/Editor/PreviewPlane"
{
    Properties
    {}
    SubShader
    {
      Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "Grid"
            Tags
            {
                "LightMode"="UniversalForward"
            }
            ZWrite False
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
                    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            TEXTURE2D(_ShadowTexture);
            SAMPLER_CMP(sampler_ShadowTexture);

            CBUFFER_START(UnityPerMaterial)
                matrix _ShadowTextureMatrix;
                float4 _clearColor;
                bool _isOrtho;
                float3 _cameraUp;
                float3 _cameraRight;
            CBUFFER_END
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.worldPos = TransformObjectToWorld(v.vertex);
                return o;
            }
            
            float gridValue(float2 coords, float tiling, float size)
            {
                float2 aa = fwidth(coords * tiling);
                float2 gridUV = 1 - abs(frac(coords * tiling) * 2 - 1);
                float2 grid2 = smoothstep(size + aa, size - aa, gridUV);
                return lerp(grid2.x, 1, grid2.y);
            }

            float gridSplit(float2 coords, float tiling)
            {
                float size = 0.05;
                float2 aa = fwidth(coords * tiling);
                float2 gridUV = 1 - abs(floor(coords * tiling))%2;
                float2 grid2 = smoothstep(size + aa, size - aa, gridUV);
                return grid2;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                float depth = distance(i.worldPos.xz, GetCameraPositionWS().xz);
                depth =  saturate(depth/ 40);
                
                float4 shadowCoord = float4(mul(_ShadowTextureMatrix, float4(i.worldPos, 1.0)).xyzw);
                shadowCoord.xyz /= shadowCoord.w; // Perspective divide
                float shadowSample = SAMPLE_TEXTURE2D_SHADOW(_ShadowTexture, sampler_ShadowTexture, shadowCoord.xyz);

                float2 pos = i.worldPos.xz;
                if (_isOrtho)
                {
                    pos.x = dot(i.worldPos, _cameraRight);
                    pos.y = dot(i.worldPos, _cameraUp);
                }
                
                float sizeFactor = 1 - depth;
                float grid1 = 1 * gridValue(pos, 0.25, 0.01 * sizeFactor);
                float grid2 = 0.75 * gridValue(pos, 1, 0.05 * sizeFactor);
                float grid3 = 0.5 * gridValue(pos, 4, 0.1 * sizeFactor);
                
                float gridfinal = max(grid1, max(grid2, grid3));
                float4 gridColor = lerp(0, 0.5, gridfinal);
                //gridfinal = lerp(gridfinal, 0, depth);
                
                float4 finalColor = lerp(_clearColor, gridColor, gridfinal);
                finalColor = lerp(finalColor, finalColor * (0.3/0.5), shadowSample);
                return float4(finalColor.xyz, 1 - depth * depth);
            }
            ENDHLSL
        }
    }
}