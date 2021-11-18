#ifndef VISION_CONE_PASS_INCLUDED
#define VISION_CONE_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// Note this matches the define in VisionConeShaderConstants.cs
#define MAX_VISION_CONE_COUNT 16

// TODO Structured buffer might be better here.
struct VisionConeData
{
    int enabled;
    float radius;
    float arcAngle;
    float3 posWS;
    float3 dirWS;
    float4 color;
    float4 zBufferParams;
};

float4x4 _WorldToVisionConeMatrices[MAX_VISION_CONE_COUNT];
float4 _packedDataEnabledRadArc[MAX_VISION_CONE_COUNT];
float4 _conePosArray[MAX_VISION_CONE_COUNT];
float4 _coneDirArray[MAX_VISION_CONE_COUNT];
float4 _coneColorArray[MAX_VISION_CONE_COUNT];
float4 _visionConeZBufferParams[MAX_VISION_CONE_COUNT];

// for fast world space reconstruction
uniform float4x4 _FrustumCornersWS;
float4 _CameraWS;

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
float4 _MainTex_TexelSize;

TEXTURE2D(_CameraDepthTexture);
SAMPLER(sampler_CameraDepthTexture);
float4 _CameraDepthTexture_TexelSize;

TEXTURE2D(_VisionConeDepthTexture);
SAMPLER(sampler_VisionConeDepthTexture);

VisionConeData GetGameplayVisionConeData(int i)
{
    VisionConeData ret;
    ret.enabled = _packedDataEnabledRadArc[i].x;
    ret.radius = _packedDataEnabledRadArc[i].y;
    ret.arcAngle = _packedDataEnabledRadArc[i].z;
    ret.posWS = _conePosArray[i].xyz;
    ret.dirWS = _coneDirArray[i].xyz;
    ret.color = _coneColorArray[i];
    ret.zBufferParams = _visionConeZBufferParams[i];
    return ret;
}

float SampleVisionConeDepth(int index, float3 worldPos)
{
    // https://learnopengl.com/Advanced-Lighting/Shadows/Shadow-Mapping
    // Basically seeing if our current depth is higher than our sampled depth. 
    float4 shadowPos = mul(_WorldToVisionConeMatrices[index], float4(worldPos, 1.0));
    shadowPos.xyz /= shadowPos.w;
    
    float closestDepth = SAMPLE_TEXTURE2D_X(_VisionConeDepthTexture, sampler_VisionConeDepthTexture, shadowPos.xy).r;

    // TODO: Quick depth bias here, could be a property.
    float currentDepth = shadowPos.z + 0.0001;
    return currentDepth >= closestDepth  ? 1.0 : 0.0;  
}

float ATan2Nrm(float a, float b)
{
    return (atan2(a, b) + PI) / (PI * 2);
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
    float rawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, input.uv_depth).r;
    float dpth = Linear01Depth(rawDepth, _ZBufferParams);
    float3 posWS = _CameraWS.xyz + (dpth * input.interpolatedRay.xyz);

    float4 contribution = 0;
    [unroll(MAX_VISION_CONE_COUNT)] 
    for(int i = 0; i < MAX_VISION_CONE_COUNT; i++)
    {
        VisionConeData data = GetGameplayVisionConeData(i);
        if(data.enabled < 0.5)
            continue;
        
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

        // vertical falloff
        float t = saturate(relPos.y / 2);

        // cosine between those two angles
        float dp = dot(normalize(relPos2D.xyz), normalize(-dir2D));

        // get angle from the cosine from 0 (180) to 1 (90)
        float dp_angle = acos(dp) / PI;
        float data_angle = data.arcAngle / 360.0;

        // Now step it to get a mask of our cone shape. 
        float angleMask = step(dp_angle, data_angle);
        
        float occluded = SampleVisionConeDepth(i, posWS);

        // We want a consistent size here so we can't use v
        float grad = length(relPos2D) / 4;

        // Pulse fast at start slow at end
        grad = grad * grad * grad;

        // Repetitions
        float g1 = frac(grad * 8);

        // Speed
        float time = frac(_Time * 10);

        // Offset the gradient value to shift the pulse along. 
        float pulse = step( frac(g1 - time), 0.1);

        // Mask the pulse
        pulse *= angleMask * (1-v);
        
        // Add some nice details on the outer edge of the vision cone
        float u = ATan2Nrm(relPos.x, relPos.z);
        float dashedRing = step(0.6, frac(u * 100)) * step(1-v, 0.01) * occluded * angleMask;
        float inner_mask = step(v, 0.25);
        float result = saturate(((pulse + dashedRing + angleMask * occluded * max(1-v, 0.2) - inner_mask) * t));
        
        contribution = max(contribution, result * data.color);
    }

    return contribution;
}

#endif // VISION_CONE_PASS_INCLUDED