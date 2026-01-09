Shader "Dev/Character"
{
    Properties
    {
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
        [HDR][MainColor] _MainColor ("Color", Color) = (0.4, 0.2, 0.8, 1)
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

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        float4 _MainTex_ST;
        float4 _MainColor;

        v2f vert(appdata v)
        {
            v2f o;
            o.vertex = TransformObjectToHClip(v.vertex);
            o.worldPos = TransformObjectToWorld(v.vertex);
            o.normal = normalize(TransformObjectToWorldNormal(v.normal));

            // Calculate fog factor
            o.fogCoord = ComputeFogFactor(o.vertex.z);

            o.uv = TRANSFORM_TEX(v.uv, _MainTex);
            
            return o;
        }
        
        ENDHLSL

        Pass
        {
            Name "CharacterBase"
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
            #include "Assets/_Project/Shaders/Library/Effects.hlsl"

            float _HitStunFlashTime;

            float3 RimLight(float3 N, float3 V, float rimPower, float3 rimColor)
            {
                float NoV = 1 - saturate(dot(N, V));
                return pow(NoV, rimPower);
            }
            
            float3 frag(v2f i) : SV_Target
            {
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                i.normal = normalize(i.normal);
                float3 color = _MainColor;

                //if (texSample.r > 0 && texSample.b <= 0) color *= 0.5;

                // Apply some lighting
                
                // Half Lambertian Lighting (NoL * 0.5 + 0.5)
                {
                    float3 N = i.normal;
                    float4 shadowCoords = TransformWorldToShadowCoord(i.worldPos);
                    
                    // Main Light
                    Light L = GetMainLight(shadowCoords);
                    L.shadowAttenuation = 1;
                    float3 lightVal = cel_shading(L, N, 0.5, 0);

                    float rim = DepthBasedRim(i.vertex, L.direction, 1.5);
                    rim = step(0.7, rim);
                    lightVal += rim * L.color * 2;

                    // Additional Lights
                    for (int l = 0; l < GetAdditionalLightsCount(); l++)
                    {
                        L = GetAdditionalLight(l, i.worldPos, shadowCoords);
                        lightVal += cel_shading(L, N);
                    }

                    // Ambient
                    lightVal += SampleSH(float3(0,1,1));
                    
                    // Apply lighting
                    color *= lightVal;
                }

                // different grid coloring
                //int uInt = (uv.x - frac(uv.x)) % 2;
                //int vInt = (uv.y - frac(uv.y)) % 2;
                //color *= lerp(0.7, 1, saturate(uInt != vInt));
                //color = uInt != vInt;

                // Apply hit stun flash
                float3 hitStunColor = 0;
                // Red fresnel
                hitStunColor += smoothstep(0.2, 0.4, RimLight(i.normal, V, 2, 1)) * float3(1,0,0) * 4;
                float hitStunFlashAlpha = 1 - saturate((_Time.y - _HitStunFlashTime) / 0.2f);
                hitStunFlashAlpha = smoothstep(0, 0.7, hitStunFlashAlpha);
                color = lerp(color, hitStunColor, hitStunFlashAlpha);

                // Sample texture
                float3 texSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                color *= texSample;
                
                // Apply fog
                color = MixFog(color, i.fogCoord);
                
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "Outline"
            Tags
            {
                "LightMode"="InverseHullOutline"
            }
            
            Cull Front
            
            HLSLPROGRAM

            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #pragma multi_compile_fog

            v2f vertOutline(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex + normalize(v.normal) * 0.01);
                o.worldPos = TransformObjectToWorld(v.vertex);
                o.normal = normalize(TransformObjectToWorldNormal(v.normal));
                o.uv = v.uv;

                // Calculate fog factor
                o.fogCoord = ComputeFogFactor(o.vertex.z);

                return o;
            }
            float3 fragOutline(v2f i) : SV_Target
            {
                // Return the outline color
                return MixFog(0, i.fogCoord);
            }
            
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            
            ZWrite On
            ColorMask R
            
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            
            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
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