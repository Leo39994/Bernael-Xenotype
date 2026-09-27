Shader "Bernael/DarkMirage"
{
    Properties
    {
        _MainTex ("Caster capture (RGBA)", 2D) = "black" {}
        _DistanceTex ("Caster silhouette distance (linear)", 2D) = "white" {}
        _Phase ("Simulation time", Float) = 0
        _SpawnAge ("Seconds since summoning", Float) = 10
        _RevealBounds ("Captured pawn bottom/top/left/right UV", Vector) = (0.3,0.75,0.3,0.7)
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
            float joinFlames(float a, float b, float radius)
            {
                float h=saturate(0.5+0.5*(b-a)/radius);
                return lerp(b,a,h)-radius*h*(1-h);
            }
            // A broad curved tongue with a tapered tip. Its shape travels with
            // the rising flame packet; it does not oscillate sideways in place.
            float flameTongue(float2 uv, float2 anchor, float width, float reach,
                float bend, float curl)
            {
                float y=uv.y-anchor.y;
                float h=saturate(y/reach);
                float center=anchor.x + bend*h*h + curl*width*sin(h*3.141593)*h;
                float radius=width*pow(saturate(1-h),0.8)
                    *(0.88+0.16*sin(h*3.141593));
                if(y<0) return length(uv-anchor)-radius;
                float2 bounds=float2(abs(uv.x-center)-radius,y-reach);
                // Euclidean distance outside the tip gives a round white cap
                // and smooth halo, instead of a square caused by max(x,y).
                return length(max(bounds,0))+min(max(bounds.x,bounds.y),0);
            }
            float risingFlame(float2 uv, float2 anchor, float width, float rise,
                float direction, float phase, float time)
            {
                // Independent emission rates and per-emission shapes remove the
                // shared sine-wave rhythm. Two overlapping packets feed each root.
                float rate=0.90+0.35*randomCell(float2(phase,17.2));
                float clock=time*rate+phase
                    +0.12*(softNoise(float2(time*0.7,phase+4.2))-0.5);
                float field=1;
                [unroll] for(int packet=0;packet<2;packet++)
                {
                    float tick=clock+packet*0.5;
                    float cycle=floor(tick);
                    float age=frac(tick);
                    float variation=randomCell(float2(cycle,phase+packet*13.7));
                    float lean=randomCell(float2(cycle+9.1,phase))-0.5;
                    // The tip always travels up. Later, the tail catches it to
                    // pinch off a small wisp rather than pull the whole tip down.
                    float tip=0.022+rise*(0.82+variation*0.36)*age;
                    float lift=(tip+0.012)*smoothstep(0.42,1.0,age);
                    float2 root=anchor+float2(lean*0.012*age,lift-0.012);
                    float reach=max(tip-lift+0.012,0.003);
                    // Side roots must feed the existing blue bed gradually;
                    // a fast radius ramp makes the lower white contour kick out.
                    float birthEnd=lerp(0.14,0.28,abs(direction));
                    float widthScale=smoothstep(0,birthEnd,age)
                        *(1-smoothstep(0.52,1.0,age));
                    float bodyWidth=max(width*(0.85+variation*0.30)*widthScale,0.0005);
                    float bend=(direction*0.45+lean*1.60)*width*(0.25+age*1.3);
                    float tongue=flameTongue(uv,root,bodyWidth,reach,bend,lean*2.4);
                    // Anchor the side flame's first section inside the blue bed.
                    // Its rising head remains free, but a new packet cannot puff
                    // out the lower boundary before it has started climbing.
                    tongue+=abs(direction)*0.018
                        *(1-smoothstep(0,0.045,uv.y-anchor.y));
                    // End with no stroke/glow before the cycle wraps. The next
                    // packet starts inside the connected blue bed, without a pop.
                    tongue+=0.090*smoothstep(0.80,1.0,age)
                        +0.090*(1-smoothstep(0,max(birthEnd,0.16),age));
                    field=joinFlames(field,tongue,0.012);
                }
                return field;
            }
            float4 frag(v2f i) : SV_Target
            {
                float t = _Phase + _Seed;
                // Slow only the flame flow; summoning, expiry and eyes keep their timing.
                float flameTime = _Phase*0.70 + _Seed;
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
                // Move the entire reveal band above the canvas once ignited.
                // Capping it relative to the pawn keeps clipping tall flame tips
                // and their glow long after the summoning animation has ended.
                float fireFront = lerp(bottom-0.06,1.025,fireProgress);
                float fireIgnition = smoothstep(1.15,1.35,_SpawnAge)
                    * (1-smoothstep(fireFront-0.025,fireFront+0.025,uv.y));

                // Solid blue flame sheets around the unchanged caster outline.
                // The packed contour anchors are generated once from the caster.
                float flame=distance-0.035;
                float span=max(_RevealBounds.w-_RevealBounds.z,0.12);
                [unroll] for(int tongue=0;tongue<5;tongue++)
                {
                    float lane=(tongue+0.5)/5.0;
                    float x=lerp(_RevealBounds.z,_RevealBounds.w,lane);
                    float rootY=tex2D(_DistanceTex,float2(x,0.5)).g;
                    float phase=tongue*2.399+_Seed;
                    float width=clamp(span*0.132,0.0275,0.0473);
                    float2 anchor=float2(x,rootY);
                    if(rootY>0.01)
                    {
                        flame=joinFlames(flame,risingFlame(uv,anchor,width,0.185,0,phase,flameTime),0.025);
                    }
                }
                // Side tongues curl upwards out of the shoulders and lower body.
                [unroll] for(int side=0;side<2;side++)
                {
                    float sign=side*2-1;
                    [unroll] for(int tongue=0;tongue<2;tongue++)
                    {
                        float y=lerp(_RevealBounds.x,_RevealBounds.y,0.14+tongue*0.36);
                        float4 contour=tex2D(_DistanceTex,float2(0.5,y));
                        float x=side==0?contour.b:contour.a;
                        float phase=side*4.7+tongue*2.399+_Seed;
                        // Keep newborn bases under the permanent blue sheet;
                        // only the upward part should push out the visible edge.
                        float2 anchor=float2(x+sign*0.008,y);
                        if(contour.a>contour.b)
                        {
                            flame=joinFlames(flame,risingFlame(uv,anchor,0.0363,0.155,sign,phase,flameTime),0.023);
                        }
                    }
                }
                // Use the same continuous field for fill, stroke and glow; only
                // the antialiasing coverage depends on screen derivatives.
                float edge=flame;
                float fill=coverage(edge);
                const float rimWidth=0.012; // Twice the previous 0.006 UV white stroke.
                float stroke=coverage(edge-rimWidth);
                float tint=saturate((uv.y-(_RevealBounds.x-0.035)) /
                    max(_RevealBounds.y-_RevealBounds.x+0.12,0.15));
                float3 navy=float3(0.075,0.137,0.173);
                float3 fireColor=lerp(float3(0.49,0.70,0.86),float3(0.74,0.865,0.945),tint);
                // Broad curved ribbons travel upward inside the BLUE sheet.
                // Deeper blue troughs and pale crests give the flame depth while
                // keeping the character and the solid white contour independent.
                float2 flowUV=uv*float2(14,8)+float2(_Seed*0.3,-flameTime*1.35);
                float curl=softNoise(flowUV*0.48+float2(3.7,8.1));
                flowUV.x+=(curl-0.5)*1.4;
                float flow=softNoise(flowUV);
                float ribbons=softNoise(flowUV*float2(1.55,0.80)+float2(4.3,9.2));
                float depth=smoothstep(0.20,0.80,flow);
                float crest=smoothstep(0.48,0.83,ribbons)*(0.45+0.55*depth);
                fireColor*=0.87+0.14*depth;
                fireColor=lerp(fireColor,float3(0.81,0.92,0.99),crest*0.30);
                float innerLight=exp2(-pow(min(edge,0)/0.016,2)*1.4)*0.28;
                fireColor=lerp(fireColor,float3(0.90,0.96,1),innerLight);
                float3 rgb=lerp(float3(1,1,1),fireColor,fill)*stroke;
                float glowDistance=max(edge-rimWidth,0);
                float glow=exp2(-pow(glowDistance/0.014,2)*1.7)*0.32
                    +exp2(-pow(glowDistance/0.028,2)*1.5)*0.07;
                glow*=1-smoothstep(0.035,0.060,glowDistance);
                rgb+=float3(0.93,0.97,1)*glow*(1-stroke);
                float alpha=stroke+glow*(1-stroke);
                rgb *= fireIgnition;
                alpha *= fireIgnition;
                // Editor inspection isolates the exterior flame layer.
                if (_PreviewFireOnly>0.5) return float4(rgb,alpha)*(_Opacity*(1-outline));

                // Keep the caster a single dark shape within the pale fire.
                rgb = lerp(rgb,navy,outline);
                alpha = lerp(alpha,1,outline);
                float detail = dot(source.rgb, float3(0.25,0.55,0.2));
                float3 bodyColor = navy
                    + smoothstep(0.15,0.7,detail)*float3(0.007,0.009,0.010);
                rgb = lerp(rgb,bodyColor,a);
                alpha = lerp(alpha,1,a);
                rgb *= reveal;
                alpha *= reveal;
                float dissolveEdge = (1-smoothstep(0.009,0.045,revealField))*reveal*outline
                    * (1-step(1,bodyProgress));
                rgb += float3(0.51,0.71,0.86)*dissolveEdge*0.68;
                float2 eyes = (eyeLight(uv,_EyeA) + eyeLight(uv,_EyeB))*eyeIgnition;
                float pulse = 0.94 + sin(t*2.4)*0.06;
                rgb += (float3(1,1,1)*eyes.x + float3(0.51,0.71,0.86)*eyes.y)*pulse;
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
