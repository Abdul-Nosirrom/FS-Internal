Shader "Dev/MatCap"
{
    Properties
    {
        [NoScaleOrOffset] _MatCapTexture ("Texture", 2D) = "white" {}
        _Tint ("Color", Color) = (1, 1, 1, 1)
        
        _EdgeColor ("Edge Color", Color) = (0.4, 0.4, 1, 1)
        _CenterColor ("Center Color", Color) = (1, 0.4, 0.4, 1)
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 100
        
        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Assets/_Project/Shaders/Library/UVHelpers.hlsl"
        
        struct appdata
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float2 uv : TEXCOORD0;
        };

        struct v2f
        {
            float4 vertex : SV_POSITION;
            float3 worldPos : TEXCOORD0;
            float3 normal : NORMAL;
            float2 uv : TEXCOORD1;
            float fogCoord : TEXCOORD2;
        };

        v2f vert(appdata v)
        {
            v2f o;
            o.vertex = TransformObjectToHClip(v.vertex);
            o.worldPos = TransformObjectToWorld(v.vertex);
            o.normal = normalize(TransformObjectToWorldNormal(v.normal));

            // Calculate fog factor
            o.fogCoord = ComputeFogFactor(o.vertex.z);

            o.uv = v.uv;
            
            return o;
        }
        
        ENDHLSL

        Pass
        {
            Name "BlockoutGrid"
            Tags
            {
                "LightMode"="UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOW_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            
            #include "Assets/_Project/Shaders/Library/Lighting.hlsl"

            TEXTURE2D(_MatCapTexture);
            SAMPLER(sampler_MatCapTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float4 _EdgeColor;
                float4 _CenterColor;
            CBUFFER_END
                
            float3 frag(v2f i) : SV_Target
            {
                float2 mUV = TransformWorldToViewNormal(i.normal, false) * 0.5f + float2(0.5f, 0.5f);
                
                float3 color = SAMPLE_TEXTURE2D(_MatCapTexture, sampler_MatCapTexture, mUV).rgb * _Tint;
                color = lerp(_EdgeColor, _CenterColor, clamp(length(mUV) - 0.5, 0, 1));
                // Apply fog
                color = MixFog(color, i.fogCoord);

                // Apply shadowing
                                // Half Lambertian Lighting (NoL * 0.5 + 0.5)
                {
                    float3 N = i.normal;   
                    
                    // Main Light
                    Light L = GetMainLight(TransformWorldToShadowCoord(i.worldPos));
                    float3 lightVal = L.shadowAttenuation;

                    // Additional Lights
                    for (int l = 0; l < GetAdditionalLightsCount(); l++)
                    {
                        L = GetAdditionalLight(l, i.worldPos, TransformWorldToShadowCoord(i.worldPos));
                        lightVal += L.shadowAttenuation;
                    }

                    // Ambient
                    lightVal = max(0.5, smoothstep(0.4, 0.6, lightVal));
                    lightVal += SampleSH(N);
                    
                    // Apply lighting
                    color *= lightVal;
                }

                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "Depth"
            Tags
            {
                "LightMode"="DepthOnly"
            }
            
            // Render State Commands
            ZWrite On
            ColorMask R
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float3 frag(v2f i) : SV_Target
            {
                // Output depth only
                return i.vertex.z;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode"="ShadowCaster"
            }
            
            // Render State Commands
            ZWrite On
            ZTest LEqual
            ColorMask 0
            

            HLSLPROGRAM
            // Note: Special care must be taken w/ the shadow caster pass. Look at how Unity does it for reference
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            
            // This is used during shadow map generation to differentiate between directional and punctual light shadows, as they use different formulas to apply Normal Bias
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            ENDHLSL
        }
    }
}