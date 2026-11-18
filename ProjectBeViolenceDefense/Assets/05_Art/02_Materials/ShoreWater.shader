// 해안 물 셰이더 (URP, 조명 없음).
// 메시의 uv.x = 해안선에서 바다 쪽으로의 거리(육지 쪽은 음수), uv.y = 해안선을 따라간 거리.
// 얕은 물에서 깊은 물로 색이 바뀌고, 물가가 밀려왔다 빠지며 거품 줄이 해안 쪽으로 다가온다. 육지 쪽은 젖은 모래로 어둡게 물든다.
Shader "BeViolenceDefense/Shore Water"
{
    Properties
    {
        _Shallow ("Shallow Color", Color) = (0.36, 0.86, 0.82, 1)
        _Deep ("Deep Color", Color) = (0.13, 0.55, 0.72, 1)
        _Foam ("Foam Color", Color) = (0.97, 0.99, 0.97, 1)
        _WetSand ("Wet Sand Color", Color) = (0.55, 0.45, 0.30, 0.45)
        _DeepDistance ("Deep Distance", Float) = 9
        _LapDistance ("Lap Distance", Float) = 0.5
        _LapSpeed ("Lap Speed", Float) = 0.9
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent-10" }

        Pass
        {
            Name "ShoreWater"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow;
                half4 _Deep;
                half4 _Foam;
                half4 _WetSand;
                float _DeepDistance;
                float _LapDistance;
                float _LapSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float along = input.uv.y;
                // 물가가 해안선을 따라 조금씩 다른 박자로 밀려왔다 빠진다.
                float lap = sin(_Time.y * _LapSpeed + along * 0.23) * _LapDistance;
                float d = input.uv.x - lap;

                if (d < 0.0)
                {
                    // 육지: 물이 닿았던 자리만 젖은 모래색으로 어둡게.
                    float reach = input.uv.x + _LapDistance;     // 물이 가장 멀리 올라오는 선 기준
                    half wet = smoothstep(-2.2, 0.0, reach) * _WetSand.a;
                    half3 sand = MixFog(_WetSand.rgb, input.fogFactor);
                    return half4(sand, wet);
                }

                half3 water = lerp(_Shallow.rgb, _Deep.rgb, smoothstep(0.0, _DeepDistance, d));
                // 물가의 거품 띠
                half edge = 1.0h - smoothstep(0.0, 0.5, d);
                // 해안으로 다가오는 거품 줄: 물가에 가까울수록 진하고 멀수록 사라진다.
                float phase = frac(d * 0.2 + _Time.y * 0.11 + sin(along * 0.31) * 0.12);
                half lines = smoothstep(0.90, 0.96, phase) * (1.0h - smoothstep(1.5, 9.0, d));
                half foam = saturate(edge + lines * 0.75h);
                water = lerp(water, _Foam.rgb, foam);
                // 아주 얕은 곳은 모래가 살짝 비친다.
                half alpha = lerp(0.72h, 1.0h, smoothstep(0.0, 2.5, d));
                alpha = max(alpha, foam);
                return half4(MixFog(water, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }
}
