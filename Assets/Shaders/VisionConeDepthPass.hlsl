#ifndef VISION_CONE_DEPTH_INCLUDED
#define VISION_CONE_DEPTH_INCLUDED

// This is a stripped down version of DepthOnlyPass.hlsl for the purposes of rendering the vision cone occluders.

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct Attributes
{
	float4 position     : POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
	float4 positionCS   : SV_POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};

Varyings DepthOnlyVertex(Attributes input)
{
	Varyings output = (Varyings)0;
	UNITY_SETUP_INSTANCE_ID(input);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
	output.positionCS = TransformObjectToHClip(input.position.xyz);
	return output;
}

half4 DepthOnlyFragment(Varyings input) : SV_TARGET
{
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
	return 0;
}

#endif // VISION_CONE_DEPTH_INCLUDED 
