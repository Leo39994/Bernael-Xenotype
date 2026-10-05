Shader "Bernael/DireOrb"
{
    Properties
    {
        _Phase ("Seconds since launch, or since impact for the burst", Float) = 0
        _Opacity ("Opacity", Range(0,1)) = 1
        _Seed ("Seed", Float) = 0
        _Mode ("Orb / trail / burst", Float) = 0
        _Direction ("Travel direction on the quad", Vector) = (1,0,0,0)
        _Length ("Trail length in cells", Float) = 2.6
        _Travel ("Cells travelled", Float) = 0
        _Charge ("Launch growth", Range(0,1)) = 1
        _Landed ("Seconds since the orb landed, 0 in flight", Float) = 0
        _Radius ("Burst radius in cells", Float) = 2.6
    }
    SubShader
    {
        // Queued with vanilla's glowing motes (3151): after the map's lighting overlay (3100), so the cold fire keeps
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
            float _Phase, _Opacity, _Seed, _Mode, _Length, _Travel, _Charge, _Landed, _Radius;
            float4 _Direction;

            static const float TAU = 6.2831853;
            // Cold flame drawn like Dark Mirage's: pale blue deepening toward its base, a navy heart,
            // a solid white contour and a soft white glow. The colours are sampled from the reference art.
            static const float3 Navy = float3(0.086,0.133,0.169);
            static const float3 Deep = float3(0.51,0.71,0.859);
            static const float3 Pale = float3(0.725,0.851,0.929);
            // Sizes are in map cells. The contour is as thick as Dark Mirage's.
            static const float Stroke = 0.036;
            static const float Halo = 0.055;
            // Half of Projectile_DireOrb.OrbSize, its TrailWidth, and how far past the blast radius
            // DireOrbMapComponent's burst quad reaches, so flames at the edge fit with their glow.
            static const float OrbCells = 2.0;
            static const float TrailWidth = 1.2;
            static const float BurstMargin = 1.9;
            // Radius of the flying orb's head, the bowl of its flame.
            static const float HeadR = 0.40;
            // Seconds from impact until the orb bursts, matching DireOrbMapComponent.DetonateTicks.
            static const float Collapse = 0.30;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
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
            // Union that rounds the crease between two shapes over k: wide where a tongue flows out of the
            // bowl, tight where two tongues meet in the reference's sharp notches.
            float smin(float a, float b, float k)
            {
                float h=saturate(0.5+0.5*(b-a)/k);
                return lerp(b,a,h)-k*h*(1-h);
            }
            float4 over(float4 back, float4 front)
            {
                return front+back*(1-front.a);
            }
            // Distance to a tapered segment: a circle of radius ra at a, joined by tangents to one of radius rb at b.
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
            // A flame tongue from base toward dir, len long and width in radius at its root, tapering to a
            // sharp tip. It leans sideways by bend and curl hooks its tip, giving the S of the reference flames.
            float tongue(float2 p, float2 base, float2 dir, float len, float width, float bend, float curl)
            {
                float2 side=float2(dir.y,-dir.x)*len;
                float2 along=dir*len;
                float2 a=base+along*0.25+side*(bend*0.4375+curl*0.0156);
                float2 b=base+along*0.5+side*(bend*0.75+curl*0.125);
                float2 c=base+along*0.75+side*(bend*0.9375+curl*0.4219);
                float2 tip=base+along+side*(bend+curl);
                float d=cone(p,base,a,width,width*0.92);
                d=min(d,cone(p,a,b,width*0.92,width*0.75));
                d=min(d,cone(p,b,c,width*0.75,width*0.45));
                return min(d,cone(p,c,tip,width*0.45,0));
            }
            // One lane of fire: two tongues in turn stretch out of base along dir, pinch off at the root and
            // shrink away as loose wisps, each with its own reach, lean and hooked tip.
            float lane(float2 p, float2 base, float2 dir, float len, float width, float lean, float seed, float time)
            {
                float d=1e3;
                float rate=2.5+0.9*randomCell(float2(seed,5.3));
                [unroll] for(int k=0;k<2;k++)
                {
                    float tick=time*rate+seed+k*0.5;
                    float cycle=floor(tick), age=frac(tick);
                    float rnd=randomCell(float2(cycle,seed+k*13.7));
                    float rnd2=randomCell(float2(cycle+9.1,seed-k*3.1));
                    float l=len*(0.85+0.3*rnd);
                    // Stretches out until 0.55; then its root climbs after the tip until only a wisp a third
                    // as wide is left, which shrinks away about its middle.
                    float loose=saturate((age-0.55)/0.45);
                    float pinch=smoothstep(0,0.5,loose);
                    float tip=l*(0.35+0.65*saturate(age/0.55)+0.2*loose);
                    float w=width*(0.85+0.3*rnd2)*smoothstep(0,0.2,age)*lerp(1,0.28,pinch);
                    float piece=lerp(tip,3.2*w,pinch);
                    float fade=1-smoothstep(0.55,1,loose);
                    float mid=tip-piece*0.5;
                    w*=fade;
                    if(w>1e-3) d=min(d,tongue(p,base+dir*(mid-piece*0.5*fade),dir,max(piece*fade,1e-3),w,
                        lean+(rnd-0.5)*0.4,(rnd2-0.5)*0.6-lean*1.5));
                }
                return d;
            }

            // Distances in cells to a flame's outline and to its navy heart, and how far up the flame the
            // point lies, which deepens its blue toward the base.
            struct Fire { float flame; float heart; float height; };

            // Paints one flame: its fill deepens toward the base as in the reference, the navy heart sits
            // inside, the white contour lies outside the fill and a soft white glow outside that.
            // px is one screen pixel in cells, which keeps the contour at least a pixel and a half wide.
            float4 paint(Fire fire, float px)
            {
                float aa=px*0.75;
                float stroke=max(Stroke,px*1.5);
                float fill=1-smoothstep(-aa,aa,fire.flame);
                float rim=1-smoothstep(-aa,aa,fire.flame-stroke);
                float heart=1-smoothstep(-aa,aa,fire.heart);
                float depth=1-pow(saturate(1-fire.height/0.65),1.5);
                float3 c=lerp(lerp(Deep,Pale,depth),Navy,heart);
                c=lerp(float3(1,1,1),c,fill);
                float g=max(fire.flame-stroke,0);
                float glow=0.45*exp2(-g*g/(Halo*Halo)*1.4427)*(1-smoothstep(Halo*1.8,Halo*2.8,g))*(1-rim);
                return float4(c*rim+glow,rim+glow);
            }

            // A flame in the reference's shape, in units of its bowl's half width from the bowl's middle,
            // rising along +y: a wide, flattened bowl with a horn flicking up out of each shoulder, a broad main
            // tongue with a branch off either side, and a navy heart low in it that is a smaller flame of its
            // own, a scooped bowl with a slender spire. squash flattens the bowl and reach scales the tongues.
            Fire flame(float2 p, float time, float seed, float reach, float squash)
            {
                Fire o;
                float side=randomCell(float2(seed,2.7))<0.5?-1:1;
                // A slow sway with a quicker flutter on top, so the flame never settles.
                float sway=softNoise(float2(time*1.6,seed))-0.5+0.5*(softNoise(float2(time*3.7,seed+4.4))-0.5);
                float lean=side*0.18+sway*0.45;
                float grow=sqrt(reach);
                // The main tongue is a broad column swaying in an S; its tip whips about and flickers in length.
                float2 up=normalize(float2(sway*0.45,1));
                float2 across=float2(up.y,-up.x);
                float curl=-lean*1.6+(softNoise(float2(time*2.9,seed+7.7))-0.5)*0.9;
                float len=max(2.4*reach*(0.85+0.3*softNoise(float2(time*2.6,seed+1.9))),1e-3);
                // Halfway up it and its tip, on the curve tongue() draws for this lean and curl.
                float2 middle=up*len*0.5+across*len*(lean*0.75+curl*0.125);
                float2 top=up*len+across*len*(lean+curl);
                o.flame=(length(p/float2(1,squash))-1)*squash;
                [unroll] for(int h=0;h<2;h++)
                {
                    float s=h*2-1;
                    float bob=softNoise(float2(time*2.6,seed+h*5.1))-0.5;
                    o.flame=smin(o.flame,tongue(p,float2(s*0.82,0.15),normalize(float2(s*0.9,1)),max(0.8*reach*(0.8+0.4*(bob+0.5)),1e-3),
                        0.24*grow,s*(0.1+bob*0.55),-s*0.5-bob*0.4),0.1);
                }
                // The column flows out of the bowl, and tongues growing out of its middle stretch past its tip
                // and tear loose there. The branches meet it in sharp notches.
                o.flame=smin(o.flame,tongue(p,float2(0,0),up,len,0.52*grow,lean,curl),0.25);
                o.flame=smin(o.flame,lane(p,middle,normalize(top-middle),length(top-middle)*1.15,0.3*grow,-lean*0.5,seed,time),0.08);
                o.flame=smin(o.flame,lane(p,float2(side*0.28,0.75),normalize(float2(side*0.7,1)),1.1*reach,0.28*grow,
                    side*0.2,seed+3.1,time),0.04);
                o.flame=smin(o.flame,lane(p,float2(-side*0.22,1.25),normalize(float2(-side*0.6,1)),0.9*reach,0.22*grow,
                    -side*0.2,seed+6.3,time),0.04);
                float bowl=max((length((p-float2(0,-0.12))/float2(0.74,0.5))-1)*0.5,0.46-length(p-float2(0,0.52)));
                // The heart's spire follows the column's S up its middle and flows smoothly out of the bowl.
                float spire=tongue(p,float2(0,-0.2),up,len*0.85,0.3*grow,lean,curl);
                o.heart=max(smin(bowl,spire,0.2),o.flame+0.15);
                o.height=(p.y+squash)/(squash+2.4*max(reach,0.3));
                return o;
            }

            float4 orb(float2 p, float px)
            {
                float time=_Phase+_Seed;
                float landed=_Landed;
                // Once landed, the tongues are dragged back into the head, leaving a stub of flame still
                // writhing, and the orb tightens and shudders until it bursts.
                float reach=lerp(1,0.35,smoothstep(0,0.2,landed));
                float strain=smoothstep(0.1,Collapse,landed);
                float scale=max(smoothstep(0,1,_Charge),0.04)*lerp(1,0.85,strain);
                float2 shake=(float2(softNoise(float2(time*40,_Seed)),softNoise(float2(_Seed,time*40)))-0.5)*0.04*strain;
                float2 dir=normalize(_Direction.xy+float2(1e-4,0));
                float2 q=(p-shake)/(scale*HeadR);
                // The orb is a flame lying along its path, its bowl in front and its tongues streaming back.
                // Its bowl rounds out as it collapses.
                Fire fire=flame(float2(dot(q,float2(-dir.y,dir.x)),-dot(q,dir)),time,_Seed,reach,lerp(0.85,1,strain));
                fire.flame*=scale*HeadR;
                fire.heart*=scale*HeadR;
                // Loose flames are drawn in from all round and swallowed.
                if(landed>0)
                {
                    [unroll] for(int m=0;m<6;m++)
                    {
                        float rnd=randomCell(float2(m,_Seed+4.2));
                        float t=saturate((landed-0.02-0.1*rnd)/0.16);
                        float angle=(m+rnd*0.7)/6*TAU;
                        float2 ray=float2(cos(angle),sin(angle));
                        float size=0.07*(1-t*t)*smoothstep(0,0.2,t);
                        if(size>1e-3) fire.flame=smin(fire.flame,tongue(p,ray*lerp(1.3,0.3,t*t),ray,size*3,size,(rnd-0.5)*0.6,0.3),0.05);
                    }
                }
                return paint(fire,px);
            }

            // Wisps torn off the orb's tongues stay where they tore loose and burn down to nothing.
            float4 trail(float2 uv, float px)
            {
                // Cells behind the orb, across its path, and from the launch point along it.
                float s=uv.x*max(_Length,0.3);
                float y=(uv.y-0.5)*TrailWidth;
                float world=_Travel-s;
                const float spacing=0.32;
                float slot=floor(world/spacing);
                // Once the orb lands they all burn out together.
                float linger=1-smoothstep(0,0.25,_Landed);
                Fire fire;
                fire.flame=1e3;
                fire.heart=1e3;
                fire.height=0.8;
                [unroll] for(int k=-1;k<=2;k++)
                {
                    float n=slot+k;
                    float rnd=randomCell(float2(n,_Seed+3.1));
                    float rnd2=randomCell(float2(n+7.7,_Seed));
                    float rnd3=randomCell(float2(n+2.9,_Seed+5.5));
                    float at=(n+0.15+0.7*rnd2)*spacing;
                    float behind=_Travel-at;
                    float life=saturate((behind-0.9)/1.4);
                    // None behind the launch point, and smaller ones while the orb was still growing.
                    float size=0.075*(0.7+0.6*rnd)*smoothstep(0,0.15,life)*(1-smoothstep(0.3,1,life))*linger
                        *step(0.35,randomCell(float2(n+5.3,_Seed+1.7)))*saturate(at/2);
                    float across=(rnd3-0.5)*0.45;
                    if(size>1e-3) fire.flame=smin(fire.flame,tongue(float2(s,y),float2(behind,across),normalize(float2(1,across)),
                        size*3,size,(rnd-0.5)*0.4,(rnd2-0.5)*0.8),0.03);
                }
                return paint(fire,px);
            }

            // One flame of the burst standing upright on the ground at foot, in cells.
            Fire standing(float2 p, float2 foot, float size, float squash, float time, float seed, float reach)
            {
                Fire fire=flame((p-foot)/size-float2(0,squash),time,seed,reach,squash);
                fire.flame*=size;
                fire.heart*=size;
                return fire;
            }

            // The orb bursts: pale fire blows out of it with broad tongues all round, then falls back into a tall
            // flame rising in its place, while blobs of fire flung out of the burst light flames where they land
            // all over the blast. Each burns up, sheds its tongues and sinks away. Flames are drawn from the back
            // row to the front one, so nearer flames overlap farther ones like the reference's.
            float4 burst(float2 p, float px)
            {
                float boom=_Phase-Collapse;
                if(boom<0) return 0;
                float R=_Radius;
                float r=length(p);
                float4 centre=0;
                if(r<3)
                {
                    // The orb's own flame grows out of the collapsed orb, as wide as its head, and sinks last.
                    float rise=smoothstep(0,0.18,boom);
                    Fire fire=standing(p,float2(0,-HeadR*0.85),lerp(HeadR*0.85,0.55,rise)*(1-smoothstep(0.9,1.3,boom)),
                        lerp(1,0.72,rise),boom*1.2+_Seed,_Seed,lerp(0.35,1,rise)*(1-smoothstep(0.8,1.2,boom)));
                    // The blast, out and back in a quarter second. It is already bursting on the first frame, so the
                    // collapsed orb never shows on its own.
                    float blast=smoothstep(-0.03,0.06,boom)*(1-smoothstep(0.08,0.26,boom));
                    if(blast>0)
                    {
                        float reach=lerp(HeadR*0.85,1.05,blast);
                        float d=r-reach;
                        const float rays=9;
                        float nearest=floor(atan2(p.y,p.x)/TAU*rays+0.5);
                        [unroll] for(int k=-1;k<=1;k++)
                        {
                            float n=nearest+k;
                            float id=n-rays*floor(n/rays);
                            float rnd=randomCell(float2(id,_Seed+5.1));
                            float rnd2=randomCell(float2(id+2.6,_Seed+0.7));
                            float angle=(n+(rnd-0.5)*0.5)/rays*TAU;
                            float2 ray=float2(cos(angle),sin(angle));
                            d=smin(d,tongue(p,ray*reach*0.6,ray,reach*lerp(0.6,1.4,rnd2),reach*lerp(0.22,0.32,rnd),
                                (rnd-0.5)*0.9,(0.5-rnd2)*1.2),0.1);
                        }
                        fire.flame=smin(fire.flame,d,0.1);
                        // Deep at its middle and pale at its tips, like a flame spread flat.
                        fire.height=lerp(fire.height,r/1.6,blast);
                    }
                    // Blobs of fire flung out of the burst, part of it until they fly clear.
                    const float blobs=12;
                    float nearestBlob=floor(atan2(p.y,p.x)/TAU*blobs);
                    [unroll] for(int b=-1;b<=1;b++)
                    {
                        float n=nearestBlob+b;
                        float id=n-blobs*floor(n/blobs);
                        float rnd=randomCell(float2(id,_Seed+8.8));
                        float rnd2=randomCell(float2(id+3.3,_Seed+1.1));
                        float angle=(n+0.2+0.6*rnd)/blobs*TAU;
                        float2 ray=float2(cos(angle),sin(angle));
                        float land=lerp(1.0,R*0.9,rnd2);
                        float size=lerp(0.07,0.1,rnd)*smoothstep(0,0.05,boom)*(1-smoothstep(land/9,land/9+0.1,boom));
                        if(size>1e-3) fire.flame=smin(fire.flame,tongue(p,ray*(0.3+min(boom*9,land-0.3)),-ray,size*3.2,size,
                            (rnd-0.5)*0.5,0),0.03);
                    }
                    centre=paint(fire,px);
                }
                float4 col=0;
                bool drawn=false;
                float top=floor(p.y-0.12);
                float left=floor(p.x-1.3);
                [loop] for(int row=0;row<4;row++)
                {
                    float j=top-row;
                    if(!drawn&&j<0)
                    {
                        col=over(col,centre);
                        drawn=true;
                    }
                    [loop] for(int column=0;column<3;column++)
                    {
                        float2 cell=float2(left+column,j);
                        float rnd=randomCell(cell+_Seed*1.7);
                        float rnd2=randomCell(cell.yx+_Seed*2.3+7.1);
                        float2 foot=cell+0.3+0.4*float2(rnd,rnd2);
                        float dist=length(foot);
                        // None inside the orb's own flame, none whose body stands outside the blast, since a
                        // flame rises above its foot, and fewer toward the edge.
                        float body=length(foot+float2(0,0.4));
                        if(dist<0.95||body>R*0.92||randomCell(cell+_Seed*3.9+2.2)>lerp(0.95,0.65,body/R)) continue;
                        // Lit as the flung fire lands, a flame burns for most of a second.
                        float local=boom-dist/9-rnd*0.05;
                        float life=lerp(0.7,1.0,rnd2);
                        if(local<0||local>life) continue;
                        float size=lerp(0.26,0.36,rnd)*lerp(1,0.65,body/R)*lerp(0.5,1,smoothstep(0,0.1,local))
                            *(1-smoothstep(life-0.2,life,local));
                        // Its horns, branches and glow reach no further.
                        float2 q=p-foot;
                        if(abs(q.x)>size*1.5+0.18||q.y<-0.18||q.y>size*4.4+0.18) continue;
                        float reach=smoothstep(0,0.12,local)*(1-smoothstep(life-0.3,life,local));
                        col=over(col,paint(standing(p,foot,size,0.72,boom*1.3+rnd*9,rnd*31+_Seed,reach),px));
                    }
                }
                if(!drawn) col=over(col,centre);
                return col;
            }

            float4 frag(v2f i) : SV_Target
            {
                // The quad's size in cells, and one screen pixel in cells, measured outside any branch.
                bool strip=_Mode>0.5&&_Mode<1.5;
                float2 cells=strip?float2(max(_Length,0.3),TrailWidth):(_Mode>1.5?(_Radius+BurstMargin)*2:OrbCells*2);
                float2 dx=ddx(i.uv)*cells, dy=ddy(i.uv)*cells;
                float px=sqrt(max(dot(dx,dx),dot(dy,dy)));
                float4 col;
                if(_Mode>1.5) col=burst((i.uv-0.5)*cells,px);
                else if(strip) col=trail(i.uv,px);
                else col=orb((i.uv-0.5)*cells,px);
                // The trail fades at its quad edges; round effects fade on a circle,
                // so a stray glow never shows the square outline.
                float2 edge=abs(i.uv-0.5)*2;
                float bound=strip?max(edge.x,edge.y):length(edge);
                col*=1-smoothstep(0.90,0.99,bound);
                return col*_Opacity;
            }
            ENDCG
        }
    }
    Fallback Off
}
