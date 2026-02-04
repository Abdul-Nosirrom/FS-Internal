// =============================================================================
// Character.shader
// Core Shader For All Characters, Cel-Shading w/ SSS map features, combat system compatible
// and more.
// =============================================================================

Shader "FreeSkies/Character"
{
    Properties
    {
        [SectionHeader(Colors And Lighting)]
        
        //////////////////////////
        // COLORS
        /////////////////////////
        [Header(Main Colors)]
        [MainTexture] [NoScaleOrOffset]
        _BaseMap("Albedo", 2D) = "white" {}
        [MainColor]
        _BaseColor("Albedo Tint", Color) = (1, 1, 1, 1)
        
        [Header(Shaded Colors)]
        [Toggle(_SSS_MAP)] [Tooltip(SSS Map defines a tint in the shaded areas)]
        _UseSSSMap("Use SSS Map", Float) = 0
        [ShowIf(_SSS_MAP)] [NoScaleOrOffset]
        _SSSMap("SSS Map", 2D) = "white" {}
        [HDR]
        _ShadowTint("Shaded Color Tint", Color) = (0.7, 0.6, 0.6, 1)

        //////////////////////////
        // LIGHTS
        /////////////////////////
        [Header(Light Parameters)]
        
        [Toggle(_LIGHT_RAMP)]
        _UseLightRamp("Use Light Ramp Gradient", Float) = 0
        [ShowIf(_LIGHT_RAMP)] [NoScaleOrOffset]
        _LightRamp("Light Ramp", 2D) = "white" {}
        
        [HideIf(_LIGHT_RAMP)]
        _LightTerminator("Light Terminator/Threshold", Range(0,1)) = 0.5
        [HideIf(_LIGHT_RAMP)]
        _ShadowSmoothness("Shadow Smoothness", Range(0, 1)) = 0
        
        [Tooltip(How much world shadows apply)]
        _ShadowInfluence("Shadow Influence", Range(0,1)) = 1 // Toggle or no range?
        
        //////////////////////////
        // RIM HIGHLIGHTS
        /////////////////////////
        [SectionHeader(Rim Highlights)]
        [Toggle(_RIM)]
        _UseRim("Enable Rim Highlights", Float) = 0
        [Tooltip(Where rim highlights are applied relative to lighting. neg1 shaded areas. 0 everywhere. 1 lit areas)]

        [Tooltip(Control where the Rim Highlight is applied based on the light mask. Negative 1 applies on shaded, 0 everywhere, 1 Lit)]
        [ShowIf(_RIM)] [Enum(Shadows, 0, Everywhere, 1, Lit, 2)]
        _RimLightInfluence("Light Influence", Int) = 0

        [ShowIf(_RIM)] [Enum(Depth, 0, Fresnel, 1, Depth x Fresnel, 2)]
        _RimHighlightMode("Rim Highlight Mode", Int) = 2
        [ShowIf(_RIM)] [HDR] _RimColor("Rim Color", Color) = (1, 1, 1, 1)  // alpha channel could control how rim blends with base color
        
        [Header(Fresnel Settings)]
        [ShowIf(_RIM)] _RimThreshold("Rim Threshold", Range(0, 1)) = 0.5
        [ShowIf(_RIM)] _RimSmoothness("Rim Smoothness", Range(0, 1)) = 0
        [Header(Depth Highlight Settings)]
        [ShowIf(_RIM)] _RimDepthMaskStrength("Rim Depth Mask Strength", Range(0, 1)) = 1
        [ShowIf(_RIM)] _RimDepthMaskThreshold("Rim Depth Threshold", Range(0, 1)) = 0.7
        [ShowIf(_RIM)] _RimDepthOffset("Rim Depth Offset", Range(0, 10)) = 0.25
        
        //////////////////////////
        // DISSOLVE / ALPHA
        /////////////////////////
        [SectionHeader(Dissolve)]
        [Header(Alpha Settings)]
        [Toggle(_ALPHATEST_ON)] _UseAlphaDissolve("Alpha Dissolve Feature", Float) = 0 // NOTE we dont need to call it _ALPHATEST_ON if we define our own func, our own func overrides
        [ShowIf(_ALPHATEST_ON)] _AlphaDissolveTex("Dissolve Texture", 2D) = "white" {}
        [ShowIf(_ALPHATEST_ON)] _AlphaCutoff("Dissolve Amount", Range(0,1)) = 0
        [Header(Dissolve Burn)]
        [ShowIf(_ALPHATEST_ON)] [HDR] _BurnColor("Dissolve Burn Color", Color) = (0,0,0)
        [ShowIf(_ALPHATEST_ON)] _BurnSize("Burn Size", Range(0,1)) = 0
        [ShowIf(_ALPHATEST_ON)] _BurnSmoothness("Burn Smoothness", Range(0,1)) = 0
        
        //////////////////////////
        // OUTLINES
        /////////////////////////
        [SectionHeader(Outlines)]
        [Toggle(_ENABLE_OUTLINES)] _EnableOutlines("Enable Outlines", Float) = 0
        [ShowIf(_ENABLE_OUTLINES)] _OutlineWidth("Outline Width", Range(0,1)) = 0.1
        [ShowIf(_ENABLE_OUTLINES)] _OutlineColor("Outline Color", Color) = (0,0,0,1)
        
        //////////////////////////
        // MISC
        /////////////////////////
        [HideInInspector] _HitStunFlashTime("Hit Stun Flash Time", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // =====================================================================
        // SHARED CODE
        // =====================================================================
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Library/CombatParameters.hlsl"

        // CBUFFER - identical across all passes for SRP Batcher
        CBUFFER_START(UnityPerMaterial)
            // Base colors
            float4 _BaseMap_ST;
            float4 _BaseColor;

            // SSS Params
            float4 _ShadowTint;

            // Light
            float _LightTerminator;
            float _ShadowSmoothness;
            float _ShadowInfluence;
        
            // Rim Light
            int _RimHighlightMode;

            int _RimLightInfluence;
        
            float _RimSmoothness;
            float _RimThreshold;
            float4 _RimColor;
            float _RimDepthMaskStrength;
            float _RimDepthMaskThreshold;
            float _RimDepthOffset;
        
            // Alpha Cutoff/Dissolve
            float4 _AlphaDissolveTex_ST;
            float _AlphaCutoff;
            float4 _BurnColor;
            float _BurnSize;
            float _BurnSmoothness;

            // Outlines
            float _OutlineWidth;
            float4 _OutlineColor;

            COMBAT_PARAMETERS;
        CBUFFER_END

        // Textures
        TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
        TEXTURE2D(_SSSMap);             SAMPLER(sampler_SSSMap);
        TEXTURE2D(_LightRamp);          SAMPLER(sampler_LightRamp);
        TEXTURE2D(_AlphaDissolveTex);   SAMPLER(sampler_AlphaDissolveTex);

        #define RIM_DEPTH_BASED (_RimHighlightMode != 1)
        #define RIM_FRESNEL_BASED (_RimHighlightMode != 0)

        #include "Library/Core/Common.hlsl"
        #include "Library/Effects.hlsl"
        
        #define EXTRA_ATTRIBUTES float4 color : COLOR;
        #define EXTRA_INTERPOLATORS \
            float4 color : COLOR;\
            float3 positionWS : TEXCOORD4;\
            float3 positionOS : TEXCOORD5;\
            float3 viewDirWS : TEXCOORD6;
        
        #define TRANSFER_EXTRA(output, input)\
            output.color = input.color;\
            output.positionOS = input.positionOS.xyz;\
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);\
            output.viewDirWS = GetWorldSpaceNormalizeViewDir(output.positionWS);
        
        // Computes the dissolve
        float4 AlphaDissolveBurn(float2 uv)
        {
            // Easy early outs
            if (_AlphaCutoff <= 0) return 0;
            if (_AlphaCutoff >= 1) { discard; }
            
            uv = TRANSFORM_TEX(uv, _AlphaDissolveTex);
            float4 alphaTex = SAMPLE_TEXTURE2D(_AlphaDissolveTex, sampler_AlphaDissolveTex, uv);
            float alpha = alphaTex.a;
            if (alpha <= _AlphaCutoff)
                discard;
            
            float edgeDistance = alpha - _AlphaCutoff;
            float burnMask = smoothstep(_BurnSize, _BurnSize - _BurnSmoothness, edgeDistance);
            return float4(_BurnColor.rgb * lerp(0.3, 1, alphaTex.r), _BurnColor.a * burnMask);
        }

        #pragma shader_feature_local_fragment _ALPHATEST_ON

        #ifdef _ALPHATEST_ON
            #define ALPHA_CLIP(input) AlphaDissolveBurn(GetNormalizedScreenSpaceUV(input.positionCS))
        #endif

        ENDHLSL

        // =====================================================================
        // FORWARD PASS
        // =====================================================================
        Pass
        {
            Name "CharacterPass"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            // Material keywords
            #pragma shader_feature_local_fragment _SSS_MAP
            #pragma shader_feature_local_fragment _LIGHT_RAMP
            #pragma shader_feature_local_fragment _RIM
            #pragma shader_feature_local_fragment _RIM_NOISEMASK

            #include_with_pragmas "Library/Keywords/ForwardKeywords.hlsl"
            #include "Library/Core/Lighting/LightLoop.hlsl"
            #include "Library/Core/DebugSupport.hlsl"
            
            #define NEEDS_NORMAL
            #define NEEDS_TANGENT
            
            #define PASS_ATTRIBUTES float2 lightmapUV : TEXCOORD1;
            #define PASS_INTERPOLATORS\
                half fogFactor : FOG_FACTOR;\
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, VERTEX_SH);
            
            #include "Library/Core/StructBuilder.hlsl"
            
            Interpolators Vert(Attributes input)
            {
                Interpolators output = INIT_INTERPOLATORS;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexData v = GetVertexData(input.positionOS.xyz, input.normalOS, input.tangentOS);

                output.positionCS = v.positionCS;
                output.positionWS = v.positionWS;
                output.positionOS = input.positionOS;
                output.normalWS = v.normalWS;
                output.viewDirWS = v.viewDirWS;
                output.tangentWS = v.tangentWS;
            
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(v.positionCS.z);
                
                OUTPUT_SH(v.normalWS, output.vertexSH);
                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                return output;
            }

            // Pass in accumulating lightmask & lighting 
            void ComputeLighting(Light L, float3 N, float3 V, inout float3 light, inout float lightMask)
            {
                // remapped lambert
                float NoL = dot(N, L.direction) * 0.5f + 0.5f;
                //L.shadowAttenuation = lerp(1, L.shadowAttenuation, _ShadowInfluence);
                L.shadowAttenuation = step(0.5, L.shadowAttenuation);//smoothstep(0.3, 1, L.shadowAttenuation);
                float lightVal = NoL * L.shadowAttenuation * L.distanceAttenuation;
#ifdef _LIGHT_RAMP                
                lightVal = SAMPLE_TEXTURE2D(_LightRamp, sampler_LightRamp, lightVal);
#else
                lightVal = smoothstep(_LightTerminator - _ShadowSmoothness/2.f, _LightTerminator + _ShadowSmoothness/2.f, lightVal);
                lightVal = lerp(0.4, 1, lightVal); // this gives us a nice 'extended hue' from additional lights w/out the hard cutoff
#endif
                lightMask += lightVal;
                light += lightVal * L.color;
            }

            #include "Library/Noise/Noise3D.hlsl"
            float3 Frag(Interpolators input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                
                //float y = input.positionOS.y;
                //float osNoise = worley_3d(input.positionOS, 8, sin(_Time.y) * sin(_Time.y));
                //return osNoise;
                //y = 1-saturate((osNoise * 0.25f + y) / 8.f);
                //float clipT = sin(_Time.y * 0.25f) * sin(_Time.y * 0.25f);
                //if (y <= 0.95) discard;
                //return y;

                float4 burn = float4(0,0,0,0);
#ifdef _ALPHATEST_ON            
                // Do early so clipping early outs we dont sample textures
                burn = AlphaDissolveBurn(GetNormalizedScreenSpaceUV(input.positionCS));
#endif                
                ApplyLODCrossfade(input.positionCS);

                // Surface
                float4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                albedo.rgb *= _BaseColor;
                albedo.rgb = lerp(albedo.rgb, burn.rgb, burn.a);

                float3 shadowTint = _ShadowTint;
#ifdef _SSS_MAP
                shadowTint *= SAMPLE_TEXTURE2D(_SSSMap, sampler_SSSMap, input.uv);
#endif 
                
                // Get shadow coords
                float4 shadowCoords = GetShadowCoord(input.positionWS, input.positionCS);

                // Normal
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                
                if (CanDebugOverrideOutputColor(albedo.rgb, input.positionCS, input.positionWS, normalWS, SAMPLE_GI(input.lightmapUV, input.vertexSH, normalWS)))
                    return albedo.rgb;

                // Lighting
                Light mainLight = GetMainLightData(input.positionWS, shadowCoords);

                float lightMask = 0; float3 lighting = 0;
                ComputeLighting(mainLight, normalWS, viewDirWS, lighting, lightMask);

                // Additional lights
                _ShadowSmoothness = max(0.1f, _ShadowSmoothness); // For additional lights some gradient is ok if not a bit better actually
                LIGHT_LOOP_BEGIN(input.positionWS, input.positionCS)
                    Light light = LIGHT_LOOP_GET_LIGHT_SHADOW(input.positionWS, 1);
                    ComputeLighting(light, normalWS, viewDirWS, lighting, lightMask);
                LIGHT_LOOP_END

                lightMask = saturate(lightMask);

                // Apply shadow tint pre-ambient by color selection via lightmask
                lighting = lerp(shadowTint, lighting, lightMask);

                // Ambient, fixed normal direction to flatten it
                lighting += SampleSH(float3(0,1,1));

                // Apply lighting to color
                float3 color = albedo * lighting;
                
                // Apply rim as a color override (masked by lighting) TODO: Param for this to move it around between shaded/lit areas
#if defined(_RIM)
                //return DepthBasedRim(input.positionCS, mainLight.direction, _RimDepthOffset);
                float rimDepth = RIM_DEPTH_BASED ? step(_RimDepthMaskThreshold, lerp(DepthBasedRim(input.positionCS, mainLight.direction, _RimDepthOffset), 1, _RimDepthMaskStrength)) : 1;
                float rimFresnel = RIM_FRESNEL_BASED ? FresnelRimLight(normalWS, viewDirWS, _RimSmoothness, _RimThreshold) : 1;
                float rim = rimDepth * rimFresnel;
                
                // Apply light influence mask on rim
                _RimLightInfluence = _RimLightInfluence - 1; // [-1, 1] instead of [0, 1, 2]
                float rimMask = 1.0 - saturate(abs(lightMask - (_RimLightInfluence * 0.5 + 0.5)) / _RimSmoothness);
                // remap so bias=0 gives full rim everywhere
                rimMask = lerp(1.0, rimMask, abs(_RimLightInfluence));
                rim *= rimMask;

                // Rim gets main light color influence
                color = lerp(color, color * _RimColor.rgb * mainLight.color, rim * _RimColor.a); // idk if a lerp is better than an add at intermediate values in terms of mimicking a rim light
#endif

                // Black-out fresnel
                float fresnelBlackout = 1 - FresnelRimLight(normalWS, viewDirWS, 0, 0.65);
                float mainLightNoL = 1 - step(0, dot(mainLight.direction, normalWS));
                fresnelBlackout = (1 - fresnelBlackout);// * mainLightNoL;
                //return (1 - fresnelBlackout) * mainLightNoL;
                //color *= (1 - fresnelBlackout);
                
                ApplyHitStunColor(normalWS, viewDirWS, _HITSTUN_FLASH_TIME, color);

                // Fog
                color = MixFog(color, input.fogFactor);
                
                return color;
            }
            ENDHLSL
        }
        // =====================================================================
        // INVERSE HULL OUTLINES
        // =====================================================================
        Pass
        {
            Name "Inverse Hull Outlines"
            Tags { "LightMode" = "InverseHull" }
            ZWrite Off
            ZTest LEqual
            Cull Front
            
            HLSLPROGRAM
            #include_with_pragmas "Library/Core/Passes/InverseHullOutlines.hlsl"
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

    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}