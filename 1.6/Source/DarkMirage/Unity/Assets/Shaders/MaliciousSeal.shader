Shader "Bernael/MaliciousSeal"
{
    Properties
    {
        _Phase ("Seconds since casting, binding or branding", Float) = 0
        _Inscribe ("Inscription, chain growth or stamp progress", Range(0,1)) = 1
        _Close ("Closing, chain release or fading progress", Range(0,1)) = 0
        _Opacity ("Opacity", Range(0,1)) = 1
        _Seed ("Seed", Float) = 0
        _Mode ("Ground seal / chain / brand", Float) = 0
        _Length ("Chain length in cells", Float) = 2
        _Size ("Quad size in cells", Float) = 1
    }
    SubShader
    {
        // Queued with vanilla's glowing motes (3151): after the map's lighting overlay (3100), so the seal keeps
        // its colours at dusk and at night, and before fog of war (3175).
        Tags { "Queue"="Transparent+151" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest LEqual
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Phase, _Inscribe, _Close, _Opacity, _Seed, _Mode, _Length, _Size;

            static const float TAU = 6.2831853;
            // Dire Orb's and Dark Mirage's palette: the concept's black emblem becomes navy and its grey skull pale
            // blue bone, inside the same solid white contour and soft white glow.
            static const float3 Navy = float3(0.086,0.133,0.169);
            static const float3 Deep = float3(0.51,0.71,0.859);
            static const float3 Pale = float3(0.725,0.851,0.929);
            // The emblem is measured in its ring's radius, and its quad reaches this far from the middle.
            // MaliciousSealMapComponent sizes the ground seal by it.
            static const float Extent = 1.6;
            // Contour and glow widths in cells. The ground seal's match Dire Orb's; the brand and chains are smaller.
            static const float2 GroundLine = float2(0.036,0.055);
            static const float2 BrandLine = float2(0.024,0.04);
            static const float2 ChainLine = float2(0.022,0.04);
            // MaliciousSealMapComponent.ChainWidth, the chain quad's width in cells.
            static const float ChainWidth = 0.8;
            // MaliciousSealMapComponent.CloseTicks in seconds.
            static const float CloseSeconds = 0.667;
            // Shapes measured from the concept, with the ring's radius as 1 and +y up.
            static const float RingHalf = 0.057;
            // Its diagonal spikes lean 47.5 degrees off the vertical and its side daggers 50: (sin, cos) of each.
            static const float2 DiagonalAxis = float2(0.7373,0.6756);
            static const float2 SideAxis = float2(0.7660,0.6428);
            static const float CentreTip = 0.32;
            static const float SideTip = 0.34;
            // Where each dagger meets the skull, and the right eye socket's middle.
            static const float2 CentreEntry = float2(0,0.25);
            static const float2 SideEntry = float2(0.21,0.177);
            static const float2 EyeCentre = float2(0.13,-0.106);

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            float4 over(float4 back, float4 front)
            {
                return front+back*(1-front.a);
            }
            float sq(float x)
            {
                return x*x;
            }
            float easeInOut(float x)
            {
                x=saturate(x);
                return x*x*(3-2*x);
            }
            // Overshoots to about 1.1 before settling, so shapes pop into place.
            float easeOutBack(float x)
            {
                float k=saturate(x)-1;
                return 1+2.70158*k*k*k+1.70158*k*k;
            }
            float smin(float a, float b, float k)
            {
                float h=saturate(0.5+0.5*(b-a)/k);
                return lerp(b,a,h)-k*h*(1-h);
            }
            float segment(float2 p, float2 a, float2 b)
            {
                float2 pa=p-a, ba=b-a;
                return length(pa-ba*saturate(dot(pa,ba)/dot(ba,ba)));
            }
            float box(float2 p, float2 size, float round)
            {
                float2 q=abs(p)-size+round;
                return length(max(q,0))+min(max(q.x,q.y),0)-round;
            }
            // Close enough to an ellipse's distance for fills, contours and glow while its radii are alike.
            float ellipse(float2 p, float2 radii)
            {
                float k0=length(p/radii);
                float k1=length(p/(radii*radii));
                return k0*(k0-1)/max(k1,1e-5);
            }
            // Two circle arcs meeting in points at y = +-sqrt(r*r - d*d).
            float vesica(float2 p, float r, float d)
            {
                p=abs(p);
                float b=sqrt(r*r-d*d);
                return (p.y-b)*d>p.x*b ? length(p-float2(0,b)) : length(p-float2(-d,0))-r;
            }
            // Narrowing from topWidth at y = +height to bottomWidth at y = -height.
            float trapezoid(float2 p, float bottomWidth, float topWidth, float height)
            {
                float2 k1=float2(topWidth,height), k2=float2(topWidth-bottomWidth,2*height);
                p.x=abs(p.x);
                float2 ca=float2(p.x-min(p.x,p.y<0?bottomWidth:topWidth),abs(p.y)-height);
                float2 cb=p-k1+k2*saturate(dot(k1-p,k2)/dot(k2,k2));
                float s=cb.x<0&&ca.y<0?-1:1;
                return s*sqrt(min(dot(ca,ca),dot(cb,cb)));
            }
            // A circle of radius ra at a, joined by tangents to one of radius rb at b.
            float cone(float2 p, float2 a, float2 b, float ra, float rb)
            {
                float2 ab=b-a;
                float h=max(length(ab),1e-4);
                float2 d=ab/h;
                p-=a;
                float2 q=float2(abs(d.x*p.y-d.y*p.x),dot(p,d));
                float s=clamp((ra-rb)/h,-0.99,0.99);
                float c=sqrt(1-s*s);
                float k=c*q.y-s*q.x;
                if(k<0) return length(q)-ra;
                if(k>c*h) return length(q-float2(0,h))-rb;
                return c*q.x+s*q.y-ra;
            }
            // A diamond along +y with points at a and b, widest (width) at m.
            float kite(float2 p, float a, float m, float b, float width)
            {
                p.x=abs(p.x);
                float2 A=float2(0,a), M=float2(width,m), B=float2(0,b);
                float d=min(segment(p,A,M),segment(p,M,B));
                bool inside=(M.x-A.x)*(p.y-A.y)-(M.y-A.y)*(p.x-A.x)>0&&(B.x-M.x)*(p.y-M.y)-(B.y-M.y)*(p.x-M.x)>0;
                return inside?-d:d;
            }
            float angle01(float2 p)
            {
                // 0 at the top of the emblem, increasing clockwise.
                return frac(atan2(p.x,p.y)/TAU+1);
            }

            // Fills a shape, rims it in a solid white contour and wraps that in a soft white glow, as Dire Orb paints
            // its flames. d is the distance to the shape and px one screen pixel, both in the shape's units, and
            // widths holds its contour and glow widths.
            float4 paint(float d, float3 fill, float px, float2 widths)
            {
                float aa=px*0.75;
                float stroke=max(widths.x,px*1.3);
                float halo=max(widths.y,px*2);
                float inside=1-smoothstep(-aa,aa,d);
                float rim=1-smoothstep(-aa,aa,d-stroke);
                float3 c=lerp(float3(1,1,1),fill,inside);
                float g=max(d-stroke,0);
                float glow=0.45*exp2(-g*g/(halo*halo)*1.4427)*(1-smoothstep(halo*1.8,halo*2.8,g))*(1-rim);
                return float4(c*rim+glow,rim+glow);
            }

            // One of the ring's spikes along +y. It pops out of the ring as grow runs from 0 to 1.
            float spike(float2 p, float grow, float inner, float outer, float width)
            {
                if(grow<=0) return 1e3;
                return kite(p,1-(1-inner)*grow,1,1+(outer-1)*grow,width*saturate(grow*1.5));
            }
            float spikeGrow(float draw, float at)
            {
                return draw>=1?1:easeOutBack((draw-at)/0.07);
            }
            // The spiked ring, drawn clockwise from the top as far as draw. Each spike pops out as the ring reaches it.
            float ringShape(float2 p, float draw)
            {
                if(draw<=0) return 1e3;
                float d=abs(length(p)-1)-RingHalf;
                if(draw<1&&angle01(p)>draw)
                {
                    float2 end=float2(sin(draw*TAU),cos(draw*TAU));
                    d=min(length(p-float2(0,1)),length(p-end))-RingHalf;
                }
                // Folded into one quadrant; p's signs tell which spike it is.
                float2 q=abs(p);
                float vertical=p.y>=0?0:0.5;
                float horizontal=p.x>=0?0.25:0.75;
                float diagonal=(p.x>=0?(p.y>=0?47.5:132.5):(p.y>=0?312.5:227.5))/360;
                d=min(d,spike(q,spikeGrow(draw,vertical),0.845,1.21,0.094));
                d=min(d,spike(q.yx,spikeGrow(draw,horizontal),0.845,1.21,0.094));
                float2 slanted=float2(dot(q,float2(DiagonalAxis.y,-DiagonalAxis.x)),dot(q,DiagonalAxis));
                return min(d,spike(slanted,spikeGrow(draw,diagonal),0.905,1.32,0.095));
            }

            // A dagger along +y pointing at the middle, its point tip from it. The top dagger has the concept's
            // diamond pommel; the side ones have its stepped grip, jutting out towards the top dagger (-x).
            float dagger(float2 p, float tip, bool side)
            {
                float d=max(vesica(float2(p.x,p.y-tip-0.28),0.5476,0.4706),p.y-tip-0.31);
                d=min(d,box(p-float2(0,tip+0.33),float2(0.04,0.035),0));
                if(side)
                {
                    d=min(d,box(p-float2(-0.012,tip+0.33),float2(0.043,0.034),0.004));
                    d=min(d,box(p-float2(-0.027,tip+0.402),float2(0.066,0.043),0.006));
                }
                else d=min(d,kite(p,tip+0.34,tip+0.408,tip+0.455,0.072));
                return d;
            }

            // The concept's skull: a full crown, square cheekbones, then a narrow upper jaw with small teeth
            // between two fangs. It has no lower jaw.
            float skullShape(float2 p)
            {
                float d=ellipse(p-float2(0,-0.025),float2(0.31,0.275));
                d=smin(d,box(p-float2(0,-0.145),float2(0.286,0.12),0.035),0.03);
                d=min(d,trapezoid(p-float2(0,-0.325),0.142,0.195,0.066));
                float2 q=float2(abs(p.x),p.y);
                d=min(d,cone(q,float2(0.112,-0.372),float2(0.1,-0.448),0.022,0));
                // Five small teeth between the fangs; only the nearest one matters.
                float2 tooth=float2(q.x-min(round(q.x/0.034),2)*0.034,-p.y);
                return min(d,kite(tooth,0.37,0.389,0.407,0.016));
            }
            // Bone deepening toward the jaw with a sheen on the crown, shaded brows over scowling navy eye holes in
            // which an ember burns, and a navy nose. As eyes rises to 1 the ember fills its hole and turns white.
            float3 skullColor(float2 p, float eyes, float aa)
            {
                float2 q=float2(abs(p.x),p.y);
                float3 c=lerp(Deep,Pale,smoothstep(-0.42,0.12,p.y));
                float2 sheen=p-float2(-0.07,0.16);
                c=lerp(c,float3(0.9,0.96,1),0.6*exp2(-dot(sheen,sheen)*90));
                float shade=1-smoothstep(-aa,aa,ellipse(q-float2(0.148,-0.09),float2(0.108,0.064)));
                c=lerp(c,lerp(Deep,Navy,0.45),shade);
                float lit=saturate(eyes);
                float2 e=(q-EyeCentre)/float2(1,0.75);
                float core=1-smoothstep(0,0.018+0.055*lit,length(e));
                float3 burn=lerp(Deep,float3(1,1,1),smoothstep(0.35,0.9,lit)*core);
                float3 hole=lerp(Navy,burn,core*saturate(lit*2.2));
                // The brow cuts the top of each hole, lower toward the nose.
                float brow=dot(q-float2(0.05,-0.1),float2(-0.208,0.978));
                c=lerp(c,hole,1-smoothstep(-aa,aa,max(ellipse(q-EyeCentre,float2(0.08,0.046)),brow)));
                // The nose widens downward, with a small notch at its top.
                float nose=max(trapezoid(p-float2(0,-0.226),0.036,0.018,0.018)-0.01,0.011-length(p-float2(0,-0.198)));
                return lerp(c,Navy,1-smoothstep(-aa,aa,nose));
            }
            // Heats a colour through the palette's blue to white, so a flash never passes through grey.
            float3 heat(float3 c, float flash)
            {
                return flash<0.5?lerp(c,Deep,flash*2):lerp(Deep,float3(1,1,1),flash*2-1);
            }
            // A four pointed star of light.
            float sparkle(float2 p, float size)
            {
                p=abs(p);
                return (min(p.x*4+p.y,p.y*4+p.x)-size)*0.24;
            }

            struct Pose
            {
                float ring;           // how far round the ring is drawn
                float ringScale;
                float skull;          // skull scale, overshooting as it pops in
                float2 skullShift;
                float squash;
                float centre, side;   // how far each dagger is driven at the skull; negative when drawn back
                float centreAlpha, sideAlpha;
                float eyes, flash, spark, wash;
            };

            // The whole seal from back to front: a navy wash, the spiked ring, the daggers, then the skull, so a
            // dagger driven into the skull vanishes behind it. Its eyes light up and sparks fly where the blades bite.
            float4 emblem(float2 p, float px, float2 widths, Pose o)
            {
                float aa=px*0.75;
                float3 white=1;
                float3 metal=heat(Navy,o.flash);
                float4 col=float4(Navy,1)*o.wash*(1-smoothstep(-aa,aa,length(p)-o.ringScale));
                col=over(col,paint(ringShape(p/o.ringScale,o.ring)*o.ringScale,metal,px,widths));
                float2 q=float2(abs(p.x),p.y);
                if(o.centreAlpha>0)
                    col=over(col,paint(dagger(q,CentreTip-o.centre,false),metal,px,widths)*o.centreAlpha);
                if(o.sideAlpha>0)
                {
                    float2 along=float2(dot(q,float2(SideAxis.y,-SideAxis.x)),dot(q,SideAxis));
                    col=over(col,paint(dagger(along,SideTip-o.side,true),metal,px,widths)*o.sideAlpha);
                }
                if(o.skull>0.01)
                {
                    float2 scale=float2(o.skull,o.skull*o.squash);
                    float2 local=(p-o.skullShift)/scale;
                    float unit=min(scale.x,scale.y);
                    float3 bone=skullColor(local,o.eyes,aa/unit);
                    col=over(col,paint(skullShape(local)*unit,lerp(bone,white,o.flash*0.7),px,widths));
                    // The burning eyes light the bone around them.
                    float2 e=(float2(abs(local.x),local.y)-EyeCentre)/float2(1,0.75);
                    float light=o.eyes*o.eyes*exp2(-dot(e,e)*55)*saturate(o.skull);
                    col.rgb+=lerp(Deep,white,0.4)*light;
                    col.a=saturate(col.a+light*0.45);
                }
                if(o.spark>0)
                {
                    float size=0.09*o.spark;
                    float d=min(sparkle(p-o.skullShift-CentreEntry,size),sparkle(q-o.skullShift-SideEntry,size));
                    col=over(col,paint(d,white,px,float2(0,widths.y)));
                }
                return col;
            }

            // Once a second of a brand's loop: how far its daggers are driven at the skull, striking it at c = 0.
            // They stay buried a moment, work free, rest, are drawn back, then strike again.
            float stab(float c)
            {
                const float Buried=0.13, Drawn=0.07;
                if(c<0.1) return Buried-0.012*sin(c/0.1*UNITY_PI);
                if(c<0.42) return Buried*(1-easeInOut((c-0.1)/0.32));
                if(c<0.6) return 0;
                if(c<0.9) return -Drawn*easeInOut((c-0.6)/0.3);
                return lerp(-Drawn,Buried,sq((c-0.9)/0.1));
            }

            // The seal cast on the ground. The ring is drawn, the skull pops up in its middle and the daggers fly
            // in over it while the chains bind its victims. Closing, the daggers are drawn back and driven into the
            // skull, whose eyes flare as the chains carry the brand off; then the seal sinks away.
            float4 groundSeal(float2 p, float px, float2 widths)
            {
                float t=_Phase, inscribe=saturate(_Inscribe), close=saturate(_Close);
                float grown=saturate((inscribe-0.35)/0.3);
                float centreIn=saturate((inscribe-0.55)/0.3), sideIn=saturate((inscribe-0.62)/0.3);
                float hover=0.012*sin(t*TAU*0.75)*(1-saturate(close*8));
                float drive=close<0.25?-0.08*easeInOut(close/0.25):lerp(-0.08,0.13,sq(saturate((close-0.25)/0.05)));
                float struck=step(0.3,close);
                float since=max(close-0.3,0)*CloseSeconds;
                float jolt=struck*exp2(-since*12);
                float sink=smoothstep(0.55,1,close);
                Pose o;
                o.ring=easeInOut(inscribe/0.5);
                o.ringScale=1+0.02*jolt-0.06*sink;
                o.skull=easeOutBack(grown);
                o.skullShift=float2(0,-0.02*jolt);
                o.squash=1-0.05*jolt;
                o.centre=-0.35*sq(1-centreIn)+hover+drive;
                o.side=-0.35*sq(1-sideIn)+hover+drive;
                o.centreAlpha=smoothstep(0,0.4,centreIn);
                o.sideAlpha=smoothstep(0,0.4,sideIn);
                o.eyes=lerp(grown*(0.2+0.06*sin(t*TAU*0.8)),1,struck*exp2(-since*2.5));
                o.flash=0.75*jolt;
                o.spark=struck*sin(UNITY_PI*saturate(since/0.16));
                o.wash=0.16*smoothstep(0,0.5,inscribe);
                return emblem(p,px,widths,o)*(1-sink);
            }

            // The chain binding a victim, from the seal (uv.x 0) to the victim (uv.x 1). Links run out as it grows.
            // Closing, a pulse of light runs down it to the victim over the first half, then it falls apart link by
            // link from the seal outward.
            float4 chain(float2 uv, float px)
            {
                float L=max(_Length,0.3);
                float s=uv.x*L;
                float y=(uv.y-0.5)*ChainWidth;
                float close=saturate(_Close);
                float reach=saturate(_Inscribe)*L;
                float pulse=saturate(close/0.5)*L;
                float dissolve=saturate((close-0.5)/0.5)*(L+0.3);
                // A slight sway, pinned at both ends.
                y-=0.04*sin(saturate(s/L)*UNITY_PI)*sin(s*2.3-_Phase*4+_Seed);
                // Face-on links alternate with links seen edge-on, each reaching into its neighbours.
                const float Pitch=0.4;
                float faces=1e3, edges=1e3;
                float nearest=floor(s/Pitch);
                [unroll] for(int k=-1;k<=1;k++)
                {
                    float n=nearest+k;
                    float at=(n+0.5)*Pitch;
                    float size=saturate((reach-at)/0.3)*(1-saturate((dissolve-at)/0.3));
                    if(n<0||at>L-0.12||size<0.01) continue;
                    float2 local=float2(s-at,y)/size;
                    float2 bar=float2(local.x-clamp(local.x,-0.1,0.1),local.y);
                    if(fmod(n,2)<0.5) faces=min(faces,(abs(length(bar)-0.1)-0.026)*size);
                    else edges=min(edges,(length(float2(local.x-clamp(local.x,-0.19,0.19),local.y))-0.034)*size);
                }
                float light=exp2(-sq((s-pulse)/0.3))*step(0.001,close)*(1-saturate((close-0.5)/0.15));
                float3 fill=lerp(Navy,float3(1,1,1),light);
                return over(paint(faces,fill,px,ChainLine),paint(edges,fill,px,ChainLine));
            }

            // The seal branded over a victim. It stamps down white hot (MaliciousSealMapComponent shrinks its quad onto
            // the pawn and lands it at 0.55 of _Inscribe), cools to its colours, then bobs over the pawn while its
            // daggers stab the skull once a second, lighting its eyes. Fading, the daggers work free and the eyes go out.
            float4 brand(float2 p, float px, float2 widths)
            {
                float t=_Phase, stamp=saturate(_Inscribe), fade=saturate(_Close);
                p.y-=0.03*sin(t*UNITY_PI);
                float c=frac(t+0.45);
                float jolt=exp2(-c*12);
                Pose o;
                o.ring=1;
                o.ringScale=1+0.025*exp2(-c*10);
                o.skull=1;
                o.skullShift=float2(0,-0.022*jolt);
                o.squash=1-0.05*jolt;
                o.centre=stab(c)-0.25*fade;
                o.side=o.centre;
                o.centreAlpha=1;
                o.sideAlpha=1;
                o.eyes=(0.18+0.82*exp2(-c*8))*(1-fade);
                o.flash=stamp<0.55?0.6+0.4*sq(stamp/0.55):exp2(-sq((stamp-0.55)/0.2)*3);
                o.spark=sin(UNITY_PI*saturate(c/0.16))*(1-fade);
                o.wash=0;
                return emblem(p,px,widths,o)*smoothstep(0,0.3,stamp)*(1-fade);
            }

            float4 frag(v2f i) : SV_Target
            {
                // Emblem units and chain cells, and one screen pixel in each, measured outside any branch.
                float2 e=(i.uv-0.5)*2*Extent;
                float2 c=float2(i.uv.x*max(_Length,0.3),i.uv.y*ChainWidth);
                float pxE=sqrt(max(dot(ddx(e),ddx(e)),dot(ddy(e),ddy(e))));
                float pxC=sqrt(max(dot(ddx(c),ddx(c)),dot(ddy(c),ddy(c))));
                float cells=max(_Size,0.01)/(2*Extent);
                bool strip=_Mode>0.5&&_Mode<1.5;
                float4 col;
                if(_Mode<0.5) col=groundSeal(e,pxE,GroundLine/cells);
                else if(strip) col=chain(i.uv,pxC);
                else col=brand(e,pxE,BrandLine/cells);
                // The seal fades on a circle and the chain at its sides, so a glow never shows the quad's outline.
                float2 edge=abs(i.uv-0.5)*2;
                col*=1-smoothstep(0.93,1,strip?edge.y:length(edge));
                return col*_Opacity;
            }
            ENDCG
        }
    }
    Fallback Off
}
