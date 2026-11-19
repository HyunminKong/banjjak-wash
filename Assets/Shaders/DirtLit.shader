Shader "Banjjak/DirtLit"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _BumpMap ("Normal (RG)", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Float) = 1
        _UseNormal ("Use Normal Map", Float) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.3
        _Metallic ("Metallic", Range(0,1)) = 0
        _UVScale ("UV Scale", Float) = 1
        _Emission ("Emission", Color) = (0,0,0,0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half _BumpScale;
        half _UseNormal;
        half _Smoothness;
        half _Metallic;
        float _UVScale;
        half4 _Emission;
        half _Cull;
    CBUFFER_END

    TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
    void BjClip(float2 uv) { }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE3D(_Mask3);
            TEXTURE3D(_Type3);
            TEXTURE2D(_DirtNoise);
            SAMPLER(my_linear_clamp_sampler);
            SAMPLER(my_point_clamp_sampler);
            SAMPLER(my_linear_repeat_sampler);

            // per-renderer (MaterialPropertyBlock) and global values
            float4 _G0;
            float4 _GS;
            float4 _InvDims;
            float4 _PC1;
            float4 _PC2;
            float _HasDirt;
            float4 _DC1[16];
            float4 _DC2[16];
            float4 _DP[16];
            float4 _FoamCols[5];
            float _DirtHL;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3;
                float3 positionOS : TEXCOORD4;
                float  fogFactor  : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = n.normalWS;
                o.tangentWS = float4(n.tangentWS, v.tangentOS.w * GetOddNegativeScale());
                o.uv = v.uv * _UVScale * _BaseMap_ST.xy + _BaseMap_ST.zw;
                o.positionOS = v.positionOS.xyz;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float side = front ? 1.0 : -1.0;
                float3 nWS = normalize(i.normalWS) * side;
                if (_UseNormal > 0.5)
                {
                    half4 nm = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv);
                    half3 nTS;
                    nTS.xy = (nm.rg * 2.0 - 1.0) * _BumpScale;
                    nTS.z = sqrt(saturate(1.0 - dot(nTS.xy, nTS.xy)));
                    float3 t = normalize(i.tangentWS.xyz);
                    float3 b = cross(nWS, t) * i.tangentWS.w;
                    nWS = normalize(nTS.x * t + nTS.y * b + nTS.z * nWS);
                }

                half3 col = albedo.rgb;
                half smooth = _Smoothness;
                half metal = _Metallic;
                half3 emission = _Emission.rgb;

                if (_HasDirt > 0.5)
                {
                    float3 pos = i.positionOS;
                    float3 dtc = (pos - _G0.xyz) / _GS.xyz;
                    // noise in world space so neighbouring zones of one floor line up without a seam
                    float3 wp = i.positionWS;
                    float2 nuv = float2(wp.x + wp.z, wp.y + wp.z * 0.5) / 1.7;
                    float3 jt = SAMPLE_TEXTURE2D(_DirtNoise, my_linear_repeat_sampler, nuv * 11.0).rgb - 0.5;
                    jt = (jt + (SAMPLE_TEXTURE2D(_DirtNoise, my_linear_repeat_sampler, nuv * 23.0 + 0.5).gbr - 0.5) * 0.6) * _InvDims.xyz;
                    float4 mk = SAMPLE_TEXTURE3D(_Mask3, my_linear_clamp_sampler, dtc + jt * 0.7);
                    int dty = clamp((int)(SAMPLE_TEXTURE3D(_Type3, my_point_clamp_sampler, dtc + jt * 1.4).r * 255.0 + 0.5), 0, 15);
                    float dm = mk.r;
                    float wet = mk.g;
                    float fm = mk.b;
                    float4 nz = SAMPLE_TEXTURE2D(_DirtNoise, my_linear_repeat_sampler, nuv);
                    float4 dp = _DP[dty];
                    float3 c1 = dty == 15 ? _PC1.rgb : _DC1[dty].rgb;
                    float3 c2 = dty == 15 ? _PC2.rgb : _DC2[dty].rgb;
                    float dA = saturate((dm - 0.1) * 1.12 * (0.78 + 0.55 * nz.r));
                    float3 dcol = lerp(c1, c2, smoothstep(dp.x, dp.y, nz.g)) * (0.85 + 0.3 * nz.b);
                    col = lerp(col, dcol, dA);
                    col *= 1.0 - 0.3 * wet * (1.0 - dA);

                    float4 nz2 = SAMPLE_TEXTURE2D(_DirtNoise, my_linear_repeat_sampler, nuv * 5.0 + 0.37);
                    float fA = smoothstep(0.06, 0.35, fm) * (0.72 + 0.28 * smoothstep(0.35, 0.65, nz2.g));
                    int ai = min((int)floor(mk.a * 5.0 + 0.001), 4);
                    float3 fcol = _FoamCols[ai].rgb;
                    col = lerp(col, fcol * (0.86 + 0.14 * nz2.b), fA * 0.94);

                    // foam bubbles get a little bumpy relief
                    float3 bump = (SAMPLE_TEXTURE2D(_DirtNoise, my_linear_repeat_sampler, nuv * 31.0).rgb - 0.5) * 0.5 * fA;
                    nWS = normalize(nWS + bump);

                    float rough = 1.0 - smooth;
                    rough = lerp(rough, dp.z, dA * 0.85);
                    rough = lerp(rough, 0.12, wet * (1.0 - dA));
                    rough = lerp(rough, 0.45, fA);
                    smooth = 1.0 - rough;
                    metal *= (1.0 - dA) * (1.0 - fA);
                    emission += float3(1.0, 0.42, 0.05) * 0.55 * smoothstep(0.12, 0.2, dm) * _DirtHL;
                }

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = nWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogFactor;
                inputData.bakedGI = SampleSH(nWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = col;
                s.metallic = metal;
                s.smoothness = smooth;
                s.normalTS = half3(0, 0, 1);
                s.emission = emission;
                s.occlusion = 1;
                s.alpha = 1;
                s.specular = 0;

                half4 c = UniversalFragmentPBR(inputData, s);
                c.rgb = MixFog(c.rgb, i.fogFactor);
                c.a = 1;
                return c;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BjShadowVert
            #pragma fragment BjShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "BanjjakCommon.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BjDepthVert
            #pragma fragment BjDepthFrag
            #pragma multi_compile_instancing
            #include "BanjjakCommon.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex BjDepthVert
            #pragma fragment BjDepthNormalsFrag
            #pragma multi_compile_instancing
            #include "BanjjakCommon.hlsl"
            ENDHLSL
        }
    }
    FallBack Off
}
