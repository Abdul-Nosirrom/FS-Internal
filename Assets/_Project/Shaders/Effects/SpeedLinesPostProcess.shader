Shader "FreeSkies/Effects/SpeedLines"
{
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"
        }
        ZWrite Off Cull Off
        Pass
        {
            Name "SpeedLinesPass"

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "../Library/Transforms.hlsl"
            #include "../Library/Noise/Noise2D.hlsl"
            #include "../Library/UVHelpers.hlsl"
            #include "../Library/PlayerRenderData.hlsl"
            #include "Packages/com.abdulal.rendereffects/ShaderLibrary/UVFunctions.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            // From David Hoskins (MIT licensed): https://www.shadertoy.com/view/4djSRW
            float3 hash33(float3 p3) {
	            p3 = frac(p3 * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yxz + 33.33);
                return frac((p3.xxy + p3.yxx) * p3.zyx) - 0.5;
            }

            // From Nikita Miropolskiy (MIT licensed): https://www.shadertoy.com/view/XsX3zB
            float simplex3d(float3 p) {
	             float3 s = floor(p + dot(p, 1.0 / 3.0));
	             float3 x = p - s + dot(s, 1.0 / 6.0);
	             float3 e = step(0, x - x.yzx);
	             float3 i1 = e * (1.0 - e.zxy);
	             float3 i2 = 1.0 - e.zxy * (1.0 - e);
	             float3 x1 = x - i1 + 1.0 / 6.0;
	             float3 x2 = x - i2 + 1.0 / 3.0;
	             float3 x3 = x - 0.5;
	             float4 w = max(0.6 - float4(dot(x, x), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
	             w *= w;
	             return dot(float4(dot(hash33(s), x), 
                                 dot(hash33(s + i1), x1), 
                                 dot(hash33(s + i2), x2),  
                                 dot(hash33(s + 1.0), x3)) * w * w, 52);
            }
            
            float2 PolarCoordinates_AspectCorrect(float2 uv)
			{
            	uv -= 0.5f; // Recenter before aspect correction
            	float2 centeredUV = AspectCorrectUV(uv);
				float angle = atan2(centeredUV.y, centeredUV.x) / TWO_PI + 0.5; // Make it between [0, 1]
				float rad = length(centeredUV);
				return float2(angle, rad);
			}
            
            // Out frag function takes as input a struct that contains the screen space coordinate we are going to use to sample our texture. It also writes to SV_Target0, this has to match the index set in the UseTextureFragment(sourceTexture, 0, …) we defined in our render pass script.   
            float3 Frag(Varyings input) : SV_Target0
            {
            	//float4 playerPosCS = TransformWorldToHClip(_PlayerPosition);
            	//float2 playerScreenPos = (playerPosCS.xy + 1)/2.f;//GetNormalizedScreenSpaceUV(playerPosCS.xy);
            	//playerScreenPos.y = playerScreenPos.y - 0.5;
            	float3 col = SAMPLE_TEXTURE2D_LOD(_BlitTexture, sampler_LinearRepeat, input.texcoord, 0);
            	//float2 toPlayer = playerScreenPos - input.texcoord;
            	//return length(toPlayer);//(1 - length(toPlayer))*0.25f + col;
            	//return (1 - length(playerScreenPos)) * 0.2 + col;
            	float2 polarUV = PolarCoordinates_AspectCorrect(input.texcoord);
            	float angle = polarUV.x;//fmod(polarUV.x + _Time.y, 1); // [0,1] -> [0, 360]
            	
            	// Perturb the angle a bit for noise
            	float angularNoise = curl_noise_worley_2d(input.texcoord, 32);
            	angularNoise = (angularNoise - 0.5) * 2;
            	angle += angularNoise * 0.001;
            	
            	int numLines = 64;
            	int sectorID = floor(numLines * angle); // Primary hash for all randomness so lines are synced
            	
            	// Line length variation per-sector
            	float sectorRandom = RandomFloat(sectorID);
            	float lineLength = lerp(-0.2, 0.2, sectorRandom);
            	//lineLength = lerp(0.5, 0.9, lineLength);
            	
            	// Radial mask w/ custom line-lengths
            	float radius = polarUV.y / sqrt(2); // [0,1]
            	
            	// Shrink and expand radius randomly based on sector ID
            	float movSpeed = lerp(16, 32, sectorRandom);
            	radius += sin(_Time.y * movSpeed) * 0.1f;
            	//radius = 1-smoothstep(1, lineLength, radius);

            	angle = frac(numLines * angle);
            	// Wanna remap it so its a gradient at each end, so [0, 1, 0] instead of [0, 1]
            	angle = 2 * (angle - 0.5f); // [-1, 1]
            	angle = 1 - angle * angle; // [0, 1, 0]
            	
            	// [15 degree width of lines]
            	float angularWidth = 0.05f;
            	radius = smoothstep(0.4 + lineLength, 1, radius); // Manipulate this to change line-profile
            	angularWidth *= radius;
            	
            	// Width shifts by radius, getting smaller and shrinking to zero by LineLength
            	//return saturate(radius - sqrt(2)+0.1);
            	//return lineLength;
            	
            	angle = smoothstep(1 - angularWidth, 1, angle);
            	float lineMask = step(0.5, angle);
            	//return col * (1 - lineMask);
            	float normSpeed = _PlayerLateralSpeed / 15;
            	return lineMask * normSpeed * lerp(0.2, 1, sectorRandom) + col;
            	//return (1-lineMask) * col;
            	return lineMask;
            	if (angle < 0.95f) angle = 0;
            	
            	return angle;
            	return angle * 0.2f + col;
            }
            ENDHLSL
        }
    }
}