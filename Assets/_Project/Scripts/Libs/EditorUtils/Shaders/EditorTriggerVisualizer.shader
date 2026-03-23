Shader "Hidden/Editor/TriggerVisualizer"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _InsideSign ("Inside Sign", Float) = 1
        
        /// 0 = Sphere, 1 = Box, 2-4 = Capsule, 5 = Mesh
        _ShapeType ("Shape Type", Integer) = 0
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
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

		    TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
		        float4 _MainTex_ST;
                float _InsideSign;
                int _ShapeType;
		    CBUFFER_END

            struct vsInput
            {
                float4 vertex : POSITION;
		        float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct psInput
            {
                float4 vertex       : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float3 normalOS     : TEXCOORD3;
                float3 objectScale  : TEXCOORD4;
                float3 positionOS   : TEXCOORD5;
            };


            psInput vert (vsInput v)
            {
                // Get object scale to transform UVs by, first by getting the object to world matrix and extracting scale from it
                float3 objectScale = float3(
                    length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x)), // scale x axis
                    length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y)), // scale y axis
                    length(float3(unity_ObjectToWorld[0].z, unity_ObjectToWorld[1].z, unity_ObjectToWorld[2].z))  // scale z axis
                    );
                
                psInput o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                o.positionWS = TransformObjectToWorld(v.vertex);
                o.positionOS = v.vertex;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);

                o.normalOS = v.normalOS;

                o.objectScale = objectScale;

                return o;
            }

            float4 frag (psInput i) : SV_Target
            {
                // Calculate view direction in world space
                float3 viewDirWS = normalize(GetCameraPositionWS() - i.positionWS);
                
                // Get the dot product between view direction and normal
                float viewDotNormal = dot(viewDirWS, normalize(i.normalWS));
                float backFaceSign = sign(viewDotNormal);
                
                // 4 Cases here:
                // 1.) Backface & Inside [-, -] -> + dont clip
                // 2.) Backface & Outside [-, +] -> - clip
                // 3.) Frontface & Inside [+, -] -> - clip
                // 4.) Frontface & Outside [+, +] -> + dont clip
                clip(backFaceSign * _InsideSign);

                if (_ShapeType == 0) // Sphere
                {
                    i.uv *= max(1, floor(abs(i.objectScale.x)));
                }
                else if (_ShapeType == 1) // Box
                {
                    // Figure out most aligned axis of the normal
                    float3 absNormal = normalize(abs(i.normalOS));

                    if (absNormal.x > 0)
                    {
                        i.uv *= abs(i.objectScale.zy);
                    }
                    else if (absNormal.y > 0)
                    {
                        i.uv *= abs(i.objectScale.xz);
                    }
                    else if (absNormal.z > 0)
                    {
                        i.uv *= abs(i.objectScale.xy);
                    }

                    i.uv /= 4;
                }
                else if (_ShapeType >= 2 && _ShapeType < 5) // Capsule
                {
                    // Handle clipping
                    if (_ShapeType == 2) // cylinder base
                    {
                        float clipSign = lerp(1, -1, abs(i.normalOS.y));
                        clip(clipSign);

                        i.uv.x *= max(1, abs(i.objectScale.x));
                        i.uv.y *= max(1, abs(i.objectScale.y))*2;
                    }
                    else if (_ShapeType == 3) // top sphere
                    {
                        clip(i.normalOS.y);
                        i.uv *= max(1, abs(i.objectScale.x));
                    }
                    else if (_ShapeType == 4) // bottom sphere
                    {
                        clip(-i.normalOS.y);
                        i.uv *= max(1, abs(i.objectScale.x));
                    }
                }
                
                // sample the texture
                float4 col = _MainTex.Sample(sampler_MainTex, i.uv);;
                return col;
            }
            ENDHLSL
        }
    }
}
