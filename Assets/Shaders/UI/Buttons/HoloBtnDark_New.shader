Shader "HoloMed/ButtonDark"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color          ("Tint",            Color) = (1,1,1,1)

        _BaseColor      ("Base Color",      Color) = (0.039, 0.059, 0.118, 0.82)
        _TopColor       ("Top highlight",   Color) = (0.102, 0.180, 0.353, 0.85)
        _RimColor       ("Rim / edge glow", Color) = (0.000, 0.784, 0.941, 0.55)
        _RimPower       ("Rim sharpness",   Range(1,8))  = 3.5
        _BevelWidth     ("Bevel width",     Range(0,0.5))= 0.08
        _BevelBright    ("Bevel brightness",Range(0,2))  = 0.55
        _PressDepth     ("Press offset",    Range(0,1))  = 0.0
        _CornerRadius   ("Corner Radius px",Range(0,64)) = 12
        _RectSize       ("Rect Size px",    Vector)      = (200, 60, 0, 0)
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

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
            };

            fixed4 _Color;
            fixed4 _BaseColor;
            fixed4 _TopColor;
            fixed4 _RimColor;
            float  _RimPower;
            float  _BevelWidth;
            float  _BevelBright;
            float  _PressDepth;
            float  _CornerRadius;
            float4 _RectSize;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.uv       = v.texcoord;     // raw UV from the quad — 0..1 across the rect
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

                // SDF rounded corners in pixel space
                float2 rectPx   = max(_RectSize.xy, float2(1,1));
                float2 pixelPos = (uv - 0.5) * rectPx;
                float2 halfExt  = rectPx * 0.5;
                float  dist     = sdfRoundedBox(pixelPos, halfExt, _CornerRadius);
                float  mask     = 1.0 - smoothstep(-1.0, 0.5, dist);
                clip(mask - 0.001);

                // vertical gradient for depth
                float tY   = 1.0 - uv.y;
                tY         = saturate(tY - _PressDepth * 0.12);
                fixed4 col = lerp(_TopColor, _BaseColor, tY * tY);

                // bevel (follows the rounded edge via SDF)
                float edgeDist = -dist;
                float maxEdge  = min(halfExt.x, halfExt.y);
                float edge     = saturate(edgeDist / max(maxEdge * _BevelWidth, 0.001));
                float bevelDir = uv.x * 0.5 + (1.0 - uv.y) * 0.5;
                float bevel    = (1.0 - edge) * _BevelBright * lerp(1.2, 0.5, bevelDir);
                col.rgb       += bevel;

                // rim glow
                float rim = 1.0 - saturate(edge * _RimPower);
                col.rgb   = lerp(col.rgb, _RimColor.rgb, rim * _RimColor.a);

                // press darken
                col.rgb *= lerp(1.0, 0.78, _PressDepth);

                // NOTE: texture is intentionally ignored — text/icon is a separate TMP child.
                col.a = mask * _BaseColor.a * i.color.a
                      * UnityGet2DClipping(i.worldPos.xy, _ClipRect);

                return col;
            }
            ENDCG
        }
    }
}
