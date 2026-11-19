Shader "BeViolenceDefense/Unit Shield Surface"
{
    Properties
    {
        _ShieldColor ("Shield Color", Color) = (0.77,0.65,1,1)
        _Strength ("Strength", Float) = 0
        _Phase ("Battle Phase", Float) = 0
        _Impact ("Impact", Float) = 0
        _HitPoint ("Impact Point and Radius", Vector) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent" }
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
                half4 _ShieldColor;
                float _Strength, _Phase, _Impact;
                float4 _HitPoint;
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
                half3 normal=normalize(input.normalWS);
                half3 view=GetWorldSpaceNormalizeViewDir(input.positionWS);
                float facing=abs(dot(normal,view));
                // No crisp silhouette or cap: a broad, translucent veil surrounds the body.
                float ends=smoothstep(0,.23,input.uv.y)*(1-smoothstep(.65,1,input.uv.y));
                float softEdge=smoothstep(0,.48,facing);
                float angle=input.uv.x*6.2831853;
                float current=.5+.5*sin(input.uv.y*7-_Phase*1.25+sin(angle*2+_Phase*.23)*.8);
                float breath=.93+.07*sin(_Phase*1.7);
                float density=ends*softEdge*(.55+.45*pow(1-facing,.65))*breath*_Strength;
                float distanceFromHit=distance(input.positionWS,_HitPoint.xyz)/max(.01,_HitPoint.w);
                float spread=lerp(.10,1.05,1-_Impact);
                float impact=exp2(-5*pow((distanceFromHit-spread)/.38,2))*_Impact;
                half3 color=pow(_ShieldColor.rgb,1.35);
                float alpha=density*(.22+current*.12+impact*.14);
                half3 light=color*density*(.38+current*.35)+lerp(color,half3(1,1,1),.35)*impact*density*.8;
                return half4(light,alpha);
            }
            ENDHLSL
        }
    }
}
