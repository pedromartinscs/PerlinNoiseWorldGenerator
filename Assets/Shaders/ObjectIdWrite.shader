Shader "Hidden/Outline/ObjectIdWrite"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-1" }
        ZWrite Off
        ZTest LEqual
        Cull Back
        Pass
        {
            Name "ObjectIdWrite"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _ObjectId01; // 0..1 from OutlineObjectId.cs

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings  { float4 positionHCS : SV_POSITION; };

            Varyings vert (Attributes IN)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // Write ID in R channel
                return half4(_ObjectId01, 0, 0, 1);
            }
            ENDHLSL
        }
    }
}
