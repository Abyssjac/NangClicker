Shader "MonaLisa/Pupil Highlight"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0
        _CoreColor ("Core Color", Color) = (0.012, 0.018, 0.055, 1)
        _IrisColor ("Iris Color", Color) = (0.04, 0.28, 0.54, 1)
        [HDR] _RimColor ("Neon Rim Color", Color) = (0.02, 1.15, 1.6, 1)
        [HDR] _GlintColor ("Glint Color", Color) = (0.82, 0.98, 1, 1)
        _PulseSpeed ("Pulse Speed", Range(0, 8)) = 3.2
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.26

        // Legacy SpriteRenderer properties retained for batching, atlases, and renderer tint support.
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex PupilVertex
            #pragma fragment PupilFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                half4 _IrisColor;
                half4 _RimColor;
                half4 _GlintColor;
                float _PulseSpeed;
                float _PulseStrength;
            CBUFFER_END

            Varyings PupilVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 PupilFragment(Varyings input) : SV_Target
            {
                half4 sprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float radialDistance = distance(input.uv, float2(0.5, 0.5));
                float irisMask = 1.0 - smoothstep(0.29, 0.47, radialDistance);
                float coreMask = 1.0 - smoothstep(0.12, 0.20, radialDistance);
                float neonRimMask = smoothstep(0.30, 0.34, radialDistance) * (1.0 - smoothstep(0.42, 0.47, radialDistance));
                float innerRingMask = smoothstep(0.20, 0.24, radialDistance) * (1.0 - smoothstep(0.25, 0.30, radialDistance));
                float glintMask = 1.0 - smoothstep(0.035, 0.10, distance(input.uv, float2(0.36, 0.67)));
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseStrength;

                float3 pupilColor = lerp(_IrisColor.rgb * irisMask, _CoreColor.rgb, coreMask);
                pupilColor += _RimColor.rgb * (neonRimMask * pulse + innerRingMask * 0.55);
                pupilColor += _GlintColor.rgb * glintMask;

                half alpha = sprite.a * input.color.a;
                return half4(pupilColor * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
