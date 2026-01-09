Shader "Hidden/DebugTagLayer"
{
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
            Name "DebugTagLayerView"
            
            ZWrite On
            ZTest LEqual
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Matcap);
            SAMPLER(sampler_Matcap);
            float3 _DebugColor;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.normal = TransformObjectToWorldNormal(v.normal);

                return o;
            }

            float3 frag(v2f i) : SV_Target
            {
                // Sample matcap texture
                float3 viewNormal = normalize(mul((float3x3)unity_WorldToCamera, i.normal));
                float2 normalUV = viewNormal.xy * 0.5 + 0.5;
                float3 matcapSample =SAMPLE_TEXTURE2D(_Matcap, sampler_Matcap, normalUV);
                float matcapVal = max(matcapSample.r, max(matcapSample.g, matcapSample.b));
                matcapVal = lerp(matcapVal, 1, 0.2);
                return matcapVal * _DebugColor * 1.5;
            }
            ENDHLSL
        }
    }
}