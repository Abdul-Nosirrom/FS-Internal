Shader "Hidden/Editor/PlayerStartMarker"
{
    Properties
    {
        [MainColor] _MainColor ("Main Color", Color) = (0,0.8,0,0.8)
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }
        LOD 100

        Pass
        {
            Name "Visualizer"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _MainColor;

            struct vsInput
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
		        float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct psInput
            {
                float4 vertex       : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };


            psInput vert (vsInput v)
            {
                psInput o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                o.color = float4(v.color.rgb, 1);
                return o;
            }

            float4 frag (psInput i) : SV_Target
            {
                return _MainColor * i.color * lerp(0.5, 1.0, sin(PI * i.uv.y));
            }
            ENDHLSL
        }
    }
}
