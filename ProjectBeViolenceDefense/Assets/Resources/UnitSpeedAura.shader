Shader "BeViolenceDefense/Unit Speed Aura"
{
    Properties
    {
        _SpeedColor ("Speed Color", Color) = (.55,.83,1,1)
        _Strength ("Strength", Float) = 0
        _Phase ("Battle Phase", Float) = 0
        _Motion ("Movement", Float) = 0
        _Height ("Body Height", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+7" "RenderType"="Transparent" }
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
                half4 _SpeedColor;
                float _Strength, _Phase, _Motion, _Height;
                float4 _Path[16];
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float strip:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                int index=(int)round(input.uv.x*15);
                float3 tangent=_Path[min(index+1,15)].xyz-_Path[max(index-1,0)].xyz;
                float3 side=float3(-tangent.z,0,tangent.x);
                side*=rsqrt(max(dot(side,side),.000001));
                float t=input.uv.x;
                float wave=sin(t*7-_Phase*3+input.positionOS.y)*.055*smoothstep(0,.25,t);
                float width=.13*(1-.55*t);
                float offset=input.positionOS.y*(.15+wave)+input.positionOS.x*width;
                float3 p=_Path[index].xyz+side*(offset*_Height);
                p.y+=sin(t*5-_Phase*2+input.positionOS.y)*.025*_Height;
                output.positionCS=TransformObjectToHClip(p);
                output.uv=input.uv;
                output.strip=input.positionOS.y;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float t=input.uv.x;
                float across=input.uv.y*2-1;
                float soft=exp2(-4*across*across)*(1-smoothstep(.65,1,abs(across)));
                float ends=smoothstep(0,.10,t)*(1-smoothstep(.58,1,t));
                // Bright crests travel continuously from the ankles toward the tail.
                float flow=.5+.5*sin(t*9-_Phase*8+input.strip*1.7);
                float density=soft*ends*(.55+.45*flow*flow)*_Strength;
                half3 color=pow(_SpeedColor.rgb,1.35);
                return half4(color*density*(.75+_Motion*.3),density*.38);
            }
            ENDHLSL
        }
    }
}
