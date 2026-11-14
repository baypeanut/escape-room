Shader "Northwind/Containment Signal"
{
    Properties
    {
        _BaseColor ("Housing tint", Color) = (0.025, 0.045, 0.065, 1)
        _WarningColor ("Pending tint", Color) = (1, 0.5, 0.08, 1)
        _StableColor ("Contained tint", Color) = (0.1, 0.9, 0.7, 1)
        _Resolved ("Containment", Range(0, 1)) = 0
        _Urgency ("Urgency", Range(0, 1)) = 0
        _Phase ("Signal phase", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Containment"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _WarningColor;
                half4 _StableColor;
                half _Resolved;
                half _Urgency;
                float _Phase;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half rim = 1 - saturate(dot(normal, view));
                rim *= rim;
                float ring = abs(frac(input.positionWS.y * 17 - _Phase) - 0.5);
                half bands = 1 - smoothstep(0.12, 0.2, ring);
                float diagonal = abs(frac(dot(input.positionWS, float3(14, 11, 14)) + _Phase) - 0.5);
                half lattice = 1 - smoothstep(0.035, 0.075, diagonal);
                half pattern = lerp(max(bands, lattice * 0.55), bands * 0.55, _Resolved);
                half3 warning = lerp(_WarningColor.rgb, half3(1, 0.14, 0.065), _Urgency);
                half3 signal = lerp(warning, _StableColor.rgb, _Resolved);
                half shade = 0.45 + 0.55 * saturate(dot(normal, normalize(half3(-0.4, 0.8, -0.5))));
                half3 color = _BaseColor.rgb * shade + signal * (0.12 + pattern * 0.8 + rim * 0.25);
                return half4(MixFog(color, input.fogFactor), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
