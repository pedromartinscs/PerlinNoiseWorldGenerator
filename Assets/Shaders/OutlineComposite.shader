Shader "Hidden/Outline/Composite"
{
    Properties{
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _Thickness ("Thickness (px)", Range(1,6)) = 2
        _DepthThreshold ("Depth Threshold", Range(0.0001, 0.01)) = 0.002
        _DrawInnerLines ("Draw Lines Between Touching Objects", Float) = 1
        _ObjectIdTex ("Object ID Texture", 2D) = "white" {}
        _DepthTex ("Depth Texture", 2D) = "black" {}
        _DebugId ("Debug ID View", Float) = 0
    }
    SubShader
    {
        Tags{ "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Overlay+1000" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "OutlineComposite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_X(_ObjectIdTex); SAMPLER(sampler_ObjectIdTex);
            float4 _ObjectIdTex_TexelSize;

            TEXTURE2D_X(_DepthTex); SAMPLER(sampler_DepthTex);
            float4 _DepthTex_TexelSize;

            float4 _OutlineColor;
            float  _Thickness;
            float  _DepthThreshold;
            float  _DrawInnerLines;
            float  _DebugId;

            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert (uint id : SV_VertexID)
            {
                Varyings o;
                o.pos = GetFullScreenTriangleVertexPosition(id);
                o.uv  = GetFullScreenTriangleTexCoord(id);
                return o;
            }

            float SampleDepthLinear(float2 uv)
            {
                float raw = SAMPLE_TEXTURE2D_X(_DepthTex, sampler_DepthTex, uv).r;
                return LinearEyeDepth(raw, _ZBufferParams);
            }

            bool IsEdge(float2 uv)
            {
                half centerId   = SAMPLE_TEXTURE2D_X(_ObjectIdTex, sampler_ObjectIdTex, uv).r;
                float centerDep = SampleDepthLinear(uv);
                int t = (int)_Thickness;
                float2 texel = _ObjectIdTex_TexelSize.xy;

                // Debug: Show raw ID
                if (_DebugId > 0.5)
                    return centerId > 0.0h;

                // External edges: background pixel adjacent to any outlined object pixel
                if (centerId <= 0.0h)
                {
                    [unroll(12)]
                    for (int dy=-t; dy<=t; ++dy)
                    [unroll(12)]
                    for (int dx=-t; dx<=t; ++dx)
                    {
                        if (dx==0 && dy==0) continue;
                        half nId = SAMPLE_TEXTURE2D_X(_ObjectIdTex, sampler_ObjectIdTex, uv + float2(dx,dy)*texel).r;
                        if (nId > 0.0h)
                        {
                            float nDepth = SampleDepthLinear(uv + float2(dx,dy)*texel);
                            if (nDepth + _DepthThreshold <= centerDep) return true;
                        }
                    }
                    return false;
                }

                // Internal separation: ALWAYS outline when IDs differ
                if (_DrawInnerLines > 0.5)
                {
                    [unroll(12)]
                    for (int dy=-t; dy<=t; ++dy)
                    [unroll(12)]
                    for (int dx=-t; dx<=t; ++dx)
                    {
                        if (dx==0 && dy==0) continue;
                        half nId = SAMPLE_TEXTURE2D_X(_ObjectIdTex, sampler_ObjectIdTex, uv + float2(dx,dy)*texel).r;
                        if (nId > 0.0h && abs(nId - centerId) > 0.001h)
                        {
                            return true; // no depth check → per-object outlines
                        }
                    }
                }

                return false;
            }

            half4 Frag (Varyings i) : SV_Target
            {
                if (IsEdge(i.uv))
                    return _OutlineColor;
                return half4(0,0,0,0);
            }
            ENDHLSL
        }
    }
}
