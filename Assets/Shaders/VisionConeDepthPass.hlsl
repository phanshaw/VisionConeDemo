#ifndef VISION_CONE_DEPTH_INCLUDED
#define VISION_CONE_DEPTH_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

float4x4 unity_MatrixVP;
float4x4 unity_ObjectToWorld;

#define UNITY_MATRIX_M unity_ObjectToWorld

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

struct VertexInput 
{
	float4 pos : POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput 
{
	float4 clipPos : SV_POSITION;
};

VertexOutput ShadowCasterPassVertex (VertexInput input) 
{
	VertexOutput output;
	UNITY_SETUP_INSTANCE_ID(input);
	float4 worldPos = mul(UNITY_MATRIX_M, float4(input.pos.xyz, 1.0));
	output.clipPos = mul(unity_MatrixVP, worldPos);

	float bias = 0.005;
	#if UNITY_REVERSED_Z
	output.clipPos.z -= bias;
	output.clipPos.z = min(output.clipPos.z, output.clipPos.w * UNITY_NEAR_CLIP_VALUE);
	#else
	output.clipPos.z += bias;
	output.clipPos.z = max(output.clipPos.z, output.clipPos.w * UNITY_NEAR_CLIP_VALUE);
	#endif
	
	return output;
}

float4 ShadowCasterPassFragment (VertexOutput input) : SV_TARGET 
{
	return 0;
}

#endif // VISION_CONE_DEPTH_INCLUDED 
