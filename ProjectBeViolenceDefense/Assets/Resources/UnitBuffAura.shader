Shader "BeViolenceDefense/Unit Buff Aura"
{
    Properties
    {
        _AttackColor ("Attack", Color) = (1,1,1,1)
        _SpeedColor ("Speed", Color) = (1,1,1,1)
        _ArmorColor ("Armor", Color) = (1,1,1,1)
        _TauntColor ("Taunt", Color) = (1,1,1,1)
        _HealColor ("Heal", Color) = (1,1,1,1)
        _Buffs ("Active Buffs", Vector) = (0,0,0,0)
        _Phase ("Battle Phase", Float) = 0
        _Heal ("Heal", Float) = 0
        _Impact ("Shield Impact", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+4" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            ColorMask RGB
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _AttackColor, _SpeedColor, _ArmorColor, _TauntColor, _HealColor;
                float4 _Buffs;
                float _Phase, _Heal, _Impact;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS=TransformObjectToHClip(input.positionOS);
                output.uv=input.uv;
                return output;
            }
            float hash(float2 p)
            {
                float3 q=frac(float3(p.x,p.y,p.x)*.1031);
                q+=dot(q,q.yzx+33.33);
                return frac((q.x+q.y)*q.z);
            }
            float noise(float2 p)
            {
                float2 cell=floor(p), f=frac(p);
                f=f*f*(3-2*f);
                return lerp(lerp(hash(cell),hash(cell+float2(1,0)),f.x),
                    lerp(hash(cell+float2(0,1)),hash(cell+1),f.x),f.y);
            }
            // Jittered particles with independent size, brightness and lifetime.
            // Four nearby cells suffice for these short soft sparks; no texture or CPU emitter is needed.
            float particles(float2 p,float2 density,float2 stretch,float time,float seed)
            {
                float2 q=p*density;
                q+=float2(sin(p.y*3.6+time*.6+seed),sin(p.x*3.9-time*.4+seed))*.8;
                float2 cell=floor(q), direction=step(.5,frac(q))*2-1;
                float light=0;
                [unroll] for(int y=0;y<2;y++)
                [unroll] for(int x=0;x<2;x++)
                {
                    float2 id=cell+float2(x,y)*direction;
                    float random=hash(id+seed);
                    float2 jitter=float2(random,hash(id+seed+19.7));
                    float2 center=id+.03+jitter*.94;
                    float radius=lerp(.09,.38,pow(hash(id+seed+41.2),1.5));
                    float2 offset=(q-center)/(radius*stretch);
                    float d=dot(offset,offset);
                    float life=frac(time*lerp(.35,.8,random)+random*7);
                    float fade=smoothstep(0,.15,life)*(1-smoothstep(.65,1,life))*step(.22,hash(id+seed+71.3));
                    light+=(exp2(-3*d)*.65+saturate(1-d*3)*.8)*fade*lerp(.55,1.25,random);
                }
                return light;
            }
            float2 rotate(float2 p,float angle)
            {
                float s=sin(angle),c=cos(angle);
                return float2(c*p.x-s*p.y,s*p.x+c*p.y);
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.uv*2-1;
                float t=_Phase;
                half3 light=0;
                // Preserve the face and torso. Density lives around the silhouette and ankles.
                float sides=smoothstep(.22,.52,abs(p.x));
                float edge=1-smoothstep(.82,1,abs(p.x));
                float vertical=smoothstep(-.92,-.72,p.y)*(1-smoothstep(.65,.98,p.y));
                if(_Buffs.w>.001)
                {
                    float2 flow=rotate(p-float2(0,.20),-t*.32)+float2(0,-t*.18);
                    float mask=smoothstep(.24,.52,abs(p.x))*edge*vertical;
                    mask+=exp2(-pow(p.x/.56,2)-pow((p.y-.78)/.18,2))*.7;
                    float motes=particles(flow,float2(8,10),float2(1.1,1.25),t*.9,83.1);
                    motes+=particles(flow*1.27+3.1,float2(7,8),float2(.8,.8),t,92.6)*.55;
                    light+=_TauntColor.rgb*(motes*1.6+noise(flow*6)*.07)*mask*_Buffs.w;
                }
                if(_Heal>.001)
                {
                    float2 flow=p-float2(0,t*.6);
                    float mask=vertical*edge*lerp(.25,1,sides);
                    light+=_HealColor.rgb*particles(flow,float2(8,9),float2(.9,1.35),t,115.4)*mask*_Heal*1.6;
                }
                // Keep combined buffs colorful instead of bleaching the character white.
                return half4(min(light,half3(1.8,1.8,1.8)),0);
            }
            ENDHLSL
        }
    }
}
