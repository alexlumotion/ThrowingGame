Shader "Unlit/TripleVideo_Builtin"
{
    Properties
    {
        _Layer0Tex ("Layer0 (Video RGBA)", 2D) = "black" {}
        _Layer1Tex ("Layer1 (Video RGBA)", 2D) = "black" {}
        _Layer2Tex ("Layer2 (Video RGBA)", 2D) = "black" {}

        _Layer0Opacity ("Layer0 Opacity", Range(0,1)) = 1
        _Layer1Opacity ("Layer1 Opacity", Range(0,1)) = 1
        _Layer2Opacity ("Layer2 Opacity", Range(0,1)) = 1

        _Tint ("Tint (RGB)*", Color) = (1,1,1,1)

        [Toggle] _ForceOverlay ("Render As Overlay (always on top)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        Fog { Mode Off }

        // ===== PASS 1: NORMAL (visible when _ForceOverlay == 0) =====
        Pass
        {
            // звичайний тест глибини
            ZTest LEqual

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f     { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            sampler2D _Layer0Tex, _Layer1Tex, _Layer2Tex;
            float _Layer0Opacity, _Layer1Opacity, _Layer2Opacity;
            float4 _Tint;
            float  _ForceOverlay;  // 0 -> показуємо цю пасу, 1 -> ховаємо

            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            float4 AlphaOver(float4 under, float4 over)
            {
                float a_over = over.a;
                float a_out  = a_over + under.a * (1.0 - a_over);
                float3 c_out = (over.rgb * a_over + under.rgb * under.a * (1.0 - a_over)) / max(a_out, 1e-4);
                return float4(c_out, a_out);
            }

            float4 Compose(float2 uv)
            {
                float4 c0 = tex2D(_Layer0Tex, uv);
                float4 c1 = tex2D(_Layer1Tex, uv);
                float4 c2 = tex2D(_Layer2Tex, uv);

                c0.a = saturate(c0.a * _Layer0Opacity);
                c1.a = saturate(c1.a * _Layer1Opacity);
                c2.a = saturate(c2.a * _Layer2Opacity);

                float4 outCol = c0;
                outCol = AlphaOver(outCol, c1);
                outCol = AlphaOver(outCol, c2);
                outCol.rgb *= _Tint.rgb;
                return outCol;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 col = Compose(i.uv);

                // якщо _ForceOverlay == 1 — цю пасу «вимикаємо»
                col.a *= (1.0 - saturate(_ForceOverlay));
                return col;
            }
            ENDCG
        }

        // ===== PASS 2: OVERLAY (visible when _ForceOverlay == 1) =====
        Pass
        {
            // рендеримо навіть якщо геометрія попереду — завжди поверх
            ZTest Always

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f     { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            sampler2D _Layer0Tex, _Layer1Tex, _Layer2Tex;
            float _Layer0Opacity, _Layer1Opacity, _Layer2Opacity;
            float4 _Tint;
            float  _ForceOverlay;  // 1 -> показуємо цю пасу, 0 -> ховаємо

            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            float4 AlphaOver(float4 under, float4 over)
            {
                float a_over = over.a;
                float a_out  = a_over + under.a * (1.0 - a_over);
                float3 c_out = (over.rgb * a_over + under.rgb * under.a * (1.0 - a_over)) / max(a_out, 1e-4);
                return float4(c_out, a_out);
            }

            float4 Compose(float2 uv)
            {
                float4 c0 = tex2D(_Layer0Tex, uv);
                float4 c1 = tex2D(_Layer1Tex, uv);
                float4 c2 = tex2D(_Layer2Tex, uv);

                c0.a = saturate(c0.a * _Layer0Opacity);
                c1.a = saturate(c1.a * _Layer1Opacity);
                c2.a = saturate(c2.a * _Layer2Opacity);

                float4 outCol = c0;
                outCol = AlphaOver(outCol, c1);
                outCol = AlphaOver(outCol, c2);
                outCol.rgb *= _Tint.rgb;
                return outCol;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 col = Compose(i.uv);

                // якщо _ForceOverlay == 0 — цю пасу «вимикаємо»
                col.a *= saturate(_ForceOverlay);
                return col;
            }
            ENDCG
        }
    }

    Fallback Off
}