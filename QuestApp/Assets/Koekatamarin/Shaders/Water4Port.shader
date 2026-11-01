// Port of FX-SimpleWater4.shader LOD 500 + WaterInclude.cginc from the preserved project.
// Original equations/parameters retained. URP scene colour/depth replace GrabPass;
// separate planar reflections serve the two XR eyes. No procedural substitute waves.
Shader "Koekatamarin/Water4 Port"
{
    Properties
    {
        _BumpMap("Original wave normal", 2D) = "bump" {}
        _ReflectionLeft("Left planar reflection", 2D) = "black" {}
        _ReflectionRight("Right planar reflection", 2D) = "black" {}
        _DistortParams("Bump, reflection, Fresnel power, bias", Vector) = (1,1,2,1.15)
        _InvFadeParemeter("Original edge parameters", Vector) = (.15,.15,.5,1)
        _BumpTiling("Bump tiling", Vector) = (1,1,-2,3)
        _BumpDirection("Bump direction", Vector) = (1,1,-1,1)
        _FresnelScale("Fresnel scale", Float) = .75
        _BaseColor("Original base RGBA", Vector) = (.54,.95,.99,.5)
        _ReflectionColor("Original reflection RGBA", Vector) = (.54,.95,.99,.5)
        _SpecularColor("Original specular RGBA", Vector) = (.72,.72,.72,1)
        _WorldLightDir("Specular light forward", Vector) = (0,.1,-.5,0)
        _Shininess("Shininess", Float) = 200
        _GerstnerIntensity("Gerstner normal intensity", Float) = 1
        _GAmplitude("Wave amplitude", Vector) = (.3,.35,.25,.25)
        _GFrequency("Wave frequency", Vector) = (1.3,1.35,1.25,1.25)
        _GSteepness("Wave steepness", Vector) = (1,1,1,1)
        _GSpeed("Wave speed", Vector) = (1.2,1.375,1.1,1.5)
        _GDirectionAB("Wave directions AB", Vector) = (.3,.85,.85,.25)
        _GDirectionCD("Wave directions CD", Vector) = (.1,.9,.5,.5)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "Water4 High"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_ReflectionLeft); SAMPLER(sampler_ReflectionLeft);
            TEXTURE2D(_ReflectionRight); SAMPLER(sampler_ReflectionRight);
            CBUFFER_START(UnityPerMaterial)
                float4 _DistortParams, _InvFadeParemeter, _BumpTiling, _BumpDirection;
                float4 _BaseColor, _ReflectionColor, _SpecularColor, _WorldLightDir;
                float4 _GAmplitude, _GFrequency, _GSteepness, _GSpeed, _GDirectionAB, _GDirectionCD;
                float _FresnelScale, _Shininess, _GerstnerIntensity;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 vertexNormal:TEXCOORD0;
                float3 viewVector:TEXCOORD1;
                float4 bumpCoords:TEXCOORD2;
                float eyeDepth:TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            float4 Phase(float2 xz)
            {
                return _GFrequency * float4(dot(_GDirectionAB.xy,xz), dot(_GDirectionAB.zw,xz),
                    dot(_GDirectionCD.xy,xz),dot(_GDirectionCD.zw,xz)) + _Time.yyyy * _GSpeed;
            }
            float3 GerstnerOffset4(float2 xz)
            {
                float4 ab = _GSteepness.xxyy * _GAmplitude.xxyy * _GDirectionAB;
                float4 cd = _GSteepness.zzww * _GAmplitude.zzww * _GDirectionCD;
                float4 phase = Phase(xz), c = cos(phase), s = sin(phase);
                return float3(dot(c,float4(ab.xz,cd.xz)),dot(s,_GAmplitude),dot(c,float4(ab.yw,cd.yw)));
            }
            float3 GerstnerNormal4(float2 xz)
            {
                float4 ab = _GFrequency.xxyy * _GAmplitude.xxyy * _GDirectionAB;
                float4 cd = _GFrequency.zzww * _GAmplitude.zzww * _GDirectionCD;
                float4 c = cos(Phase(xz));
                return normalize(float3(-dot(c,float4(ab.xz,cd.xz))*_GerstnerIntensity,
                    2.0,-dot(c,float4(ab.yw,cd.yw))*_GerstnerIntensity));
            }
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                // Original Tile transforms had uniform scale .5 (unity_Scale.w = 2).
                // The six original meshes are combined into one unit-scale renderer.
                // Combined mesh positions are the original scene's world positions.
                // Keep that coordinate frame when the VR scenery is placed at the headset.
                float3 offset = GerstnerOffset4(v.positionOS.xz * 2.0);
                o.vertexNormal = GerstnerNormal4(v.positionOS.xz * 2.0 + offset.xz);
                o.bumpCoords = (v.positionOS.xzxz + _Time.xxxx * _BumpDirection) * _BumpTiling;
                o.viewVector = world - GetCameraPositionWS();
                float3 displaced = TransformObjectToWorld(v.positionOS.xyz + offset * .5);
                o.positionCS = TransformWorldToHClip(displaced);
                o.eyeDepth = -TransformWorldToView(displaced).z;
                return o;
            }
            float3 OriginalGamma(float3 color)
            {
                #if defined(UNITY_COLORSPACE_GAMMA)
                    return color;
                #else
                    return LinearToSRGB(color);
                #endif
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Old DXT5nm used (A+ A-1,G+G-1). UnpackNormal gives the same
                // two-sample XY slopes for both desktop and Android normal encodings.
                float3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.bumpCoords.xy));
                float3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.bumpCoords.zw));
                float2 bump = (n1.xy+n2.xy)*.5;
                float3 normal = TransformObjectToWorldDir(normalize(i.vertexNormal + float3(bump.x,0,bump.y)*_DistortParams.x));
                float3 view = normalize(i.viewVector);
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float2 distorted = uv + normal.xz * _DistortParams.y * 10.0 / i.eyeDepth;
                float3 refracted = SampleSceneColor(distorted);
                float distortedDepth = LinearEyeDepth(SampleSceneDepth(distorted),_ZBufferParams);
                if (distortedDepth < i.eyeDepth) refracted = SampleSceneColor(uv);
                float3 reflected;
                #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                    if (unity_StereoEyeIndex == 1)
                        reflected = SAMPLE_TEXTURE2D(_ReflectionRight,sampler_ReflectionRight,distorted).rgb;
                    else
                #endif
                        reflected = SAMPLE_TEXTURE2D(_ReflectionLeft,sampler_ReflectionLeft,distorted).rgb;
                float3 h = normalize(_WorldLightDir.xyz + view);
                float spec = pow(max(0,dot(normal,-h)),_Shininess);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(uv),_ZBufferParams);
                float edge = saturate(_InvFadeParemeter.x * (sceneDepth-i.eyeDepth));
                normal.xz *= _FresnelScale;
                float facing = clamp(1-max(dot(-view,normal),0),0,1);
                float fresnel = saturate(_DistortParams.w + (1-_DistortParams.w)*pow(facing,_DistortParams.z));
                // Original project rendered in Gamma. Preserve its colour math inside the
                // linear Quest pipeline, instead of silently changing the original palette.
                float3 reflectionColor = lerp(OriginalGamma(reflected),_ReflectionColor.rgb,_ReflectionColor.a);
                float3 color = lerp(lerp(OriginalGamma(refracted),_BaseColor.rgb,_BaseColor.a),reflectionColor,fresnel);
                color += spec * _SpecularColor.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    color = SRGBToLinear(color);
                #endif
                return half4(color,edge);
            }
            ENDHLSL
        }
    }
}
