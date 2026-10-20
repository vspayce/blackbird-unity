// Muybridge's 1878 panorama on the horizon. The photograph's sky is blank paper, so it is keyed out: the top of
// the strip and its bright sky fade away into the real sky, leaving the hills and the city standing against it.
// A little haze blends the city toward the sky colour so it sits back in the distance.
Shader "BlackBird/Panorama"
{
    Properties
    {
        _BaseMap ("Photograph", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1.05, 1.0, 0.92, 1)
        _Haze ("Haze colour", Color) = (0.78, 0.78, 0.76, 1)
        _HazeAmount ("Haze", Range(0, 1)) = 0.22
        _SkyFrom ("Sky fade starts (0 bottom, 1 top)", Range(0, 1)) = 0.64
        _SkyTo ("Sky fade ends", Range(0, 1)) = 0.76
        _KeyFrom ("Bright sky key from", Range(0, 1)) = 0.70
        _KeyTo ("Bright sky key to", Range(0, 1)) = 0.84
        _Bottom ("Bottom border", Range(0, 0.2)) = 0.04
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-100" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _Haze;
                half _HazeAmount, _SkyFrom, _SkyTo, _KeyFrom, _KeyTo, _Bottom;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                // where the strip's two ends meet the plates don't quite match: over the last 1% of the strip, fade
                // into the strip's first column so the join closes (and the texture is clamped, so no bright line)
                half edge = smoothstep(0.99, 1.0, i.uv.x);
                if (edge > 0) c = lerp(c, SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, float2(0.0005, i.uv.y)).rgb, edge);
                half lum = dot(c, half3(0.3, 0.59, 0.11));
                float v = i.uv.y;
                half a = 1 - smoothstep(_SkyFrom, _SkyTo, v);                                       // the paper sky above the hills
                a *= 1 - smoothstep(_KeyFrom, _KeyTo, lum) * smoothstep(_SkyFrom - 0.12, _SkyFrom, v); // bright sky between the peaks
                a *= smoothstep(0, _Bottom, v);                                                        // the mount's border
                half hz = _HazeAmount * (0.6 + 0.4 * smoothstep(0.3, _SkyFrom, v));                     // farther toward the horizon
                c = lerp(c * _BaseColor.rgb, _Haze.rgb, hz);
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
