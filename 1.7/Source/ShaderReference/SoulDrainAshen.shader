Shader "Bernael/SoulDrainAshen"
{
    Properties
    {
        _MainTex ("Pawn layer", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ColorTwo ("Secondary tint", Color) = (1,1,1,1)
        _MaskTex ("Mask", 2D) = "black" {}
        [HideInInspector] _UseMask ("Use original color mask", Float) = 0
        _DrainAmount ("Drained appearance", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _MaskTex;
            float4 _Color, _ColorTwo;
            float _DrainAmount, _UseMask;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_base v)
            {
                v2f o; o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.texcoord; return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float4 tex=tex2D(_MainTex,i.uv);
                clip(tex.a*_Color.a-0.35);
                // CutoutComplex applies the red and green tint masks multiplicatively.
                // An unmasked pixel retains the source texture color.
                float2 mask = tex2D(_MaskTex,i.uv).rg;
                float3 maskedTint = lerp(float3(1,1,1),_Color.rgb,mask.r) *
                    lerp(float3(1,1,1),_ColorTwo.rgb,mask.g);
                float3 original = tex.rgb*lerp(_Color.rgb,maskedTint,_UseMask);
                float luminance=dot(original,float3(0.299,0.587,0.114));
                float gray=0.018+pow(saturate(luminance),0.85)*0.31;
                return float4(lerp(original,gray.xxx,saturate(_DrainAmount)),1);
            }
            ENDCG
        }
    }
    Fallback Off
}
