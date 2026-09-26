Shader "Bernael/DarkMirage"
{
    Properties
    {
        _MainTex ("Caster capture (RGBA)", 2D) = "black" {}
        _DistanceTex ("Caster silhouette distance (linear)", 2D) = "white" {}
        _Phase ("Simulation time", Float) = 0
        _SpawnAge ("Seconds since summoning", Float) = 10
        _RevealBounds ("Captured pawn bottom/top UV", Vector) = (0.3,0.75,0,0)
        _Seed ("Phase offset", Float) = 0
        _Opacity ("Lifetime opacity", Range(0,1)) = 1
        _EyeA ("Eye A: UV, enabled, scale", Vector) = (0.47,0.6,1,1)
        _EyeB ("Eye B: UV, enabled, scale", Vector) = (0.53,0.6,1,1)
        [HideInInspector] _PreviewFireOnly ("Inspect complete flame geometry", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest LEqual
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            sampler2D _DistanceTex;
            float4 _MainTex_TexelSize;
            float _Phase, _Seed, _Opacity;
            float _SpawnAge;
            float4 _RevealBounds;
            float _PreviewFireOnly;
            float4 _EyeA, _EyeB;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }
            float silhouetteDistance(float2 uv)
            {
                return (tex2D(_DistanceTex,uv).r-0.5)*0.25;
            }
            float coverage(float distance)
            {
                float aa = max(fwidth(distance)*0.7,0.0012);
                return 1-smoothstep(-aa,aa,distance);
            }
            float randomCell(float2 p)
            {
                return frac(sin(dot(p,float2(41.73,289.17)))*15731.743);
            }
            float softNoise(float2 p)
            {
                float2 cell=floor(p), f=frac(p);
                f=f*f*f*(f*(f*6-15)+10);
                return lerp(lerp(randomCell(cell),randomCell(cell+float2(1,0)),f.x),
                    lerp(randomCell(cell+float2(0,1)),randomCell(cell+1),f.x),f.y);
            }
            float2 eyeLight(float2 uv, float4 eye)
            {
                float2 d = (uv - eye.xy) * float2(105, 160) / max(eye.w, 0.2);
                float r = dot(d,d);
                float core = 1-smoothstep(0.35,1.15,r);
                float halo = exp2(-r * 0.24)*0.52 + exp2(-r * 0.065)*0.13;
                return float2(core, halo) * eye.z;
            }
            float4 frag(v2f i) : SV_Target
            {
                float t = _Phase + _Seed;
                float2 uv = i.uv;
                float4 source = tex2D(_MainTex, uv);
                float a = source.a;
                float distance = silhouetteDistance(uv);
                float outline = coverage(distance-0.0095);

                // Independent of the random flame phase: body, eyes, then fire.
                float bodyProgress = saturate(_SpawnAge/0.85);
                float bottom = _RevealBounds.x-0.022;
                float top = _RevealBounds.y+0.022;
                float height = (uv.y-bottom)/max(top-bottom,0.05);
                // Static noise makes the inverse dissolve strictly bottom-to-top:
                // newly revealed pieces never flicker away on the following tick.
                float dissolveNoise = softNoise(uv*float2(43,51)+_Seed)*0.75
                    + softNoise(uv*float2(89,97)+13.2)*0.25;
                float front = lerp(-0.10,1.10,bodyProgress);
                float revealField = front-height-(dissolveNoise-0.5)*0.13;
                float reveal = smoothstep(-0.008,0.008,revealField);
                if (_SpawnAge<=0) reveal=0;
                if (bodyProgress>=1) reveal=1;
                float eyeIgnition = smoothstep(0.85,1.15,_SpawnAge);
                float fireProgress = smoothstep(1.15,1.95,_SpawnAge);
                float fireFront = lerp(bottom-0.06,top+0.17,fireProgress);
                float fireIgnition = smoothstep(1.15,1.35,_SpawnAge)
                    * (1-smoothstep(fireFront-0.025,fireFront+0.025,uv.y));

                // Fixed exterior volume. Animation transports density UP through it;
                // it never swings the entire outline or a solid flame sheet sideways.
                float crown = smoothstep(0.43,0.73,uv.y);
                float root = smoothstep(0.013,0.024,distance);
                float footFade = smoothstep(0.305,0.370,uv.y);
                float outward = max(distance-0.021,0);
                float width = 0.029+0.053*crown;
                float envelope = exp2(-pow(outward/width,2)*2.1);
                // The stored distance saturates at 0.125 UV; end the volume before it
                // saturates so faint noise cannot leak across the whole background.
                envelope *= 1-smoothstep(0.095,0.12,distance);
                float2 flowUV = uv*float2(32,13) + float2(_Seed*0.21,-t*1.6421053);
                flowUV.x += (softNoise(flowUV*0.27+8.2)-0.5)*0.32;
                // The detail layer has the same transport velocity in UV space.
                // It adds depth without counter-moving ripples or horizontal wobble.
                float flow = softNoise(flowUV)*0.88 + softNoise(flowUV*1.65+float2(7,19))*0.12;
                float threads = smoothstep(0.18,0.74,flow);
                // The connected flame bed cannot be punched out by a dark noise cell.
                // Broader rising tongues grow out of it, rather than appearing as
                // isolated bright fragments separated by two multiplied thresholds.
                float bed = exp2(-pow(outward/(0.022+0.021*crown),2)*1.6);
                bed *= 1-smoothstep(0.095,0.12,distance);
                float bedAlpha = bed*0.43*root*footFade;
                float tongueAlpha = envelope*pow(threads,1.2)*0.83*root*footFade;
                float fireAlpha = 1-(1-bedAlpha)*(1-tongueAlpha);
                float heat = pow(saturate(bed*0.22+threads*envelope*0.88),1.25);
                float3 fireColor = lerp(float3(0.075,0.14,0.29),float3(0.29,0.59,0.85),heat);
                float radiance = pow(heat,2.0)*envelope*root*footFade*0.18;

                // Stable close glow merges into the moving translucent threads.
                float halo = exp2(-pow(max(distance-0.011,0)/0.018,2))*0.20;
                halo *= smoothstep(0.0095,0.015,distance)*footFade;
                float3 rgb = fireColor*fireAlpha + float3(0.17,0.40,0.66)*halo*(1-fireAlpha)
                    + float3(0.16,0.48,0.76)*radiance;
                float alpha = fireAlpha+halo*(1-fireAlpha);
                rgb *= fireIgnition;
                alpha *= fireIgnition;
                // Editor inspection renders the complete flame with no pawn covering it.
                if (_PreviewFireOnly>0.5) return float4(rgb,alpha)*_Opacity;

                // Outside -> inside: flat ghost fire, a short luminous band, then a
                // thick almost-black vanilla outline. The bands stay separate.
                float glowDistance = max(distance-0.0095,0);
                float glow = exp2(-pow(glowDistance/0.008,2))*0.35*(1-outline);
                rgb = rgb*(1-glow) + float3(0.20,0.41,0.68)*glow;
                alpha = alpha + glow*(1-alpha);
                rgb = lerp(rgb,float3(0.008,0.014,0.028)*0.97,outline);
                alpha = lerp(alpha,0.97,outline);
                float detail = dot(source.rgb, float3(0.25,0.55,0.2));
                float3 bodyColor = lerp(float3(0.025,0.047,0.09),float3(0.068,0.13,0.20),smoothstep(0.15,0.7,detail));
                rgb = lerp(rgb,bodyColor*0.86,a);
                alpha = lerp(alpha,0.86,a);
                rgb *= reveal;
                alpha *= reveal;
                float dissolveEdge = (1-smoothstep(0.009,0.045,revealField))*reveal*outline
                    * (1-step(1,bodyProgress));
                rgb += float3(0.12,0.40,0.62)*dissolveEdge*0.68;
                float2 eyes = (eyeLight(uv,_EyeA) + eyeLight(uv,_EyeB))*eyeIgnition;
                float pulse = 0.94 + sin(t*2.4)*0.06;
                rgb += (float3(0.64,0.94,1)*eyes.x + float3(0.08,0.50,0.90)*eyes.y)*pulse;
                alpha = saturate(alpha + eyes.x*0.5 + eyes.y*0.18);
                float border = smoothstep(0,0.025,uv.x)*smoothstep(0,0.025,1-uv.x)*
                               smoothstep(0,0.025,uv.y)*smoothstep(0,0.025,1-uv.y);
                return float4(rgb,alpha) * (_Opacity*border);
            }
            ENDCG
        }
    }
    Fallback Off
}
