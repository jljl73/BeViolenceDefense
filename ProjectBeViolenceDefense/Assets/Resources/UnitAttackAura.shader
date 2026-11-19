Shader "BeViolenceDefense/Unit Attack Aura"
{
    Properties
    {
        _AttackColor ("Attack Color", Color) = (1,.65,.53,1)
        _Strength ("Strength", Float) = 0
        _Phase ("Battle Phase", Float) = 0
        _AttackPulse ("Attack Pulse", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+6" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ColorMask RGB
            Cull Off
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _AttackColor;
                float _Strength, _Phase, _AttackPulse;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.positionWS=TransformObjectToWorld(input.positionOS);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.uv=input.uv;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float angle=input.uv.x*6.2831853;
                float y=input.uv.y;
                float facing=abs(dot(normalize(input.normalWS),GetWorldSpaceNormalizeViewDir(input.positionWS)));
                // Broad connected currents rise through the torso, with no separate sparks or hard bands.
                float current=.5+.5*sin(y*8-_Phase*2.5+sin(angle*3+_Phase*.35)*1.1);
                float crest=.68+.07*sin(angle*3+_Phase*.6);
                float ends=smoothstep(0,.19,y)*(1-smoothstep(crest,.98,y));
                float sides=smoothstep(0,.45,facing)*(.45+.55*pow(1-facing,.7));
                float density=ends*sides*(.45+.55*current)*_Strength;
                float pulse=saturate(_AttackPulse);
                half3 color=pow(_AttackColor.rgb,1.7);
                half3 light=color*density*(.70+pulse*.70);
                float alpha=density*(.55+pulse*.15);
                return half4(light,alpha);
            }
            ENDHLSL
        }
    }
}
