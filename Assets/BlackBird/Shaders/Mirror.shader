// Old silvered glass in its frame: a pale grey that brightens at a glancing angle, with the fog over it. It is drawn
// pulled a hair toward the camera (Offset), because the glass sits on the frame's backing in the same plane and the
// two fought over every pixel (the Palace Hotel's mirrors flickered as you walked past).
Shader "BlackBird/Mirror"
{
    Properties
    {
        _BaseColor ("Glass", Color) = (0.46, 0.5, 0.52, 1)
        _SheenColor ("Sheen at a glancing angle", Color) = (0.85, 0.88, 0.9, 1)
        _Glow ("Self light", Range(0, 1)) = 0.15
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Offset -1, -1
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SheenColor;
                half _Glow;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; half fog : TEXCOORD2; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                half fres = pow(1 - saturate(dot(n, v)), 3);
                // the room's light, from the probes, so the glass is dark in a dark room and bright in a lit one
                half3 room = SampleSH(n) + SampleSH(reflect(-v, n)) * 0.5;
                half3 c = _BaseColor.rgb * (room + _Glow) + _SheenColor.rgb * fres * (room + 0.1);
                return half4(MixFog(c, i.fog), 1);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
