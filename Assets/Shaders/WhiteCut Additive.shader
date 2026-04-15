Shader "UI/WhiteCutAdditiveTMPCompatible"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0,0.5,1,1)

        _WhiteThreshold ("White Threshold", Range(0, 1)) = 0.9
        _WhiteSoftness ("White Softness", Range(0.001, 0.5)) = 0.08
        _Intensity ("Intensity", Range(0, 5)) = 1

        _Stencil("Stencil ID", Float) = 0
        _StencilComp("Stencil Comparison", Float) = 8
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255
        _ColorMask("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
        }

        Cull Off
        ZWrite Off
        Blend One One
        ColorMask [_ColorMask]

        Pass
        {
            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _WhiteThreshold;
            float _WhiteSoftness;
            float _Intensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed3 tinted = tex.rgb * _Color.rgb * i.color.rgb;

                float luma = dot(tex.rgb, float3(0.299, 0.587, 0.114));
                float keep = 1.0 - smoothstep(_WhiteThreshold - _WhiteSoftness, _WhiteThreshold + _WhiteSoftness, luma);

                fixed3 additive = tinted * keep * _Intensity;
                return fixed4(additive, 1);
            }
            ENDCG
        }
    }
}
