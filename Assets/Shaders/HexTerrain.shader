Shader "NangClicker/Hex Terrain"
{
    Properties
    {
        _TopColor("Top", Color) = (0.36,0.5,0.25,1)
        _SoilColor("Soil", Color) = (0.34,0.25,0.17,1)
        _SideColor("Rock", Color) = (0.32,0.3,0.29,1)
        _SoilThickness("Soil thickness", Range(0,1)) = 0.18
        _PatternScale("Facet density", Range(1,20)) = 5
        _Variation("Facet variation", Range(0,0.5)) = 0.12
        _Water("Water facets", Range(0,1)) = 0
        [PerRendererData] _TileSeed("Tile seed", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _TopColor, _SoilColor, _SideColor;
            float _SoilThickness, _PatternScale, _Variation, _Water, _TileSeed;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "Terrain"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float depth:TEXCOORD3; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv; o.depth = 1.5-v.positionOS.y;
                return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p = i.uv * _PatternScale;
                float2 tile = floor(p); float facetSide = step(1, frac(p.x)+frac(p.y));
                float seed = frac(UNITY_MATRIX_M._m03 * 0.137 + UNITY_MATRIX_M._m23 * 0.271);
                float noise = Hash(tile + float2(facetSide*0.37+seed,_TileSeed+seed));
                float top = step(0.65,normalize(i.normalWS).y);
                half3 side = lerp(_SideColor.rgb,_SoilColor.rgb,1-step(_SoilThickness,i.depth));
                half3 color = lerp(side,_TopColor.rgb,top);
                color *= 1 + (noise*2-1)*_Variation;
                color += top*_Water*pow(saturate(noise),8)*0.13;
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half diffuse = saturate(dot(normalize(i.normalWS),light.direction));
                half3 illumination = 0.42h + light.color*(0.58h*diffuse*lerp(0.55h,1.0h,light.shadowAttenuation));
                return half4(color*illumination,1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            struct A { float4 p:POSITION; };
            float4 DepthVert(A i):SV_POSITION { return TransformObjectToHClip(i.p.xyz); }
            half4 DepthFrag():SV_Target { return 0; }
            ENDHLSL
        }
    }
}
