Shader "Bernael/SoulDrain"
{
    Properties
    {
        _MainTex ("Captured victim", 2D) = "white" {}
        _SmokeTex ("Packed smoke", 2D) = "gray" {}
        _Phase ("Game time", Float) = 0
        _Progress ("Extraction progress", Range(0,1)) = 0
        _Opacity ("Opacity", Range(0,1)) = 1
        _Length ("Eye to victim distance", Float) = 3
        _Width ("Quad width", Float) = 2.4
        _Seed ("Seed", Float) = 0
        _Mode ("Smoke / eye / soul", Float) = 0
        _Pull ("Eye relative to victim", Vector) = (-3,0,0,0)
        _SoulAngle ("Captured body angle", Float) = 0
        _AshenTex ("Preview drained pawn", 2D) = "white" {}
        _DrainAmount ("Preview drain amount", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest LEqual
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex, _SmokeTex, _AshenTex;
            float4 _MainTex_TexelSize, _Pull;
            float _Phase, _Progress, _Opacity, _Length, _Width, _Seed, _Mode, _SoulAngle, _DrainAmount;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                if (_Mode > 1.5 && _Mode < 2.5)
                {
                    // One continuous head-first pull: rotation, stretching and travel
                    // all begin together, without a separate floating or turning stage.
                    float progress = saturate(_Progress);
                    float2 soulPos = v.vertex.xz;
                    float2 direction = normalize(_Pull.xy+float2(0.0001,0));
                    float2 normal = float2(-direction.y,direction.x);
                    float2 bodyAxis = float2(sin(radians(_SoulAngle)),cos(radians(_SoulAngle)));
                    float angle = atan2(bodyAxis.x*direction.y-bodyAxis.y*direction.x,dot(bodyAxis,direction));
                    float turn = 1-pow(1-progress,2.5);
                    float sine, cosine;
                    sincos(angle*turn,sine,cosine);
                    soulPos = float2(cosine*soulPos.x-sine*soulPos.y,sine*soulPos.x+cosine*soulPos.y);
                    float leading = saturate(0.5 + dot(v.vertex.xz,bodyAxis)*0.82);
                    soulPos -= normal*dot(soulPos,normal)*0.62*smoothstep(0,1,progress);
                    float travel = smoothstep(0,1,pow(progress,lerp(2.3,0.85,leading)));
                    float arc = sin(travel*UNITY_PI);
                    float2 destination = _Pull.xy;
                    float2 deformed = lerp(soulPos,destination,travel);
                    deformed += normal * arc * (0.09 + sin(v.uv.y*9+_Phase*4)*0.035);
                    deformed += normal * sin(v.uv.y*17+_Phase*5) * 0.012 * progress*(1-travel);
                    v.vertex.xz = deformed;
                }
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            void over(inout float4 dst, float3 color, float alpha)
            {
                alpha=saturate(alpha);
                dst.rgb=color*alpha+dst.rgb*(1-alpha);
                dst.a=alpha+dst.a*(1-alpha);
            }
            float4 frag(v2f i) : SV_Target
            {
                float4 col=0;
                if (_Mode > 2.5)
                {
                    float4 pawn=tex2D(_MainTex,i.uv);
                    pawn=lerp(pawn,tex2D(_AshenTex,i.uv),_DrainAmount);
                    return float4(pawn.rgb*pawn.a,pawn.a)*_Opacity;
                }
                if (_Mode > 1.5)
                {
                    float4 captured=tex2D(_MainTex,i.uv);
                    float2 pixel=_MainTex_TexelSize.xy*2.2;
                    float halo=max(max(tex2D(_MainTex,i.uv+float2(pixel.x,0)).a,
                        tex2D(_MainTex,i.uv-float2(pixel.x,0)).a),
                        max(tex2D(_MainTex,i.uv+float2(0,pixel.y)).a,
                        tex2D(_MainTex,i.uv-float2(0,pixel.y)).a));
                    float inside=min(min(tex2D(_MainTex,i.uv+float2(pixel.x,0)).a,
                        tex2D(_MainTex,i.uv-float2(pixel.x,0)).a),
                        min(tex2D(_MainTex,i.uv+float2(0,pixel.y)).a,
                        tex2D(_MainTex,i.uv-float2(0,pixel.y)).a));
                    float smoke=tex2D(_SmokeTex,i.uv*1.6+float2(_Phase*0.10,-_Phase*0.12)).g;
                    float detail=dot(captured.rgb,float3(0.30,0.59,0.11));
                    float visibility=1-smoothstep(0.975,1,_Progress);
                    float fill=captured.a*(0.44+smoke*0.24);
                    float edge=saturate(captured.a-inside);
                    over(col,float3(0.12,0.57,0.63),(halo-captured.a)*0.32);
                    over(col,lerp(float3(0.16,0.47,0.51),float3(0.54,0.87,0.86),detail),fill);
                    over(col,float3(0.68,0.98,0.96),edge*0.8);
                    return col*visibility*_Opacity;
                }
                if (_Mode > 0.5)
                {
                    float2 p=(i.uv-0.5)*2;
                    float r=length(p);
                    over(col,float3(0.65,0.004,0.019),exp(-r*r*8)*0.6);
                    over(col,float3(1,0.03,0.065),exp(-abs(p.y)*60-abs(p.x)*4)*0.24);
                    over(col,float3(1,0.58,0.49),exp(-r*r*165));
                    // The final condensed soul is swallowed by the red eye light.
                    over(col,float3(0.61,0.97,0.95),exp(-r*r*95)*
                        smoothstep(0.82,0.94,_Progress)*(1-smoothstep(0.97,1,_Progress)));
                    return col*_Opacity*(1-smoothstep(0.75,1,r));
                }
                // Only advected smoke here; the soul is a separate deforming pawn capture.
                float2 p=float2(i.uv.x*(_Length+1.1)-0.55,(i.uv.y-0.5)*_Width);
                float u=saturate(p.x/max(_Length,0.1));
                float gate=smoothstep(-0.10,0.15,p.x)*(1-smoothstep(_Length-0.08,_Length+0.45,p.x));
                float open=smoothstep(0.015,0.16,_Progress);
                float center=sin(u*4.5-_Phase*1.2+_Seed)*0.10*sin(u*UNITY_PI);
                float width=0.07+0.5*u;
                float y=(p.y-center)/width;
                float2 flow=float2(p.x*0.33+_Phase*0.24+_Seed,p.y*0.62+_Seed*0.19);
                float broad=tex2D(_SmokeTex,flow*0.85+float2(_Phase*0.04,0)).r;
                flow+=float2(broad-0.5,sin(u*5-_Phase)*0.12)*0.19;
                float smoke=tex2Dbias(_SmokeTex,float4(flow,0,2)).g;
                float detail=tex2D(_SmokeTex,flow*1.73+float2(_Phase*0.08,0.37)).b;
                float billow=tex2D(_SmokeTex,flow*1.21+float2(0.38,-_Phase*0.055)).r;
                float softEnvelope=exp(-y*y*1.35);
                // Broad, translucent density carries the effect. Fine wisps only add
                // internal motion, so the connection reads as smoke rather than ribbons.
                float cloud=pow(saturate(broad*0.82+billow*0.66+detail*0.42),1.12);
                float alpha=cloud*softEnvelope*gate*open*0.86;
                float red=saturate(billow*0.72+detail*0.32);
                over(col,lerp(float3(0.018,0.016,0.023),float3(0.29,0.042,0.052),red),alpha);
                over(col,float3(0.42,0.035,0.045),smoke*softEnvelope*gate*open*0.26);
                return col*_Opacity*(1-smoothstep(1.0,1.19,abs(p.y)));
            }
            ENDCG
        }
    }
    Fallback Off
}
