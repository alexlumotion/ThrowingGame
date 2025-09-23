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
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            sampler2D _Layer0Tex;
            sampler2D _Layer1Tex;
            sampler2D _Layer2Tex;

            float _Layer0Opacity;
            float _Layer1Opacity;
            float _Layer2Opacity;
            float4 _Tint;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // straight-alpha compositing: out = over + under*(1-over.a)
            float4 AlphaOver(float4 under, float4 over)
            {
                float a_over = over.a;
                float a_out  = a_over + under.a * (1.0 - a_over);
                float3 c_out = (over.rgb * a_over + under.rgb * under.a * (1.0 - a_over)) / max(a_out, 1e-4);
                return float4(c_out, a_out);
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 c0 = tex2D(_Layer0Tex, i.uv);
                float4 c1 = tex2D(_Layer1Tex, i.uv);
                float4 c2 = tex2D(_Layer2Tex, i.uv);

                // відео вже з альфою → множимо на опакіті шарів
                c0.a = saturate(c0.a * _Layer0Opacity);
                c1.a = saturate(c1.a * _Layer1Opacity);
                c2.a = saturate(c2.a * _Layer2Opacity);

                // складення: Layer0 (низ) → Layer1 → Layer2 (верх)
                float4 outCol = c0;
                outCol = AlphaOver(outCol, c1);
                outCol = AlphaOver(outCol, c2);

                outCol.rgb *= _Tint.rgb;
                return outCol;
            }
            ENDCG
        }
    }

    Fallback Off
}