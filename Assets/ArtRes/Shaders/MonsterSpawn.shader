Shader "SecretCore/MonsterSpawn"
{
    Properties
    {
        [HDR] _BaseColor ("Energy Color", Color) = (0.5, 0.3, 0.9, 1)
        _Progress ("Spawn Progress", Range(0, 1)) = 0.25
        [Toggle] _Column ("Energy Column", Float) = 0
        _Intensity ("Energy Intensity", Range(0, 4)) = 1.4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Progress;
                float _Column;
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
                float2 uv : TEXCOORD0;
                float height : TEXCOORD1;
                float side : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                // Unity's cylinder extends from -1 to +1 on its local Y axis.
                output.height = input.positionOS.y * 0.5 + 0.5;
                output.side = 1.0 - abs(input.normalOS.y);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float p = saturate(_Progress);
                float envelope = smoothstep(0.0, 0.1, p) * (1.0 - smoothstep(0.5, 1.0, p));
                float shape;
                if (_Column > 0.5)
                {
                    float h = saturate(input.height);
                    float slices = pow(saturate(sin(input.uv.x * TWO_PI * 12.0 + h * 3.0)), 14.0);
                    float rising = pow(saturate(1.0 - frac(h * 2.5 - p * 2.0)), 9.0);
                    float heightFade = smoothstep(0.0, 0.04, h) * (1.0 - smoothstep(0.2, 1.0, h));
                    shape = (0.055 + slices * 0.6 + rising * 0.3) * heightFade * input.side;
                }
                else
                {
                    float2 centered = input.uv - 0.5;
                    float radius = length(centered);
                    float aa = max(fwidth(radius), 0.001);
                    float angle = atan2(centered.y, centered.x) / TWO_PI;
                    float ringRadius = lerp(0.18, 0.40, smoothstep(0.0, 0.45, p));
                    float ring = 1.0 - smoothstep(0.008, 0.008 + aa, abs(radius - ringRadius));
                    float broken = smoothstep(0.12, 0.22, frac(angle * 12.0 - p * 0.6));
                    float inner = (1.0 - smoothstep(0.012, 0.012 + aa, abs(radius - ringRadius * 0.78))) * broken;
                    float outer = (1.0 - smoothstep(0.004, 0.004 + aa, abs(radius - lerp(0.23, 0.485, p))));
                    float spokes = pow(saturate(cos(angle * TWO_PI * 6.0 + p * 3.0)), 32.0);
                    float radial = smoothstep(0.12, 0.18, radius) * (1.0 - smoothstep(ringRadius-0.02, ringRadius, radius));
                    shape = max(max(ring, inner * 0.7), max(outer * 0.5, radial * spokes * 0.65));
                }
                float alpha = saturate(shape * envelope) * _BaseColor.a;
                clip(alpha - 0.003);
                half3 color = lerp(_BaseColor.rgb, half3(0.83, 0.93, 1.0), shape * 0.25);
                return half4(color * _Intensity, alpha);
            }
            ENDHLSL
        }
    }
}
