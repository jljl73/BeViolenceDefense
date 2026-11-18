Shader "BeViolenceDefense/Spell Area Burst"
{
    Properties
    {
        _BurstColor ("Skill Color", Color) = (1, 1, 1, 1)
        _BurstAge ("Age From BattleClock", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+2" "RenderType" = "Transparent" }
        Pass
        {
            Name "AreaFlash"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
            ColorMask RGB
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BurstColor;
                float _BurstAge;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv * 2.0 - 1.0) * 1.08;
                float radius = length(p);
                float age = max(0.0, _BurstAge);
                // Full-area flash first, then an expanding colored rim and a softer echo.
                float flash = pow(saturate(1.0 - age / 0.12), 2.0);
                float disk = 1.0 - smoothstep(0.60, 1.0, radius);
                float fill = disk * pow(saturate(1.0 - age / 0.24), 2.0);
                float travel = 1.0 - pow(1.0 - saturate(age / 0.24), 3.0);
                float waveRadius = lerp(0.12, 1.0, travel);
                float wave = (1.0 - smoothstep(0.015, 0.10, abs(radius - waveRadius)))
                    * (1.0 - smoothstep(0.08, 0.55, age));
                float echoRadius = lerp(0.08, 0.96, saturate((age - 0.04) / 0.29));
                float echo = (1.0 - smoothstep(0.01, 0.045, abs(radius - echoRadius)))
                    * smoothstep(0.03, 0.08, age) * (1.0 - smoothstep(0.13, 0.46, age));

                // Twelve short rays travel with the shockwave. No textures or wall-clock animation.
                float angle = atan2(p.y, p.x + 0.00001);
                float rays = pow(saturate(cos(angle * 12.0)), 28.0)
                    * (1.0 - smoothstep(0.04, 0.24, abs(radius - waveRadius)))
                    * (1.0 - smoothstep(0.06, 0.32, age)) * smoothstep(0.12, 0.3, radius);
                float core = exp(-radius * radius * 20.0) * flash;
                half3 color = _BurstColor.rgb;
                half3 light = color * (fill * 0.95 + wave * 1.25 + echo * 0.5 + rays * 1.4);
                light += lerp(color, half3(1, 1, 1), 0.65) * (flash * disk * 0.55 + core * 1.6);
                light *= 1.0 - smoothstep(1.0, 1.08, radius);
                return half4(light, 0);
            }
            ENDHLSL
        }
    }
}
