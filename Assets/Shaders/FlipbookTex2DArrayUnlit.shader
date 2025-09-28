Shader "Custom/FlipbookTex2DArrayUnlit"
{
    Properties
    {
        _FlipbookTex("Flipbook Texture2DArray", 2DArray) = "" {}
        _Frame("Frame Index", Int) = 0
        _Color("Tint", Color) = (1,1,1,1)

        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip", Float) = 0
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5

        // ▼ ДОДАНО: керування зет-тестом для “оверлею”
        [Enum(UnityEngine.Rendering.CompareFunction)]
        _ZTestMode ("ZTest (LessEqual=звично, Always=Overlay)", Float) = 4
        // 4 = LessEqual, 8 = Always
    }
    SubShader
    {
        // Transparent черга за замовчуванням, але можна підняти у матеріалі до 4000
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest [_ZTestMode]   // ← використовує значення з матеріалу

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma shader_feature_local _ALPHATEST_ON
            #include "UnityCG.cginc"

            UNITY_DECLARE_TEX2DARRAY(_FlipbookTex);
            uniform int _Frame;
            uniform fixed4 _Color;
            uniform fixed _Cutoff;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float2 uv:TEXCOORD0; float4 vertex:SV_POSITION; };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = UNITY_SAMPLE_TEX2DARRAY(_FlipbookTex, float3(i.uv, _Frame));
                col *= _Color;

                #ifdef _ALPHATEST_ON
                clip(col.a - _Cutoff);
                #endif

                return col;
            }
            ENDCG
        }
    }
}