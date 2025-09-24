Shader "Lumotion/AVProUnlitGamma_BIRP"
{
    Properties
    {
        _MainTex ("Video Texture", 2D) = "white" {}
        _Gamma   ("Gamma (sRGB lift)", Range(0.10, 3.0)) = 0.4545
        _Tint    ("Tint", Color) = (1,1,1,1)

        [Toggle] _ForceOverlay ("Render As Overlay (always on top)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        // Прозорий бленд, без запису в глибину
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_ST;
        fixed4 _Tint;
        float  _Gamma;
        float  _ForceOverlay;

        struct appdata {
            float4 vertex : POSITION;
            float2 uv     : TEXCOORD0;
            float4 color  : COLOR;
        };
        struct v2f {
            float4 pos  : SV_Position;
            float2 uv   : TEXCOORD0;
            float4 color: COLOR;
        };

        v2f vert (appdata v) {
            v2f o;
            o.pos   = UnityObjectToClipPos(v.vertex);
            o.uv    = TRANSFORM_TEX(v.uv, _MainTex);
            o.color = v.color;
            return o;
        }

        fixed4 SampleColor(v2f i)
        {
            fixed4 c = tex2D(_MainTex, i.uv) * _Tint * i.color;

            // У Linear-проектах піднімаємо до «sRGB-схожої» яскравості:
            #if !defined(UNITY_COLORSPACE_GAMMA)
                c.rgb = pow(max(c.rgb, 1e-6), _Gamma);
            #endif
            return c;
        }
        ENDCG

        // ------- PASS 1: NORMAL (поважає глибину), активна коли _ForceOverlay == 0
        Pass
        {
            ZTest LEqual
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            fixed4 frag (v2f i) : SV_Target {
                fixed4 col = SampleColor(i);
                // Вимикаємо цю пасу, якщо ввімкнено Overlay
                col.a *= (1.0 - saturate(_ForceOverlay));
                return col;
            }
            ENDCG
        }

        // ------- PASS 2: OVERLAY (завжди зверху), активна коли _ForceOverlay == 1
        Pass
        {
            ZTest Always
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            fixed4 frag (v2f i) : SV_Target {
                fixed4 col = SampleColor(i);
                // Показуємо лише коли Overlay увімкнено
                col.a *= saturate(_ForceOverlay);
                return col;
            }
            ENDCG
        }
    }

    Fallback Off
}