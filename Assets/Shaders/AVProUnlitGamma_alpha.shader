Shader "Lumotion/AVProUnlitGamma_BIRP"
{
    Properties
    {
        _MainTex ("Video Texture", 2D) = "white" {}
        _Gamma   ("Gamma (sRGB lift)", Range(0.10, 3.0)) = 0.4545   // ≈ 1/2.2 → світліше
        _Tint    ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        // Прозорий оверлей:
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back     // односторонній ренд

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Tint;
            float  _Gamma;

            v2f vert (appdata v) {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                fixed4 c = tex2D(_MainTex, i.uv) * _Tint * i.color;

                // У Linear-проектах піднімаємо до «sRGB-схожої» яскравості:
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    c.rgb = pow(max(c.rgb, 1e-6), _Gamma);
                #endif

                return c;
            }
            ENDCG
        }
    }
}