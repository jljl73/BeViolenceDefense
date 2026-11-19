Shader "Hidden/BeViolenceDefense/Core Shield Glow"
{
    Properties
    {
        _MainTex ("Input", 2D) = "black" {}
        _TileTex ("Gauge Tile", 2D) = "white" {}
        _GaugeMask ("Gauge Mask", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        ENDCG

        Pass
        {
            Name "ComposeGauge"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            sampler2D _TileTex;
            float4 _TileRect;
            float4 _TileUv;
            float4 _GaugeRect;
            half _UseFillColor;

            fixed4 Frag(v2f_img input) : SV_Target
            {
                float2 tile = (input.uv - _TileRect.xy) / _TileRect.zw;
                float2 gauge = (input.uv - _GaugeRect.xy) / _GaugeRect.zw;
                float2 inside = step(0.0, tile) * step(tile, 1.0) * step(0.0, gauge) * step(gauge, 1.0);
                fixed4 art = tex2D(_TileTex, _TileUv.xy + saturate(tile) * _TileUv.zw);
                // The original tiles include an opaque dark track around the gold fill.
                half3 color = IsGammaSpace() ? art.rgb : LinearToGammaSpace(art.rgb);
                half fill = smoothstep(0.34, 0.66, color.r) * smoothstep(0.20, 0.29, color.r - color.b);
                half tileAlpha = art.a * lerp(1.0, fill, _UseFillColor);
                fixed coverage = max(tex2D(_MainTex, input.uv).r, tileAlpha * inside.x * inside.y);
                return fixed4(coverage, 0, 0, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "SoftenGauge"
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment Frag
            float2 _BlurStep;

            fixed4 Frag(v2f_img input) : SV_Target
            {
                // Bilinear samples combine adjacent taps across the wide blur radius.
                float coverage = tex2D(_MainTex, input.uv).r;
                float totalWeight = 1.0;
                [unroll]
                for (int tap = 1; tap < 16; tap += 2)
                {
                    float nearWeight = exp2(-0.0125 * tap * tap);
                    float farWeight = exp2(-0.0125 * (tap + 1) * (tap + 1));
                    float weight = nearWeight + farWeight;
                    float2 offset = _BlurStep * (tap + farWeight / weight);
                    coverage += (tex2D(_MainTex, input.uv + offset).r + tex2D(_MainTex, input.uv - offset).r) * weight;
                    totalWeight += 2.0 * weight;
                }
                return half4(coverage / totalWeight, 0, 0, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "ShieldOverlay"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment Frag
            sampler2D _GaugeMask;

            fixed4 Frag(v2f_img input) : SV_Target
            {
                half blurred = tex2D(_MainTex, input.uv).r;
                half shape = tex2D(_GaugeMask, input.uv).r;
                half halo = saturate(blurred * 0.96);
                half opacity = lerp(halo, 0.21, shape);
                return half4(1, 1, 1, opacity);
            }
            ENDCG
        }
    }
}
