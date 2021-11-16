Shader "Hidden/VisionConeDepth"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass 
		{
			Tags 
			{
				"LightMode" = "ShadowCaster"
			}
			
			HLSLPROGRAM
			
			#pragma target 3.5
			
			#pragma multi_compile_instancing
			
			#pragma vertex ShadowCasterPassVertex
			#pragma fragment ShadowCasterPassFragment
			
			#include "VisionConeDepthPass.hlsl"
			
			ENDHLSL
		}
    }
}
