Shader "HoloMed/ButtonLight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color          ("Tint",            Color) = (1,1,1,1)

        _BaseColor      ("Base Color",      Color) = (0.710, 0.878, 0.969, 0.92)
        _TopColor       ("Top highlight",   Color) = (0.980, 0.996, 1.000, 0.95)
        _RimColor       ("Rim / edge glow", Color) = (0.000, 0.200, 0.650, 0.40)
        _RimPower       ("Rim sharpness",   Range(1,8))  = 4.0
        _BevelWidth     ("Bevel width",     Range(0,0.5))= 0.07
        _BevelBright    ("Bevel brightness",Range(0,2))  = 0.30
        _PressDepth     ("Press offset",    Range(0,1))  = 0.0
        _CornerRadius   ("Corner Radius px",Range(0,64)) = 12
        _RectSize       ("Rect Size px",    Vector)      = (200, 80, 0, 0)
        _Stencil        ("Stencil ID",      Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil { Ref [_Stencil] Comp Always Pass Replace }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t { float4 vertex:POSITION; float4 color:COLOR; float2 texcoord:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 worldPos:TEXCOORD1; };

            fixed4 _Color; fixed4 _BaseColor; fixed4 _TopColor; fixed4 _RimColor;
            float _RimPower; float _BevelWidth; float _BevelBright; float _PressDepth;
            float _CornerRadius; float4 _RectSize; float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.uv       = v.texcoord;
                o.color    = v.color * _Color;
                o.worldPos = v.vertex;
                return o;
            }

            float sdfRoundedBox(float2 q, float2 b, float r)
            {
                float2 d = abs(q) - b + r;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - r;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 rectPx   = max(_RectSize.xy, float2(1,1));
                float2 pixelPos = (uv - 0.5) * rectPx;
                float2 halfExt  = rectPx * 0.5;
                float  dist     = sdfRoundedBox(pixelPos, halfExt, _CornerRadius);
                float  mask     = 1.0 - smoothstep(-1.0, 0.5, dist);
                clip(mask - 0.001);

                float tY   = 1.0 - uv.y;
                tY         = saturate(tY - _PressDepth * 0.12);
                fixed4 col = lerp(_TopColor, _BaseColor, tY * tY);

                float edgeDist = -dist;
                float maxEdge  = min(halfExt.x, halfExt.y);
                float edge     = saturate(edgeDist / max(maxEdge * _BevelWidth, 0.001));
                float bevelDir = uv.x * 0.5 + (1.0 - uv.y) * 0.5;
                float bevel    = (1.0 - edge) * _BevelBright * lerp(0.4, 1.2, bevelDir);
                col.rgb       -= bevel;

                float rim = 1.0 - saturate(edge * _RimPower);
                col.rgb   = lerp(col.rgb, _RimColor.rgb, rim * _RimColor.a);

                col.rgb *= lerp(1.0, 0.82, _PressDepth);

                col.a = mask * _BaseColor.a * i.color.a
                      * UnityGet2DClipping(i.worldPos.xy, _ClipRect);
                return col;
            }
            ENDCG
        }
    }
}
