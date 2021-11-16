#ifndef VISION_CONE_PASS_INCLUDED
#define VISION_CONE_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

#define MAX_VISION_CONE_COUNT 16

struct VisionConeData
{
    int enabled;
    float radius;
    float arcAngle;
    float3 posWS;
    float3 dirWS;
    float4 color;
};

float4x4 _WorldToVisionConeMatrices[MAX_VISION_CONE_COUNT];
float4 _packedDataEnabledRadArc[MAX_VISION_CONE_COUNT];
float4 _conePosArray[MAX_VISION_CONE_COUNT];
float4 _coneDirArray[MAX_VISION_CONE_COUNT];
float4 _coneColorArray[MAX_VISION_CONE_COUNT];

// for fast world space reconstruction
uniform float4x4 _FrustumCornersWS;
float4 _CameraWS;

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
float4 _MainTex_TexelSize;

TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);
float4 _CameraDepthTexture_TexelSize;

TEXTURE2D_ARRAY_SHADOW(_VisionConeDepthTexture);
SAMPLER_CMP(sampler_VisionConeDepthTexture);

VisionConeData GetGameplayVisionConeData(int i)
{
    VisionConeData ret;
    ret.enabled = _packedDataEnabledRadArc[i].x;
    ret.radius = _packedDataEnabledRadArc[i].y;
    ret.arcAngle = _packedDataEnabledRadArc[i].z;
    ret.posWS = _conePosArray[i];
    ret.dirWS = _coneDirArray[i];
    ret.color = _coneColorArray[i];
    return ret;
}

float SampleVisionConeVisibility(int i, float3 worldPos)
{
    float4 shadowCoord = mul(_WorldToVisionConeMatrices[i], float4(worldPos, 1.0));
    shadowCoord.xyz /= shadowCoord.w;
    float attenuation = SAMPLE_TEXTURE2D_ARRAY_SHADOW(_VisionConeDepthTexture, sampler_VisionConeDepthTexture, shadowCoord.xyz, i);
    return attenuation;
}
    
struct Attributes
{
    float4 positionOS   : POSITION;
    float2 texcoord     : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
 
struct Varyings
{
    float4  positionCS  : SV_POSITION;
    float2  uv          : TEXCOORD0;
    float2  uv_depth : TEXCOORD1;
    float4  interpolatedRay : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings Vertex(Attributes input)
{
    Varyings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = input.texcoord;
    output.uv_depth = input.texcoord.xy;
    	
    output.interpolatedRay = _FrustumCornersWS[output.uv.x + 2 * output.uv.y];
    	
    return output;
}

float4 VisionConesFrag (Varyings input) : SV_Target
{
    float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, input.uv);
    float dpth = Linear01Depth(rawDepth, _ZBufferParams);
    float3 posWS = _CameraWS + (dpth * input.interpolatedRay);

    float contribution = 0;

    for(int i = 0; i < MAX_VISION_CONE_COUNT; i++)
    {
        VisionConeData data = GetGameplayVisionConeData(i);
        float3 decalPosWS = data.posWS.xyz;
        float3 relPos = decalPosWS - posWS;
        
        float3 relPos2D = relPos * float3(1, 0, 1);
        float3 dir2D = data.dirWS.xyz * float3(1, 0, 1);
        
        float v = length(relPos2D) / data.radius;

        // We only want the interior of the masked area. 
        if(v < 0 || v > 1)
        {
            continue;
        }

        // cosine between those two angles
        float dp = dot(normalize(relPos2D.xyz), normalize(-data.dirWS.xyz));

        // get angle from the cosine from 0 (180) to 1 (90)
        float dp_angle = acos(dp) / PI;
        float data_angle = data.arcAngle / 360.0;
        float angleMask = step(dp_angle, data_angle);

        //float occluded = 1-SampleVisionConeVisibility(i, posWS);
        contribution = max(contribution, saturate(angleMask));// max(contribution, result);
    }

    return contribution; // contribution * float4(1, 0, 0, 1);
}

#endif // VISION_CONE_PASS_INCLUDED