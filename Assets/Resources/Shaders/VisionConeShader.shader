Shader "Hidden/ScreenSpaceVisionConeAdditive"
{
    Properties
    {
        _MainTex("Albedo", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100

        Pass
        {
            Name "LightLeakPass"
            ZWrite Off
        	Blend One One

            HLSLPROGRAM

            #include "ScreenSpaceVisionConeAdditive.hlsl"

            #pragma prefer_hlslcc gles
            #pragma vertex Vertex
            #pragma fragment VisionConesFrag
 
            ENDHLSL
        }
    }
}
