Shader "Bernael/MaliciousSeal"
{
    Properties
    {
        _Phase ("Seconds since branding, or since the chain was thrown", Float) = 0
        _Inscribe ("Chain growth or stamp progress", Range(0,1)) = 1
        _Close ("Chain release or fading progress", Range(0,1)) = 0
        _Opacity ("Opacity", Range(0,1)) = 1
        _Seed ("Seed", Float) = 0
        _Mode ("Chain (1) or brand (2)", Float) = 2
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

            // Dire Orb's and Dark Mirage's palette: the concept's black emblem becomes navy and its grey skull pale
            // blue bone, inside the same solid white contour and soft white glow.
            static const float3 Navy = float3(0.086,0.133,0.169);
            static const float3 Deep = float3(0.51,0.71,0.859);
            static const float3 Pale = float3(0.725,0.851,0.929);
            // The emblem is measured in its ring's radius, and its quad reaches this far from the middle.
            // MaliciousSealMapComponent sizes the brand by it.
            static const float Extent = 1.75;
            // Contour and glow widths in cells, thinner than Dire Orb's since the brand is small.
            static const float2 BrandLine = float2(0.018,0.03);
            static const float2 ChainLine = float2(0.022,0.04);
            // MaliciousSealMapComponent.ChainWidth, the chain quad's width in cells.
            static const float ChainWidth = 0.8;
            // Shapes measured from the concept, with the ring's radius as 1 and +y up.
            static const float RingHalf = 0.055;
            // Its diagonal spikes lean 47.5 degrees off the vertical and its side daggers 50: (sin, cos) of each.
            static const float2 DiagonalAxis = float2(0.7373,0.6756);
            static const float2 SideAxis = float2(0.7660,0.6428);
            // How far each dagger's point rests from the middle, just clear of the skull, and the skull's scale.
            static const float CentreTip = 0.37;
            static const float SideTip = 0.39;
            static const float SkullScale = 1.12;
            // The right eye socket's middle.
            static const float2 EyeCentre = float2(0.115,-0.085);

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

            // The concept's ring with its eight spikes: short ones on the axes, longer ones on the diagonals.
            float ringShape(float2 p)
            {
                float d=abs(length(p)-1)-RingHalf;
                // Folded into one quadrant, so each line draws a spike on every side.
                float2 q=abs(p);
                d=min(d,kite(q,0.84,1,1.21,0.1));
                d=min(d,kite(q.yx,0.84,1,1.21,0.1));
                float2 slanted=float2(dot(q,float2(DiagonalAxis.y,-DiagonalAxis.x)),dot(q,DiagonalAxis));
                return min(d,kite(slanted,0.9,1,1.32,0.1));
            }

            // A dagger along +y, its point tip from the middle: a straight blade widening to the crossguard, a short
            // grip and a round pommel, so it still reads as a dagger when the brand is small.
            float dagger(float2 p, float tip)
            {
                p.y-=tip;
                float d=cone(p,float2(0,0.016),float2(0,0.22),0.014,0.064);
                d=min(d,box(p-float2(0,0.245),float2(0.1,0.025),0.014));
                d=min(d,box(p-float2(0,0.31),float2(0.028,0.05),0.01));
                return min(d,length(p-float2(0,0.38))-0.042);
            }

            // A plain skull: a round crown over square cheekbones and a narrower jaw.
            float skullShape(float2 p)
            {
                float d=length(p-float2(0,0.02))-0.28;
                d=smin(d,box(p-float2(0,-0.14),float2(0.215,0.1),0.07),0.05);
                return smin(d,box(p-float2(0,-0.29),float2(0.135,0.085),0.045),0.04);
            }
            // Bone deepening toward the jaw, with scowling navy eye sockets in which an ember burns, a small nose and
            // three gaps between the teeth. As eyes rises to 1 the ember fills its socket and turns white.
            float3 skullColor(float2 p, float eyes, float aa)
            {
                float2 q=float2(abs(p.x),p.y);
                float3 c=lerp(Deep,Pale,smoothstep(-0.38,0.1,p.y));
                // Each socket is cut flat by a brow sloping down toward the nose.
                float socket=max(ellipse(q-EyeCentre,float2(0.088,0.075)),dot(q-float2(0.03,-0.06),float2(-0.316,0.949)));
                float lit=saturate(eyes);
                float core=1-smoothstep(0,0.02+0.05*lit,length(q-EyeCentre+float2(0,0.008)));
                float3 burn=lerp(Deep,float3(1,1,1),smoothstep(0.35,0.9,lit)*core);
                float3 hole=lerp(Navy,burn,core*saturate(lit*2.2));
                c=lerp(c,hole,1-smoothstep(-aa,aa,socket));
                // The nose narrows to a point at the top.
                float nose=trapezoid(p-float2(0,-0.2),0.042,0.008,0.036);
                float teeth=max(max(abs(q.x-round(q.x/0.065)*0.065)-0.011,q.x-0.1),p.y+0.275);
                return lerp(c,Navy,1-smoothstep(-aa,aa,min(nose,teeth)));
            }
            // Heats a colour through the palette's blue to white, so a flash never passes through grey.
            float3 heat(float3 c, float flash)
            {
                return flash<0.5?lerp(c,Deep,flash*2):lerp(Deep,float3(1,1,1),flash*2-1);
            }

            struct Pose
            {
                float ringScale;
                float2 skullShift;
                float squash;
                float stab;           // how far the daggers are driven at the skull; negative when drawn back
                float eyes, flash;
            };

            // The ring and daggers under one fill and contour, then the skull over them, so a dagger driven into the
            // skull vanishes behind it. Its eyes light up as the blades bite.
            float4 emblem(float2 p, float px, float2 widths, Pose o)
            {
                float aa=px*0.75;
                float3 white=1;
                float2 q=float2(abs(p.x),p.y);
                float2 along=float2(dot(q,float2(SideAxis.y,-SideAxis.x)),dot(q,SideAxis));
                float metal=ringShape(p/o.ringScale)*o.ringScale;
                metal=min(metal,dagger(q,CentreTip-o.stab));
                metal=min(metal,dagger(along,SideTip-o.stab));
                float4 col=paint(metal,heat(Navy,o.flash),px,widths);
                float unit=o.squash*SkullScale;
                float2 local=(p-o.skullShift)/(float2(1,o.squash)*SkullScale);
                float3 bone=skullColor(local,o.eyes,aa/unit);
                col=over(col,paint(skullShape(local)*unit,lerp(bone,white,o.flash*0.7),px,widths));
                // The burning eyes light the bone around them.
                float2 e=(float2(abs(local.x),local.y)-EyeCentre)/float2(1,0.75);
                float light=o.eyes*o.eyes*exp2(-dot(e,e)*55);
                col.rgb+=lerp(Deep,white,0.4)*light;
                col.a=saturate(col.a+light*0.45);
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

            // The chain the seal throws from a sealed corpse (uv.x 0) to the pawn it spreads to (uv.x 1). Links run
            // out as it grows; then a pulse of light runs down it to the victim over the first half of its release,
            // and it falls apart link by link from the corpse outward.
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

            // The seal branded over a victim's head. It stamps down white hot (MaliciousSealMapComponent shrinks its
            // quad onto the pawn and lands it at 0.55 of _Inscribe), cools to its colours, then bobs while its daggers
            // stab the skull once a second, lighting its eyes. Fading, the daggers work free and the eyes go out.
            float4 brand(float2 p, float px, float2 widths)
            {
                float t=_Phase, stamp=saturate(_Inscribe), fade=saturate(_Close);
                p.y-=0.03*sin(t*UNITY_PI);
                float c=frac(t+0.45);
                float jolt=exp2(-c*12);
                Pose o;
                o.ringScale=1+0.025*exp2(-c*10);
                o.skullShift=float2(0,-0.022*jolt);
                o.squash=1-0.05*jolt;
                o.stab=stab(c)-0.15*fade;
                o.eyes=(0.18+0.82*exp2(-c*8))*(1-fade);
                o.flash=stamp<0.55?0.6+0.4*sq(stamp/0.55):exp2(-sq((stamp-0.55)/0.2)*3);
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
                bool strip=_Mode<1.5;
                float4 col=strip?chain(i.uv,pxC):brand(e,pxE,BrandLine/cells);
                // The brand fades on a circle and the chain at its sides, so a glow never shows the quad's outline.
                float2 edge=abs(i.uv-0.5)*2;
                col*=1-smoothstep(0.93,1,strip?edge.y:length(edge));
                return col*_Opacity;
            }
            ENDCG
        }
    }
    Fallback Off
}
