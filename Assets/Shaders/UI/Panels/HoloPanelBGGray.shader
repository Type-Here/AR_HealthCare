Shader "HoloMed/PanelBGGray"
{
    Properties
    {
        _MainTex         ("Sprite Texture",   2D)    = "white" {}

        _BaseColor       ("Base Color",       Color) = (0.10, 0.10, 0.10, 0.80)
        _TopColor        ("Top Color",        Color) = (0.25, 0.25, 0.25, 0.82)

        _VignetteColor   ("Vignette Color",   Color) = (0.03, 0.03, 0.03, 0.85)
        _VignetteRadius  ("Vignette Radius",  Range(0,1))   = 0.58
        _VignetteSmooth  ("Vignette Smooth",  Range(0.01,1))= 0.55

        _GridColor       ("Grid Color",       Color) = (1.0, 1.0, 1.0, 0.04)
        _GridTiling      ("Grid Tiling",      Vector)= (20, 8, 0, 0)
        _GridWidth       ("Grid Line Width",  Range(0,0.5))= 0.04

        _HeaderColor     ("Header Color",     Color) = (0.18, 0.18, 0.18, 0.90)
        _HeaderHeight    ("Header Height",    Range(0,0.5))= 0.14

        _BorderColor     ("Border Color",     Color) = (0.75, 0.75, 0.75, 0.30)
        _BorderWidth     ("Border Width",     Range(0,0.08))= 0.012
        _BorderBright    ("Border Brightness",Range(0,3))   = 0.70

        _BracketColor    ("Bracket Color",    Color) = (0.85, 0.85, 0.85, 0.75)
        _BracketSize     ("Bracket Size",     Range(0,0.25))= 0.10
        _BracketWidth    ("Bracket Width",    Range(0,0.05))= 0.012

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
                float2 texcoord : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;

            fixed4  _BaseColor;
            fixed4  _TopColor;
            fixed4  _VignetteColor;
            float   _VignetteRadius;
            float   _VignetteSmooth;
            fixed4  _GridColor;
            float4  _GridTiling;
            float   _GridWidth;
            fixed4  _HeaderColor;
            float   _HeaderHeight;
            fixed4  _BorderColor;
            float   _BorderWidth;
            float   _BorderBright;
            fixed4  _BracketColor;
            float   _BracketSize;
            float   _BracketWidth;
            float4  _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex   = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color    = v.color;
                o.worldPos = v.vertex;
                return o;
            }

            float gridLine(float v, float w)
            {
                float fw = fwidth(v);
                return 1.0 - smoothstep(w * 0.5 - fw, w * 0.5 + fw, abs(frac(v) - 0.5));
            }

            float bracket(float2 uv, float size, float w)
            {
                float2 q = float2(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                float hLine = step(q.x, size) * (abs(q.y) < w * 0.5 ? 1.0 : 0.0);
                float vLine = step(q.y, size) * (abs(q.x) < w * 0.5 ? 1.0 : 0.0);
                return saturate(hLine + vLine);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;

                fixed4 col = lerp(_TopColor, _BaseColor, uv.y * uv.y);

                float inHeader = step(uv.y, _HeaderHeight);
                col = lerp(col, _HeaderColor, inHeader * _HeaderColor.a);

                float2 vc    = uv - float2(0.5, 0.65);
                float  vDist = length(vc * float2(1.0, 0.7));
                float  vMask = 1.0 - smoothstep(_VignetteRadius - _VignetteSmooth,
                                                 _VignetteRadius, vDist);
                col.rgb = lerp(col.rgb, _VignetteColor.rgb, vMask * _VignetteColor.a);

                float gx = gridLine(uv.x * _GridTiling.x, _GridWidth);
                float gy = gridLine(uv.y * _GridTiling.y, _GridWidth);
                float g  = saturate(gx + gy) * (1.0 - inHeader);
                col.rgb  = lerp(col.rgb, _GridColor.rgb, g * _GridColor.a);

                float edgeX  = min(uv.x, 1.0 - uv.x);
                float edgeY  = min(uv.y, 1.0 - uv.y);
                float edge   = min(edgeX, edgeY) / _BorderWidth;
                float border = saturate(1.0 - edge) * _BorderBright;
                col.rgb      = lerp(col.rgb, _BorderColor.rgb, border * _BorderColor.a);

                float brk = bracket(uv, _BracketSize, _BracketWidth);
                col.rgb   = lerp(col.rgb, _BracketColor.rgb, brk * _BracketColor.a);

                col.a = _BaseColor.a * UnityGet2DClipping(i.worldPos.xy, _ClipRect);

                return col;
            }
            ENDCG
        }
    }
}
