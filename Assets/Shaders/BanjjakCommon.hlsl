#ifndef BANJJAK_COMMON_INCLUDED
#define BANJJAK_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

// Shared shadow / depth / depth-normals passes. The including shader defines its
// UnityPerMaterial CBUFFER and an optional BJ_DISPLACE(positionOS, normalOS, uv) macro.

#ifndef BJ_DISPLACE
#define BJ_DISPLACE(p, n, uv, col) (p)
#endif

float3 _LightDirection;
float3 _LightPosition;

struct BjDepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv         : TEXCOORD0;
    float4 color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct BjDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

// BjClip(float2 uv) must be defined by the including shader before this file.

BjDepthVaryings BjShadowVert(BjDepthAttributes v)
{
    BjDepthVaryings o = (BjDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    float3 pOS = BJ_DISPLACE(v.positionOS.xyz, v.normalOS, v.uv, v.color);
    float3 positionWS = TransformObjectToWorld(pOS);
    float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    o.positionCS = ApplyShadowClamping(positionCS);
    o.uv = v.uv;
    return o;
}

half4 BjShadowFrag(BjDepthVaryings i) : SV_Target
{
    BjClip(i.uv);
    return 0;
}

BjDepthVaryings BjDepthVert(BjDepthAttributes v)
{
    BjDepthVaryings o = (BjDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float3 pOS = BJ_DISPLACE(v.positionOS.xyz, v.normalOS, v.uv, v.color);
    o.positionCS = TransformObjectToHClip(pOS);
    o.normalWS = TransformObjectToWorldNormal(v.normalOS);
    o.uv = v.uv;
    return o;
}

half BjDepthFrag(BjDepthVaryings i) : SV_Target
{
    BjClip(i.uv);
    return i.positionCS.z;
}

half4 BjDepthNormalsFrag(BjDepthVaryings i, bool front : SV_IsFrontFace) : SV_Target
{
    BjClip(i.uv);
    float3 n = normalize(i.normalWS) * (front ? 1.0 : -1.0);
    return half4(n, 0.0);
}

#endif
