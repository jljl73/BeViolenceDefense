// 그림책 책장 넘김 (UI). _MainTex 는 넘어가는 쪽 페이지 그림이다.
// 페이지가 오른쪽 아래 모서리부터 원통에 감기듯 말려 올라가 왼쪽으로 넘어가고, 넘어간 자리는 투명해져 아래 페이지가 보인다.
// 말린 부분의 안쪽은 종이 뒷면(그림이 희미하게 비친다), 바깥쪽은 그림이 감겨 어두워진다. 말린 곳 앞쪽에는 그림자가 진다.
// _Wear 를 올리면 낡은 그림책처럼 가장자리가 그을리고 얼룩이 지며, _Gutter 는 가운데 책등 그림자를 만든다.
// 그림 색에는 Graphic 의 색을 곱한다(그림 없이 색만 주면 그 색의 종이·표지가 된다).
// 좌표 단위는 RectTransform 의 세로 길이 = 1 (가로는 _Aspect 배).
Shader "BeViolenceDefense/UI/Page Curl"
{
    Properties
    {
        [PerRendererData] _MainTex ("Page", 2D) = "white" {}
        _Progress ("Progress", Range(0, 1)) = 0
        _Angle ("Fold angle (deg, 0 = vertical fold)", Range(-60, 60)) = -20
        _Radius ("Curl radius (share of page height)", Range(0.02, 0.4)) = 0.12
        _Aspect ("Page width / height", Float) = 1.7778
        _Paper ("Paper (back of the page)", Color) = (0.98, 0.95, 0.88, 1)
        _PaperLine ("Paper grain", Color) = (0.80, 0.70, 0.56, 1)
        _ShowThrough ("Picture showing through the paper", Range(0, 0.5)) = 0.14
        _Shadow ("Shadow", Range(0, 1)) = 0.5
        _Wear ("Worn edges and stains", Range(0, 1)) = 0
        _Gutter ("Spine shadow down the middle", Range(0, 1)) = 0
        _InkDark ("Burnt edge ink", Color) = (0.20, 0.13, 0.08, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "False" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct Attributes { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            sampler2D _MainTex;
            float _Progress, _Angle, _Radius, _Aspect, _ShowThrough, _Shadow, _Wear, _Gutter;
            float4 _Paper, _PaperLine, _InkDark;

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(41.3, 289.1))) * 15731.743); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            float Fbm(float2 p) { return Noise(p) * 0.5 + Noise(p * 2.03) * 0.3 + Noise(p * 4.1) * 0.2; }

            // 낡은 종이: 얼룩, 그을린 가장자리, 넓은 비네트, 가운데 책등 그림자. 앞뒤 면에 함께 쓴다.
            float3 Worn(float3 col, float2 uv, bool front)
            {
                float2 p = uv * float2(_Aspect, 1);
                float stain = smoothstep(0.55, 0.85, Fbm(p * 3.0 + 7.3));
                float fox = smoothstep(0.93, 1.0, Noise(p * 38.0)) * Noise(p * 5.0);
                col *= 1 - _Wear * (0.14 * stain + 0.3 * fox);
                float2 e = min(uv, 1 - uv) * float2(_Aspect, 1);
                float edge = min(e.x, e.y) + (Fbm(p * 9.0) - 0.5) * 0.05;
                col = lerp(col, _InkDark.rgb * 0.8, _Wear * (1 - smoothstep(0.0, 0.12, edge)) * 0.85);
                col *= 1 - _Wear * 0.22 * (1 - smoothstep(0.0, 0.42, edge));
                if (front)
                {
                    float g = abs(uv.x - 0.5) * _Aspect;
                    col *= 1 - _Gutter * (0.42 * exp(-g * 24.0) + 0.45 * exp(-g * 380.0));
                }
                return col;
            }

            float3 Front(float2 uv) { return Worn(tex2D(_MainTex, uv).rgb, uv, true); }

            bool OnPage(float2 uv) { return uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1; }

            float3 Back(float2 uv)
            {
                // 종이 결과 뒤로 비치는 그림(뒤에서 보므로 좌우가 뒤집힌다)
                float grain = Noise(uv * float2(_Aspect * 9, 160)) * 0.6 + Noise(uv * float2(_Aspect * 4, 40)) * 0.4;
                float3 col = lerp(_Paper.rgb, _PaperLine.rgb, grain * 0.18);
                float3 pic = tex2D(_MainTex, float2(1 - uv.x, uv.y)).rgb;
                col = lerp(col, col * (0.6 + 0.5 * dot(pic, float3(0.3, 0.59, 0.11))), _ShowThrough);
                return Worn(col * lerp(1.0, 0.94, _Wear), uv, false);
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float2 q = i.uv * float2(_Aspect, 1);
                float a = radians(_Angle);
                float2 n = float2(cos(a), sin(a));          // 말려 올라가는 쪽(페이지 오른쪽)을 가리킨다
                float R = _Radius;

                // 접는 선은 페이지 오른쪽 끝 너머에서 출발해, 말린 원통까지 왼쪽 끝을 지나갈 때까지 움직인다.
                float c0 = dot(float2(0, 0), n), c1 = dot(float2(_Aspect, 0), n), c2 = dot(float2(0, 1), n), c3 = dot(float2(_Aspect, 1), n);
                float hi = max(max(c0, c1), max(c2, c3)), lo = min(min(c0, c1), min(c2, c3));
                float t = _Progress;
                float s = lerp(hi + 0.01, lo - 3.1416 * R - 0.02, t);
                float d = dot(q, n) - s;                     // 접는 선에서 말리는 쪽으로의 거리

                // 1) 원통 위쪽 면 = 종이 뒷면 (말린 끝이 다시 앞으로 덮이는 부분까지)
                if (d <= R)
                {
                    float arc = d >= 0 ? R * (3.1416 - asin(d / R)) : 3.1416 * R - d;
                    float2 uv = (q + (arc - d) * n) / float2(_Aspect, 1);
                    if (OnPage(uv))
                    {
                        float shade = 1;
                        if (d >= 0)
                        {
                            float c = sqrt(saturate(1 - (d / R) * (d / R)));
                            shade = lerp(0.74, 1.04, c);
                        }
                        else
                            shade = lerp(0.88, 1.0, saturate(-d / (R * 2.5)));
                        return float4(Back(uv) * shade * i.color.rgb, i.color.a);
                    }
                }
                // 2) 원통 아래쪽 면 = 그림이 감겨 넘어가는 부분
                if (d >= 0 && d <= R)
                {
                    float arc = R * asin(d / R);
                    float2 uv = (q + (arc - d) * n) / float2(_Aspect, 1);
                    if (OnPage(uv))
                    {
                        float c = sqrt(saturate(1 - (d / R) * (d / R)));
                        return float4(Front(uv) * lerp(0.5, 1.0, c) * i.color.rgb, i.color.a);
                    }
                }
                // 3) 아직 평평한 페이지
                if (d < 0)
                {
                    float sh = _Shadow * 0.45 * smoothstep(-R * 1.4, 0, d);
                    return float4(Front(i.uv) * (1 - sh) * i.color.rgb, i.color.a);
                }
                // 4) 넘어간 자리: 아래 페이지 위에 말린 종이의 그림자만
                float shadow = _Shadow * smoothstep(R * 2.6, R, d);
                return float4(0, 0, 0, shadow * i.color.a);
            }
            ENDCG
        }
    }
}
