Shader "SecretCore/CoreShield"
{
    Properties
    {
        [HDR] _BaseColor ("Camp Color", Color) = (0.1, 0.85, 1, 1)
        _Health ("Remaining Health", Range(0, 1)) = 1
        _HitFlash ("Hit Flash", Range(0, 1)) = 0
        _Opacity ("Shield Opacity", Range(0, 1)) = 0.32
        _Intensity ("Energy Intensity", Range(0, 4)) = 1.3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Health;
                float _HitFlash;
                float _Opacity;
                float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float fresnel = pow(1.0 - saturate(dot(normal, view)), 3.0);
                float scanY = frac(_Time.y * 0.14);
                float scan = exp2(-abs(input.uv.y - scanY) * 90.0);
                float2 grid = frac(input.uv * float2(24, 12));
                float lattice = 1.0 - smoothstep(0.02, 0.07, min(min(grid.x, 1.0-grid.x), min(grid.y, 1.0-grid.y)));
                float hit = saturate(_HitFlash);
                float waveY = lerp(0.08, 0.95, 1.0-hit);
                float impact = exp2(-abs(input.uv.y - waveY) * 38.0) * hit;
                float power = lerp(0.5, 1.0, saturate(_Health));
                float alpha = saturate(_Opacity * (fresnel * 0.8 + scan * 0.3 + lattice * 0.045) * power
                    + hit * (fresnel * 0.5 + lattice * 0.16) + impact * 0.6) * _BaseColor.a;
                clip(alpha - 0.003);
                half3 color = lerp(_BaseColor.rgb, half3(1.0, 0.93, 0.8), saturate(hit * 0.5 + impact));
                return half4(color * _Intensity * (1.0 + hit), alpha);
            }
            ENDHLSL
        }
    }
}
