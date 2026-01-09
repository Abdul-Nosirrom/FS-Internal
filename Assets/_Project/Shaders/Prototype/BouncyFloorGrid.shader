Shader "Dev/BouncyBlockoutGrid"
{
    Properties
    {
        [MainTexture] _GridTexture ("Texture", 2D) = "white" {}
        [MainColor] _GridColor ("Color", Color) = (1, 0.4, 0.2, 1)
        _GridBoundaryColor("Boundary Color", Color) = (0.4, 0.4, 0.4, 0.4)
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
            "PreviewType" = "Plane"
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

        float3 _BouncePoint;
        float _BounceTime;

        float GetBounceDisplacementFactor(float3 worldPos)
        {
            float bounceSphereRadius = 8;
            float waveLength = 0.5f;
            
            float normalizedTime = saturate((_Time.y - _BounceTime) / waveLength);
            float damping = 1.0 - normalizedTime; // Fade out over time
            float bounceAlpha = -sin(2 * PI * normalizedTime) * damping;
            //bounceAlpha = (bounceAlpha + 1) / 2; // Remap if needed
            
            float bounceDist = distance(worldPos, _BouncePoint);
            float bounceDistAttenuation = saturate(1 - (bounceDist / bounceSphereRadius)); // 1 at center, 0 at edge
            
            return bounceAlpha * bounceDistAttenuation;
        }

        v2f vert(appdata v)
        {
            v2f o;
            o.worldPos = TransformObjectToWorld(v.vertex);

            float bounceFactor = GetBounceDisplacementFactor(o.worldPos);
            v.vertex.xyz += v.normal * bounceFactor * 4;

            o.vertex = TransformObjectToHClip(v.vertex);
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

            TEXTURE2D(_GridTexture);
            SAMPLER(sampler_GridTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _GridTexture_ST;
                float4 _GridColor;
                float4 _GridBoundaryColor;
            CBUFFER_END
                
            float3 frag(v2f i) : SV_Target
            {
                i.normal = normalize(i.normal);
                float2 uv = i.uv;//GetPlanarCoordinates(i.worldPos, i.normal);
                
                float3 texSample = SAMPLE_TEXTURE2D(_GridTexture, sampler_GridTexture, uv).rgb;//_GridTexture.Sample(sampler_GridTexture, uv);

                float colVal = texSample.r;//smoothstep(0, 0.05, texSample.r);
                float3 color = lerp(_GridBoundaryColor, _GridColor, colVal);
                color = lerp(_GridColor * 0.8f, _GridColor, texSample);

                //if (texSample.r > 0 && texSample.b <= 0) color *= 0.5;

                // Apply some lighting
                
                // Half Lambertian Lighting (NoL * 0.5 + 0.5)
                {
                    float3 N = i.normal;
                    #if SHADOWS_SCREEN
                    float4 shadowCoords = ComputeScreenPos(i.vertex);
                    #else
                    float4 shadowCoords = TransformWorldToShadowCoord(i.worldPos);
                    #endif
                    
                    // Main Light
                    Light L = GetMainLight(shadowCoords);
                    //L.shadowAttenuation = smoothstep(0.2f, 0.5f, L.shadowAttenuation);
                    L.shadowAttenuation = step(0.7, L.shadowAttenuation);
                    L.shadowAttenuation = lerp(0.5f, 1.0f, L.shadowAttenuation);
                    float3 lightVal = half_lambert(L, N);

                    // Additional Lights
                    for (int l = 0; l < GetAdditionalLightsCount(); l++)
                    {
                        L = GetAdditionalLight(l, i.worldPos, shadowCoords);
                        lightVal += half_lambert(L, N);
                    }

                    // Ambient
                    lightVal += SampleSH(N);
                    
                    // Apply lighting
                    color *= lightVal;
                }

                // different grid coloring
                //int uInt = (uv.x - frac(uv.x)) % 2;
                //int vInt = (uv.y - frac(uv.y)) % 2;
                //color *= lerp(0.7, 1, saturate(uInt != vInt));
                //color = uInt != vInt;
                // Apply fog
                color = MixFog(color, i.fogCoord);

                //return GetBounceDisplacementFactor(i.worldPos);

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
            Name "DepthNormals"
            Tags
            {
                "LightMode"="DepthNormals"
            }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float3 frag(v2f i) : SV_Target
            {
                // Output depth only
                return normalize(i.normal);
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