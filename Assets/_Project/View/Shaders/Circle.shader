// Flat anti-aliased circle on a unit quad, colour per instance.
// Drawn with Graphics.DrawMeshInstanced; the whole world is a handful of draw calls.
// Built-in pipeline only (the project has no render pipeline package, see docs/PROGRESS.md "No URP").
Shader "WormRush/Circle"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Circle"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 disc : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = UnityObjectToClipPos(float4(input.positionOS, 1.0));
                output.disc = input.uv * 2.0 - 1.0;
                return output;
            }

            fixed4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float4 color = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float d = length(input.disc);
                float edge = max(fwidth(d), 1e-4);
                color.a *= saturate((1.0 - d) / edge);
                return fixed4(color);
            }
            ENDCG
        }
    }
}
