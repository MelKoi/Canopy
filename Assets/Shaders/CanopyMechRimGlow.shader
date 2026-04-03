Shader "Custom/CanopyMechRimGlow"
{
    Properties
    {
        [HDR] _RimColor ("Rim Color", Color) = (0.35, 0.55, 0.85, 1)
        _RimPower ("Rim Sharpness", Range(0.5, 16)) = 5
        _Intensity ("Intensity", Range(0, 2)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+120"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "MechRimGlow"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _RimColor;
                float _RimPower;
                float _Intensity;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs vi = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs ni = GetVertexNormalInputs(v.normalOS);

                o.positionCS = vi.positionCS;
                o.positionWS = vi.positionWS;
                o.normalWS = ni.normalWS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 N = normalize(i.normalWS);
                float ndv = saturate(dot(N, V));
                float rim = pow(saturate(1.0h - ndv), _RimPower);
                half3 rgb = _RimColor.rgb * rim * _Intensity;
                return half4(rgb, rim * _Intensity);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
