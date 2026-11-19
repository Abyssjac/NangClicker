Shader "NangClicker/Hex Spell Preview"
{
    Properties { _BaseColor("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half shade : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.shade = 0.45h + 0.55h * saturate(dot(normalWS, normalize(float3(0.3, 0.85, -0.4))));
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return half4(_BaseColor.rgb * input.shade, 1); }
            ENDHLSL
        }
    }
}
