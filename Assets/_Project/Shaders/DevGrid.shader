// =============================================================================
// Blockout.shader
// Example shader demonstrating the framework.
// =============================================================================

Shader "Dev/Blockout"
{
    Properties
    {
        [Header(Surface)]
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)
        
        [Toggle(_USE_SEPERATE_TOP_COLOR)] _UseSeperateTopColor("Use Separate Top Color", Float) = 0
        [ShowIf(_USE_SEPERATE_TOP_COLOR)] _TopColor("Top Color", Color) = (1, 1, 1, 1)
        _StrokesTexture("Strokes Texture", 2D) = "white" {}

        [Header(Normal)]
        [Toggle(_NORMALMAP)] _UseNormalMap("Use Normal Map", Float) = 0
        [NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
        
        [Header(Specular)]
        [Toggle(_SPECULAR)] _UseSpecular("Enable Specular", Float) = 0
        _SpecSize("Specular Size", Range(1, 256)) = 64
        _SpecSmoothness("Specular Smoothness", Range(0, 0.5)) = 0.05
        
        [Header(Reflections)]
        [Toggle(_ENABLE_SSR)] _SSREnabled("Enable Reflections", Float) = 0
        [ShowIf(_ENABLE_SSR)] _Roughness("Roughness", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "TerrainCompatible" = "True"
            "PreviewType" = "Plane"
        }

        // =====================================================================
        // SHARED CODE
        // =====================================================================
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // CBUFFER - identical across all passes for SRP Batcher
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _TopColor;
        
            half _SpecSize;
            half _SpecSmoothness;

            float _Roughness;
        CBUFFER_END

        // Textures
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);
        TEXTURE2D(_EmissionMap);
        SAMPLER(sampler_EmissionMap);

        TEXTURE2D(_StrokesTexture);
        SAMPLER(sampler_StrokesTexture);

        TEXTURE2D(_SSRTex);
        SAMPLER(sampler_SSRTex);

        #include "Library/Core/Common.hlsl"
        
        // Material keywords
        #pragma shader_feature_local _NORMALMAP
        #pragma shader_feature_local _ENABLE_SSR
        #pragma shader_feature_local_fragment _SPECULAR
        #pragma shader_feature_local_fragment _USE_SEPERATE_TOP_COLOR

        #include "Library/Core/Lighting/LightLoop.hlsl"
        #include "Library/Core/DebugSupport.hlsl"
        #include "Library/Core/Lighting/LightingModels.hlsl"
        #include "Library/Effects.hlsl"

        // #ifndef DEBUG_DISPLAY
        //     #define DEBUG_DISPLAY
        // #endif
        
        #define EXTRA_ATTRIBUTES float4 color : COLOR;
        #define EXTRA_INTERPOLATORS \
            float4 color : COLOR;\
            float3 positionWS : TEXCOORD1;\
            float3 viewDirWS : TEXCOORD4;
        
        #define TRANSFER_EXTRA(output, input)\
            output.color = input.color;\
            output.positionWS = TransformObjectToWorld(input.positionOS);\
            output.viewDirWS = GetWorldSpaceNormalizeViewDir(output.positionWS);
        
        half GridLinesFactor(float2 uv, float gridSize, float lineWidth)
        {
            float2 gridUV = uv * gridSize;
            float2 derivative = fwidth(gridUV);
            
            // Fade out when grid cells are smaller than a few pixels
            float gridDensity = max(derivative.x, derivative.y);
            float fadeOut = 1.0 - saturate(gridDensity * 2.0 - 0.5);
            
            float2 grid = abs(frac(gridUV - 0.5) - 0.5) / (derivative * lineWidth);
            float lineG = min(grid.x, grid.y);
            
            return saturate(1.0 - lineG) * fadeOut;
        }
        
        ENDHLSL

        // =====================================================================
        // FORWARD PASS
        // =====================================================================
        Pass
        {
            Name "BlockoutPass"
            Tags
            {
                "LightMode" = "UniversalForward"
                "TerrainCompatible" = "True"
            }

            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            
            // Material keywords
            #pragma shader_feature_local _ENABLE_SSR

            #include_with_pragmas "Library/Keywords/ForwardKeywords.hlsl"
            
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                #define VERTEX_LIGHTS_INTERP half3 vertexLight : TEXCOORD7;
            #else
                #define VERTEX_LIGHTS_INTERP
            #endif
            
            #define PASS_ATTRIBUTES float2 lightmapUV : TEXCOORD1;
            #define PASS_INTERPOLATORS \
                half fogFactor : TEXCOORD5;\
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 6);\
                VERTEX_LIGHTS_INTERP
            
            
            #define NEEDS_NORMAL
            #define NEEDS_TANGENT
            #include "Library/Core/StructBuilder.hlsl"

            Interpolators Vert(Attributes input)
            {
                Interpolators output = INIT_INTERPOLATORS;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexData v = GetVertexData(input.positionOS.xyz, input.normalOS, input.tangentOS);

                output.positionCS = v.positionCS;
                output.positionWS = v.positionWS;
                output.normalWS = v.normalWS;
                output.viewDirWS = v.viewDirWS;
                output.color = input.color;
                output.tangentWS = v.tangentWS;

                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(v.positionCS.z);

                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(v.normalWS, output.vertexSH);

                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    output.vertexLight = DoVertexLighting(v.positionWS, v.normalWS);
                #endif

                return output;
            }

            TEXTURE2D(_SkyBox);
            SAMPLER(sampler_SkyBox);

            half4 Frag(Interpolators input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 reflectionDir = reflect(-input.viewDirWS, input.normalWS);
                // Sample the reflection probe (unity_SpecCube0 is still the name internally)
                half3 envSample = GlossyEnvironmentReflection(
                    reflectionDir,
                    0,  // perceptualRoughness (0 = mirror, 1 = fully rough)
                    1.0   // occlusion
                );
                
                // Surface
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = baseMap.rgb * _BaseColor.rgb;
                half gridFactor = 0;//smoothstep(0.3, 0.6, GridLinesFactor(input.uv * 2, 1, 2));
                
                #if defined(_USE_SEPERATE_TOP_COLOR)
                    float topColorBlendFactor = smoothstep(0.9f, 1.0f, input.normalWS.y);
                    albedo = lerp(albedo, baseMap.rgb * _TopColor.rgb, topColorBlendFactor);
                #endif

                // TODO: Issue here with material previews being black, think it needs conditional compilation
                //ApplyDecalToBaseColor(input.positionCS, albedo);

                albedo = lerp(albedo, albedo * 0.2, gridFactor);

                ApplyLODCrossfade(input.positionCS);

                // Normal
                half3 normalWS;
                #if defined(_NORMALMAP)
                    half4 n = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                    half3 normalTS = UnpackNormal(n);
                    float3 bitangent = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                    normalWS = ApplyNormalMap(normalTS, input.tangentWS.xyz, bitangent, input.normalWS);
                #else
                normalWS = normalize(input.normalWS);
                #endif

                float3 viewDirWS = normalize(input.viewDirWS);
                float4 shadowCoord = GetShadowCoord(input.positionWS, input.positionCS);
                half4 shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
                
                if (CanDebugOverrideOutputColor(albedo, input.positionCS, input.positionWS, normalWS, SAMPLE_GI(input.lightmapUV, input.vertexSH, normalWS)))
                    return half4(albedo, 1);

                // Lighting
                half3 color = 0;
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, shadowMask);
                CleanupShadowAttenuation(mainLight);

                float3 lighting = cel_shading(mainLight, normalWS);

                // Specular
                #if defined(_SPECULAR)
                    float3 H = normalize(mainLight.direction + viewDirWS);
                    half spec = pow(saturate(dot(normalWS, H)), _SpecSize);
                    spec = smoothstep(0.5f - _SpecSmoothness, 0.5f + _SpecSmoothness, spec);
                    spec *= mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                    lighting += spec * mainLight.color;
                #endif

                // Additional lights
                LIGHT_LOOP_BEGIN(input.positionWS, input.positionCS)
                    Light light = LIGHT_LOOP_GET_LIGHT_SHADOW(input.positionWS, shadowMask);
                    CleanupShadowAttenuation(light);
                    lighting += cel_shading(light, normalWS, 0.5f, 0.1f, 0);

                    #if defined(_SPECULAR)
                        H = normalize(light.direction + viewDirWS);
                        spec = pow(saturate(dot(normalWS, H)), _SpecSize);
                        spec = smoothstep(0.5f - _SpecSmoothness, 0.5f + _SpecSmoothness, spec);
                        spec *= light.shadowAttenuation * light.distanceAttenuation;
                        lighting += spec * light.color;
                    #endif
                LIGHT_LOOP_END

                // Vertex lighting
                #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                    lighting += input.vertexLight;
                #endif

                // GI
                lighting += SAMPLE_GI(input.lightmapUV, input.vertexSH, normalWS);

                // Strokes mask
                half strokesSample = SAMPLE_TEXTURE2D(_StrokesTexture, sampler_StrokesTexture, input.uv).r;
                lighting *= lerp(0.7f, 1.f, strokesSample);
                //return half4(lighting, 1);
                

                // Apply lighting to color
                color.rgb = input.color * albedo * lighting;

    //            float fresnel = FresnelRimLight(normalWS, viewDirWS, 0.5, 0.5);
    // #ifdef _ENABLE_SSR            
    //             // Apply SSR
    //             half mip = PerceptualRoughnessToMipmapLevel(_Roughness);
    //             float3 reflection = SAMPLE_TEXTURE2D_LOD(_SSRTex, sampler_SSRTex, GetNormalizedScreenSpaceUV(input.positionCS), 5);
    //             // Mask by fresnel
    //             color.rgb += reflection * fresnel;
    // #endif            

                // Fog
                color.rgb = MixFog(color, input.fogFactor);

                return half4(color.rgb, 1);
            }
            
            ENDHLSL
        }

        // =====================================================================
        // SSR RELATED PASSES
        // =====================================================================
        Pass
        {
            Name "SSR Mask"
            Tags {"LightMode" = "SSRMask"}
            Cull Back
            
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment FragSSRMask
            #include "Library/Core/Passes/ForwardVertex.hlsl"
            float FragSSRMask(Interpolators i) : SV_TARGET
            {
                return 1;
            }
            ENDHLSL
        }

        // =====================================================================
        // SHADOW CASTER
        // =====================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #include_with_pragmas "Library/Core/Passes/ShadowCaster.hlsl"
            ENDHLSL
        }

        // =====================================================================
        // DEPTH ONLY
        // =====================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #include_with_pragmas "Library/Core/Passes/DepthOnly.hlsl"
            ENDHLSL
        }

        // =====================================================================
        // DEPTH NORMALS
        // =====================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #include_with_pragmas "Library/Core/Passes/DepthNormals.hlsl"
            ENDHLSL
        }

        // =====================================================================
        // MOTION VECTORS
        // =====================================================================
        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }

            ColorMask RG
            Cull Back

            HLSLPROGRAM
            #include_with_pragmas "Library/Core/Passes/MotionVectors.hlsl"
            ENDHLSL
        }

        // =====================================================================
        // META (Lightmap baking)
        // =====================================================================
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #include_with_pragmas "Library/Core/Passes/Meta.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "FS.Rendering.SSRShaderGUI"
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}