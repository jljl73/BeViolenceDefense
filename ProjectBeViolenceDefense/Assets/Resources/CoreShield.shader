Shader "BeViolenceDefense/Core Shield"
{
    Properties
    {
        _ShieldColor ("Shield Color", Color) = (0.72, 0.61, 1, 1)
        _ShieldAge ("Age From BattleClock", Float) = 0
        _ShieldCharge ("Charge", Range(0, 1)) = 0
        _ShieldBurst ("Break Flash", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+3" "RenderType" = "Transparent" }
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShieldColor;
                float _ShieldAge;
                half _ShieldCharge;
                half _ShieldBurst;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float height : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS);
                Varyings output;
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.height = input.positionOS.y;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half grazing = 1.0 - saturate(dot(normalize(input.normalWS), viewDir));
                half rim = pow(grazing, 2.4);
                half edge = pow(grazing, 7.0);
                float bandPhase = abs(frac(input.height * 1.8 - _ShieldAge * 0.25) - 0.5);
                half band = (1.0 - smoothstep(0.012, 0.080, bandPhase)) * (0.14 + rim * 0.86);

                half flash = _ShieldBurst * _ShieldBurst;
                half opacity = (0.04 + rim * 0.39 + edge * 0.36 + band * 0.16) * saturate(_ShieldCharge + flash);
                opacity += flash * (0.14 + rim * 0.20);
                half3 tint = lerp(_ShieldColor.rgb, half3(1, 1, 1), saturate(edge * 0.52 + flash * 0.64));
                return half4(tint, saturate(opacity));
            }
            ENDHLSL
        }
    }
}
