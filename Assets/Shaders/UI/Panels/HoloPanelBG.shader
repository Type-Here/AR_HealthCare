Shader "HoloMed/PanelBG"
{
    Properties
    {
        _MainTex         ("Sprite Texture",   2D)    = "white" {}

        // --- base fill ---
        _BaseColor       ("Base Color",       Color) = (0.039, 0.059, 0.118, 0.82)  // Abyss 0.82a
        _TopColor        ("Top Color",        Color) = (0.067, 0.110, 0.220, 0.78)  // Deep Navy 0.78a

        // --- inner vignette (makes centre feel deeper) ---
        _VignetteColor   ("Vignette Color",   Color) = (0.010, 0.020, 0.055, 0.90)
        _VignetteRadius  ("Vignette Radius",  Range(0,1))   = 0.55
        _VignetteSmooth  ("Vignette Smooth",  Range(0.01,1))= 0.60

        // --- scan-line / grid overlay ---
        _GridColor       ("Grid Color",       Color) = (0.000, 0.784, 0.941, 0.06)  // Holo Cyan very dim
        _GridTiling      ("Grid Tiling",      Vector)= (20, 8, 0, 0)                // cols, rows, -, -
        _GridWidth       ("Grid Line Width",  Range(0,0.5))= 0.04

        // --- header band (top stripe) ---
        _HeaderColor     ("Header Color",     Color) = (0.026, 0.082, 0.235, 0.92)
        _HeaderHeight    ("Header Height",    Range(0,0.5))= 0.14                   // fraction of panel

        // --- border glow ---
        _BorderColor     ("Border Color",     Color) = (0.000, 0.784, 0.941, 0.50)  // Holo Cyan
        _BorderWidth     ("Border Width",     Range(0,0.08))= 0.012
        _BorderBright    ("Border Brightness",Range(0,3))   = 1.0

        // --- corner brackets (AR targeting style) ---
        _BracketColor    ("Bracket Color",    Color) = (0.000, 0.784, 0.941, 0.90)
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

            // returns 1 inside a line of thickness 'w' centred on value 'v' in [0,1]
            float gridLine(float v, float w)
            {
                float fw = fwidth(v);
                return 1.0 - smoothstep(w * 0.5 - fw, w * 0.5 + fw, abs(frac(v) - 0.5));
            }

            // bracket helper: returns 1 if pixel is on a bracket arm
            float bracket(float2 uv, float size, float w)
            {
                // mirror to work in top-left quadrant only, then rotate by symmetry
                float2 q = float2(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
                float hLine = step(q.x, size) * (abs(q.y - 0.0) < w * 0.5 ? 1.0 : 0.0);
                float vLine = step(q.y, size) * (abs(q.x - 0.0) < w * 0.5 ? 1.0 : 0.0);
                return saturate(hLine + vLine);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;

                // --- base gradient (top lighter, bottom deeper) ---
                fixed4 col = lerp(_TopColor, _BaseColor, uv.y * uv.y);

                // --- header band ---
                float inHeader = step(uv.y, _HeaderHeight);
                col = lerp(col, _HeaderColor, inHeader * _HeaderColor.a);

                // --- vignette (darkens centre-bottom area for depth) ---
                float2 vc     = uv - float2(0.5, 0.65);
                float  vDist  = length(vc * float2(1.0, 0.7));
                float  vMask  = 1.0 - smoothstep(_VignetteRadius - _VignetteSmooth,
                                                  _VignetteRadius, vDist);
                col.rgb = lerp(col.rgb, _VignetteColor.rgb, vMask * _VignetteColor.a);

                // --- grid / scan-lines ---
                float gx = gridLine(uv.x * _GridTiling.x, _GridWidth);
                float gy = gridLine(uv.y * _GridTiling.y, _GridWidth);
                float g  = saturate(gx + gy) * (1.0 - inHeader); // no grid on header
                col.rgb  = lerp(col.rgb, _GridColor.rgb, g * _GridColor.a);

                // --- border glow ---
                float edgeX  = min(uv.x, 1.0 - uv.x);
                float edgeY  = min(uv.y, 1.0 - uv.y);
                float edge   = min(edgeX, edgeY) / _BorderWidth;
                float border = saturate(1.0 - edge) * _BorderBright;
                col.rgb      = lerp(col.rgb, _BorderColor.rgb, border * _BorderColor.a);

                // --- corner brackets ---
                float brk = bracket(uv, _BracketSize, _BracketWidth);
                col.rgb   = lerp(col.rgb, _BracketColor.rgb, brk * _BracketColor.a);

                // --- final alpha (uses base alpha, clipped by UI rect) ---
                col.a = _BaseColor.a * UnityGet2DClipping(i.worldPos.xy, _ClipRect);

                return col;
            }
            ENDCG
        }
    }
}
