Shader "Custom/OctopathCommandFrame"
{
    Properties
    {
        _FrameTex ("Frame Texture", 2D) = "white" {}
        _MaskTex ("Mask Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _FrameTex;
            sampler2D _MaskTex;
            float4 _FrameTex_ST;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 pos : SV_POSITION; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _FrameTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 frame = tex2D(_FrameTex, i.uv);
                fixed4 mask = tex2D(_MaskTex, i.uv);
                // 用 Mask 图的 R 通道作为 Frame 图的 Alpha
                frame.a = mask.r +mask.g;
                return frame;
            }
            ENDCG
        }
    }
}