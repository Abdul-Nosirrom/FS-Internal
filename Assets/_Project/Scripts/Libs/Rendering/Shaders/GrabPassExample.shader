Shader "Custom/GrabPassExample"
{
    Properties
    {
        _Cutoff("Cutoff", Range(0, 1)) = 0.5
        _Distortion("Distortion", 2D) = "black" {}
        _DistortionAmount("Distortion Amount", Range(0, 50)) = 0.2
    }
    
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Off

        Tags 
        { 
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "LightMode" = "GrabPass"
        }
        

        Pass
        {
            
            //BlendOp ColorDodge
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            TEXTURE2D(_GrabPassTexture);
            SAMPLER(sampler_GrabPassTexture);

            float _Cutoff;
            float _DistortionAmount;
            TEXTURE2D(_Distortion);
            SAMPLER(sampler_Distortion);
            float4 _Distortion_ST;
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }
            
            float4 frag(Varyings input) : SV_Target
            {
                
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float2 uv = input.uv + _Time.x * 2;;
                float2 distortion = SAMPLE_TEXTURE2D(_Distortion, sampler_Distortion, uv).xy;
                //return float4(distortion, 0, 1);
                //float2 distortion = norm.xy;
                distortion = (distortion * 2 - 1) * _DistortionAmount;// * pow(height, 4);
                screenUV += distortion * input.screenPos.z;
                float4 col = _GrabPassTexture.SampleLevel(sampler_GrabPassTexture, screenUV, 0);
                return col;
            }
            ENDHLSL
        }
    }
}