Shader "SecretCore/CoreMarker"
{
    Properties
    {
        [HDR] _BaseColor ("Camp Color", Color) = (0.1, 0.85, 1, 1)
        _Health ("Remaining Health", Range(0, 1)) = 1
        _HitFlash ("Hit Flash", Range(0, 1)) = 0
        _Intensity ("Energy Intensity", Range(0, 3)) = 1.2
        _Speed ("Orbit Speed", Range(0, 2)) = 0.18
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
                float _Health;
                float _HitFlash;
                float _Intensity;
                float _Speed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uvOffset = input.uv - 0.5;
                float radius = length(uvOffset);
                float aa = max(fwidth(radius), 0.001);
                float angle = frac(atan2(uvOffset.x, uvOffset.y) / TWO_PI + 1.0);
                float healthArc = step(angle, saturate(_Health)) * step(0.001, _Health);
                float segments = smoothstep(0.05, 0.12, frac(angle * 40.0));
                float rim = 1.0 - smoothstep(0.002, 0.002 + aa, abs(radius - 0.445));
                float band = (1.0 - smoothstep(0.017, 0.017 + aa, abs(radius - 0.405))) * segments;
                float orbitAngle = frac(angle - _Time.y * _Speed);
                float orbit = pow(saturate(1.0 - frac(orbitAngle * 3.0)), 10.0);
                float inner = (1.0 - smoothstep(0.003, 0.003 + aa, abs(radius - 0.35))) * (0.2 + orbit * 0.8);
                float hitProgress = 1.0 - saturate(_HitFlash);
                float shock = (1.0 - smoothstep(0.006, 0.006 + aa, abs(radius - lerp(0.15, 0.485, hitProgress)))) * _HitFlash;
                float pulse = 0.86 + 0.14 * sin(_Time.y * 1.6);
                float energy = max(rim * 0.4, max(band * lerp(0.12, 0.88, healthArc), inner * 0.6));
                float alpha = saturate(max(energy, shock)) * _BaseColor.a;
                clip(alpha - 0.003);
                half3 color = lerp(_BaseColor.rgb, half3(1.0, 0.93, 0.8), saturate(_HitFlash * 0.85 + shock));
                color *= _Intensity * pulse * (1.0 + _HitFlash * 0.7);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
