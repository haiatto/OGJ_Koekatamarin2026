Shader "Koekatamarin/Sunset Ocean"
{
    Properties
    {
        _DeepColor("Deep water", Color) = (0.045, 0.022, 0.035, 1)
        _HorizonColor("Horizon", Color) = (0.38, 0.12, 0.055, 1)
        _SunColor("Sun reflection", Color) = (1, 0.48, 0.12, 1)
        _SunDirection("Sun direction", Vector) = (0, 0.09, 1, 0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "Ocean"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _HorizonColor;
                half4 _SunColor;
                float4 _SunDirection;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 plane : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 p = input.positionOS.xyz;
                output.plane = p.xz;
                p.y += sin(p.z * 1.3 + _Time.y * 0.8) * 0.025;
                output.positionWS = TransformObjectToWorld(p);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 p = input.plane;
                float t = _Time.y;
                float ripple = sin(p.y * 9.0 + sin(p.x * 2.7) * 1.8 - t * 1.5);
                float ripple2 = sin(p.x * 6.3 + p.y * 4.7 + t * 0.55);
                // Fade sub-pixel ripples to avoid shimmering in a moving headset.
                float waveFade = saturate(1.0 - max(length(ddx(p)), length(ddy(p))) * 3.0);
                ripple *= waveFade;
                ripple2 *= waveFade;
                float3 normalOS = normalize(float3(ripple2 * 0.10, 1, ripple * 0.08));
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
                float3 view = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(1.0 - saturate(dot(view, normalWS)), 3.0);
                float3 reflected = reflect(-view, normalWS);
                float shine = pow(saturate(dot(reflected, normalize(_SunDirection.xyz))), 48.0);
                float stripes = smoothstep(-0.4, 0.65, ripple) * 0.8 + 0.2;
                half3 color = lerp(_DeepColor.rgb, _HorizonColor.rgb, fresnel * 0.8);
                color += _SunColor.rgb * shine * stripes * 0.8;
                color *= 0.92 + ripple2 * 0.08;
                // A small foam edge at the shore; no depth textures or reflection cameras.
                float edge = 1.0 - smoothstep(0.0, 0.45, p.y - 7.0 + sin(p.x * 1.5 + t) * 0.10);
                color = lerp(color, half3(0.52, 0.39, 0.28), saturate(edge) * 0.55);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
