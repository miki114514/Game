Shader "UI/RoundedRect"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Sprite Texture", 2D) = "white"{ }
        _Radius ("Corner Radius", Range(0, 0.5)) = 0.15
        _OutlineWidth ("Outline Width", Range(0, 0.1)) = 0.02
        _OutlineColor ("Outline Color", Color) = (0.7, 0.9, 1, 1)

        [HideInInspector]_StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector]_Stencil ("Stencil ID", Float) = 0
        [HideInInspector]_StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector]_StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector]_StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector]_ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _Radius;
            float _OutlineWidth;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            float roundedBoxSDF(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 p = uv - 0.5;

                float2 halfSize = float2(0.5, 0.5);
                float dist = roundedBoxSDF(p, halfSize, _Radius);

                float aa = fwidth(dist) * 1.5;

                float fillAlpha = 1.0 - smoothstep(0.0, aa, dist);
                float outlineAlpha = 1.0 - smoothstep(_OutlineWidth, _OutlineWidth + aa, abs(dist));

                fixed4 tex = tex2D(_MainTex, uv);
                fixed4 fillCol = tex * i.color;
                fixed4 finalCol = lerp(fillCol, _OutlineColor, outlineAlpha * _OutlineColor.a);

                finalCol.a *= fillAlpha;

                return finalCol;
            }
            ENDCG
        }
    }
}
