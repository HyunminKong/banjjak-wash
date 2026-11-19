Shader "Banjjak/Foliage"
{
    // Alpha-tested leaves / grass with wind sway, vertex colour tint and soft back-lighting.
    Properties
    {
        _BaseMap ("Leaf (RGBA)", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        _Wind ("Wind Amount", Float) = 0.035
        _GrassMode ("Grass Mode", Float) = 0
        _Translucency ("Translucency", Range(0,1)) = 0.35
        _Smoothness ("Smoothness", Range(0,1)) = 0.2
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half _Cutoff;
        float _Wind;
        float _GrassMode;
        half _Translucency;
        half _Smoothness;
    CBUFFER_END

    TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

    float3 BjWind(float3 pOS, float2 uv, float4 col)
    {
        float3 wp = TransformObjectToWorld(pOS);
        float t = _Time.y;
        if (_GrassMode > 0.5)
        {
            // uv.y = height along the blade, col.a = cut amount (1 = mown)
            float h = uv.y;
            float sw = (sin(t * 1.7 + wp.x * 0.55 + wp.z * 0.4) * 0.12 + sin(t * 2.9 + wp.x * 1.3) * 0.04) * (1.0 - col.a * 0.85);
            float3 d = float3(sw * h * h, 0, sw * 0.4 * h * h);
            pOS.y *= lerp(1.0, 0.17, col.a);   // mown blades are short stubs
            return pOS + mul((float3x3)GetWorldToObjectMatrix(), d);
        }
        float3 off;
        off.x = sin(t * 1.4 + wp.x * 0.6 + wp.y * 0.8) * _Wind;
        off.z = cos(t * 1.1 + wp.z * 0.6 + wp.y * 0.5) * _Wind;
        off.y = 0;
        return pOS + mul((float3x3)GetWorldToObjectMatrix(), off);
    }
    #define BJ_DISPLACE(p, n, uv, col) BjWind(p, uv, col)

    void BjClip(float2 uv)
    {
        half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv * _BaseMap_ST.xy + _BaseMap_ST.zw).a * _BaseColor.a;
        clip(a - _Cutoff);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 color      : TEXCOORD3;
                float  fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 pOS = BjWind(v.positionOS.xyz, v.uv, v.color);
                VertexPositionInputs p = GetVertexPositionInputs(pOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half alpha = tex.a * _BaseColor.a;
                clip(alpha - _Cutoff);
                half3 albedo = tex.rgb * _BaseColor.rgb * i.color.rgb;
                float3 n = normalize(i.normalWS) * (front ? 1.0 : -1.0);

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fogFactor;
                inputData.bakedGI = SampleSH(n);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.smoothness = _Smoothness;
                s.normalTS = half3(0, 0, 1);
                s.occlusion = 1;
                s.alpha = 1;

                half4 c = UniversalFragmentPBR(inputData, s);

                // light passing through thin leaves
                Light mainLight = GetMainLight(inputData.shadowCoord);
                half back = saturate(dot(-n, mainLight.direction));
                c.rgb += albedo * mainLight.color * back * mainLight.shadowAttenuation * _Translucency;

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
            Cull Off
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
            Cull Off
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
            Cull Off
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
