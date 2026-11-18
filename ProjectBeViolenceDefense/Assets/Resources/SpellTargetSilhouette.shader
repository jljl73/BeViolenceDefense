Shader "BeViolenceDefense/Spell Target Silhouette"
{
    Properties
    {
        _SourceMap ("Source Texture", 2D) = "white" {}
        _SourceColor ("Source Color", Color) = (1, 1, 1, 1)
        _SilhouetteColor ("Silhouette Color", Color) = (1, 1, 1, 1)
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+101" "RenderType" = "Opaque" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_SourceMap);
        SAMPLER(sampler_SourceMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _SourceMap_ST;
            half4 _SourceColor;
            half4 _SilhouetteColor;
            half4 _OutlineColor;
        CBUFFER_END

        struct Attributes
        {
            float3 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
        };
        ENDHLSL

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float4 positionCS = TransformObjectToHClip(input.positionOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 normalCS = mul(GetWorldToHClipMatrix(), float4(normalWS, 0.0));
                float2 screenSize = GetScaledScreenParams().xy;
                float2 projected = (normalCS.xy * positionCS.w - positionCS.xy * normalCS.w) * screenSize;
                float2 direction = projected * rsqrt(max(dot(projected, projected), 1e-6));
                float width = max(2.4, screenSize.y / 300.0);
                positionCS.xy += direction * (2.0 * width / screenSize) * positionCS.w;
                output.positionCS = positionCS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Hatching"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct SurfaceVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            SurfaceVaryings Vert(Attributes input)
            {
                SurfaceVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _SourceMap);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(SurfaceVaryings input) : SV_Target
            {
                half3 detail = SAMPLE_TEXTURE2D(_SourceMap, sampler_SourceMap, input.uv).rgb * _SourceColor.rgb;
                half lighting = saturate(dot(normalize(input.normalWS), GetMainLight().direction));
                detail *= lerp(0.68h, 1.0h, lighting);

                float pitch = max(7.5, GetScaledScreenParams().y / 80.0);
                float lane = dot(input.positionCS.xy, float2(1.0, 0.62)) / pitch;
                float distanceFromLine = abs(frac(lane) - 0.5);
                float feather = fwidth(lane) * 0.5;
                half hatch = 1.0h - smoothstep(0.12, 0.12 + feather, distanceFromLine);

                // Keep the stripe edges fixed and send a highlight down each line.
                float alongStripe = dot(input.positionCS.xy, float2(-0.62, 1.0)) / (pitch * 4.8);
                float phase = alongStripe + floor(lane) * 0.37 - _Time.y * 1.10;
                half glint = 0.5h + 0.5h * cos(TWO_PI * phase);
                half highlight = glint * glint;
                half intensity = 0.51h + 0.15h * highlight;
                half3 surface = lerp(detail, _SilhouetteColor.rgb, 0.10h + intensity * hatch);
                surface += _SilhouetteColor.rgb * (0.12h * highlight * hatch);
                return half4(surface, 1.0h);
            }
            ENDHLSL
        }
    }
}
