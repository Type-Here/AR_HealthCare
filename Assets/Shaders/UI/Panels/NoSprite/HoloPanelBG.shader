Shader "HoloMed/PanelBG"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color           ("Tint",             Color) = (1,1,1,1)

        _BaseColor       ("Base Color",       Color) = (0.039, 0.059, 0.118, 0.82)
        _TopColor        ("Top Color",        Color) = (0.067, 0.110, 0.220, 0.78)

        _VignetteColor   ("Vignette Color",   Color) = (0.010, 0.020, 0.055, 0.90)
        _VignetteRadius  ("Vignette Radius",  Range(0,1))   = 0.55
        _VignetteSmooth  ("Vignette Smooth",  Range(0.01,1))= 0.60

        _GridColor       ("Grid Color",       Color) = (0.000, 0.784, 0.941, 0.06)
        _GridTiling      ("Grid Tiling",      Vector)= (20, 8, 0, 0)
        _GridWidth       ("Grid Line Width",  Range(0,0.5))= 0.04

        _HeaderColor     ("Header Color",     Color) = (0.026, 0.082, 0.235, 0.92)
        _HeaderHeight    ("Header Height",    Range(0,0.5))= 0.14

        _BorderColor     ("Border Color",     Color) = (0.000, 0.784, 0.941, 0.50)
        _BorderWidthPx   ("Border Width px",  Range(0,12)) = 2.0
        _BorderBright    ("Border Brightness",Range(0,3))   = 1.0

        _BracketColor    ("Bracket Color",    Color) = (0.000, 0.784, 0.941, 0.90)
        _BracketSize     ("Bracket Size px",  Range(0,80)) = 28
        _BracketWidthPx  ("Bracket Width px", Range(0,8))  = 2.5

        _CornerRadius    ("Corner Radius px", Range(0,80)) = 16
        _RectSize        ("Rect Size px",     Vector)      = (400, 300, 0, 0)
        _Stencil         ("Stencil ID",       Float) = 0
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

            fixed4 _Color; fixed4 _BaseColor; fixed4 _TopColor;
            fixed4 _VignetteColor; float _VignetteRadius; float _VignetteSmooth;
            fixed4 _GridColor; float4 _GridTiling; float _GridWidth;
            fixed4 _HeaderColor; float _HeaderHeight;
            fixed4 _BorderColor; float _BorderWidthPx; float _BorderBright;
            fixed4 _BracketColor; float _BracketSize; float _BracketWidthPx;
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

            float gridLine(float v, float w)
            {
                float fw = fwidth(v);
                return 1.0 - smoothstep(w * 0.5 - fw, w * 0.5 + fw, abs(frac(v) - 0.5));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float2 rectPx   = max(_RectSize.xy, float2(1,1));
                float2 pixelPos = (uv - 0.5) * rectPx;
                float2 halfExt  = rectPx * 0.5;

                // rounded mask
                float dist = sdfRoundedBox(pixelPos, halfExt, _CornerRadius);
                float mask = 1.0 - smoothstep(-1.0, 0.5, dist);
                clip(mask - 0.001);

                // base gradient
                fixed4 col = lerp(_TopColor, _BaseColor, uv.y * uv.y);

                // header band (top)
                float inHeader = step(uv.y, _HeaderHeight);
                col = lerp(col, _HeaderColor, inHeader * _HeaderColor.a);

                // vignette
                float2 vc    = uv - float2(0.5, 0.65);
                float  vDist = length(vc * float2(1.0, 0.7));
                float  vMask = 1.0 - smoothstep(_VignetteRadius - _VignetteSmooth, _VignetteRadius, vDist);
                col.rgb = lerp(col.rgb, _VignetteColor.rgb, vMask * _VignetteColor.a);

                // grid (skip header)
                float gx = gridLine(uv.x * _GridTiling.x, _GridWidth);
                float gy = gridLine(uv.y * _GridTiling.y, _GridWidth);
                float g  = saturate(gx + gy) * (1.0 - inHeader);
                col.rgb  = lerp(col.rgb, _GridColor.rgb, g * _GridColor.a);

                // border — distance from rounded edge in px
                float edgeDistPx = -dist;
                float border = 1.0 - smoothstep(_BorderWidthPx - 1.0, _BorderWidthPx + 1.0, edgeDistPx);
                col.rgb = lerp(col.rgb, _BorderColor.rgb, border * _BorderColor.a * _BorderBright);

                // corner brackets — only near corners
                float2 distToCorner = halfExt - abs(pixelPos);   // px from nearest edges
                bool nearCornerX = distToCorner.x < _BracketSize;
                bool nearCornerY = distToCorner.y < _BracketSize;
                float onArmX = (distToCorner.y < _BracketWidthPx && distToCorner.x < _BracketSize) ? 1.0 : 0.0;
                float onArmY = (distToCorner.x < _BracketWidthPx && distToCorner.y < _BracketSize) ? 1.0 : 0.0;
                float brk = saturate(onArmX + onArmY);
                col.rgb = lerp(col.rgb, _BracketColor.rgb, brk * _BracketColor.a);

                col.a = mask * _BaseColor.a * i.color.a
                      * UnityGet2DClipping(i.worldPos.xy, _ClipRect);
                return col;
            }
            ENDCG
        }
    }
}
