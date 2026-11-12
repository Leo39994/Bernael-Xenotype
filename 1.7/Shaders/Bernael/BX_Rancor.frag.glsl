#version 450
// Rancor of Souls in the souls' flat cyan palette (Mortis's soul art: pale, mid and deep cyan bands).
// Mode 0: the volley's last contraction and its release burst. 1: a soul. 2: one segment of a soul's trail.
// 3: a soul striking. 4: the cast warmup, the reservoir stirring inside the caster.
// Everything but the souls themselves stays translucent and low in contrast (Mortis, 2026-11-09).
layout(location = 0) in vec2 vUv;
layout(location = 0) out vec4 outColor;
#include "BX_ShaderCommon.glsl"
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    float _Phase; // @default = 0.0
    float _Mode; // @default = 0.0
    float _Opacity; // @default = 1.0
    float _Angle; // @default = 0.0
    float _Souls; // @default = 20.0
    float _Seed; // @default = 0.0
    float _U0; // @default = 0.0
    float _U1; // @default = 1.0
    float _Length; // @default = 3.0
    float _Progress; // @default = 1.0
    float _Size; // @default = 12.0
    float _Gather; // @default = 1.0
    float _FaceVariant; // @default = 0.0
    float _AuthoredFace; // @default = 0.0
} uMaterial;
// Piloto Studio masks in alpha (Common/Textures/Effects/Piloto/SOURCES.json).
layout(set = 2, binding = 1) uniform sampler2D _Ghost;
layout(set = 2, binding = 2) uniform sampler2D _Wisp;
layout(set = 2, binding = 3) uniform sampler2D _Ring;
layout(set = 2, binding = 4) uniform sampler2D _Smoke;
layout(set = 2, binding = 5) uniform sampler2D _Rays;

const vec3 Light = vec3(0.678,0.984,1.0);
const vec3 Mid = vec3(0.467,0.773,0.788);
const vec3 Dark = vec3(0.267,0.698,0.722);
const vec3 Deep = vec3(0.05,0.24,0.27);
const vec3 Ink = vec3(0.016,0.075,0.09);
// The dark teal of a soul's eyes and mouth: clear on its pale face without turning into black dots.
const vec3 Socket = vec3(0.075,0.33,0.37);
const float PI = 3.14159265;
const float TAU = 6.2831853;

vec4 over(vec4 back, vec4 front) { return front+back*(1-front.a); }
float band(float x, float edge) { return bxSmoothstep(edge-0.035,edge+0.035,x); }
// A soft profile; squared directly, since pow() of a negative base is undefined.
float bell(float x) { return exp(-x*x); }
mat2 rotation(float a) { float c=cos(a), s=sin(a); return mat2(c,s,-s,c); }
float mask(sampler2D tex, vec2 uv) { return tex2D(tex,uv).a; }
float maskLod(sampler2D tex, vec2 uv, float lod) { return textureLod(tex,vec2(uv.x,1-uv.y),lod).a; }
// Radial masks: peak is where the texture's ring is densest, as a fraction of its half size; radius is where that
// ring should sit, in the same units as p. Sampled everywhere and masked afterwards: a sample that only some pixels
// take has no defined mip level.
float radialMask(sampler2D tex, vec2 p, float peak, float radius, float spin)
{
    vec2 uv=rotation(spin)*p/(2*radius/peak)+0.5;
    float inside=step(0.0,uv.x)*step(uv.x,1.0)*step(0.0,uv.y)*step(uv.y,1.0);
    return mask(tex,clamp(uv,vec2(0.0),vec2(1.0)))*inside;
}

float triangle(vec2 p, vec2 a, vec2 b, vec2 c)
{
    vec2 e0=b-a, e1=c-b, e2=a-c;
    vec2 v0=p-a, v1=p-b, v2=p-c;
    vec2 q0=v0-e0*saturate(dot(v0,e0)/dot(e0,e0));
    vec2 q1=v1-e1*saturate(dot(v1,e1)/dot(e1,e1));
    vec2 q2=v2-e2*saturate(dot(v2,e2)/dot(e2,e2));
    float s=sign(e0.x*e2.y-e0.y*e2.x);
    vec2 d=min(min(vec2(dot(q0,q0),s*(v0.x*e0.y-v0.y*e0.x)),vec2(dot(q1,q1),s*(v1.x*e1.y-v1.y*e1.x))),
        vec2(dot(q2,q2),s*(v2.x*e2.y-v2.y*e2.x)));
    return -sqrt(d.x)*sign(d.y);
}

// The souls wear the face of Mortis's Rancor of Souls icon: slanted eyes over a jagged mouth. Head coordinates: +x
// is the way the soul flies (the top of its head), y runs across it and the head's rim lies at length 1.
// Mortis (2026-11-11): the point of the soul variants is their eyes. Five pairs, every one under a brow that slopes
// down to the nose so that none of them looks friendly: the icon's wedges, hollow sockets after Mortis's sketch,
// narrow slits, half discs and slanted diamonds. Each soul keeps its face for its whole flight, including
// after loading a save.
float soulEyes(vec2 p, float variant)
{
    vec2 q=vec2(p.x,abs(p.y));
    // Along the brow, from its outer end down to the nose, and the side of it toward the top of the head.
    const vec2 Brow=vec2(-0.39,-0.92);
    const vec2 Above=vec2(0.92,-0.39);
    if(variant<0.5) return triangle(q,vec2(0.40,0.60),vec2(0.04,0.58),vec2(0.13,0.13));
    if(variant<1.5)
    {
        // One socket a little larger than the other, as in the sketch, but both slanted along the brow.
        vec2 c=q-vec2(0.22,0.38);
        vec2 r=vec2(0.27,0.15)*mix(0.86,1.0,step(0.0,p.y));
        return (length(vec2(dot(c,Brow),dot(c,Above))/r)-1)*r.y;
    }
    if(variant<2.5) return triangle(q,vec2(0.43,0.65),vec2(0.25,0.67),vec2(0.11,0.12));
    if(variant<3.5) return max(length(q-vec2(0.19,0.40))-0.25,dot(q-vec2(0.30,0.40),Above));
    vec2 c=q-vec2(0.22,0.38);
    return (abs(dot(c,Brow))/0.31+abs(dot(c,Above))/0.125-1)*0.11;
}

// The source mask's skull sockets, filled to the height of the head around them: along each of eight directions the
// brightest point within reach, and the third lowest of those, since the sockets open into the body's dark streaks
// in one or two directions. A hole inside the head is closed at the level of its rim. On the head's outline about
// half of the directions lead outside, so it keeps its own shading. g is in mask space, steps in head units.
float socketFill(vec2 g)
{
    float lowest=1.0, second=1.0, third=1.0;
    for(int i=0;i<8;i++)
    {
        float a=float(i)*PI*0.25;
        vec2 stride=vec2(-cos(a)*0.17,sin(a)*0.32)*0.1;
        float wall=0.0;
        for(int j=1;j<=7;j++) wall=max(wall,maskLod(_Ghost,clamp(g+stride*float(j),vec2(0.0),vec2(1.0)),1.5));
        third=min(third,max(second,wall));
        second=min(second,max(lowest,wall));
        lowest=min(lowest,wall);
    }
    return third;
}

// A jagged mouth with pointed ends, its corners turned down; the seed sets its width and its number of teeth.
float soulMouth(vec2 p, float seed)
{
    float width=0.46+0.06*sin(seed*3.7);
    float teeth=3.0+floor(fract(seed*0.618)*3.0);
    float wave=abs(fract(p.y*teeth/(2*width)+0.25)-0.5)*4-1;
    // Kept well inside the pale disc of the face, so its corners never meet the disc's edge.
    float centre=-0.37-0.08*p.y*p.y/(width*width)+0.085*wave;
    float slope=0.085*4*teeth/(2*width);
    float thick=0.078*bxSmoothstep(width,width-0.16,abs(p.y));
    // Closed off past its ends: the band thins to nothing there, and a band of no thickness still drew its centre
    // line as a hairline running on from each corner of the mouth to the edge of the face.
    return max((abs(p.x-centre)-thick)/sqrt(1+slope*slope),abs(p.y)-width);
}

// Flat cyan souls with a face at uv (0.78, 0.5) and a tail toward uv.x 0.
// The seed also varies proportions, tail rhythm and tint without consuming gameplay RNG.
vec4 ghost(vec2 uv, float t, float seed)
{
    float v1=fract(sin(seed*91.7+3.1)*43758.5453);
    float v2=fract(sin(seed*47.3+7.9)*24634.6345);
    float v3=fract(sin(seed*13.9+1.7)*35791.1357);
    uv.y=mix(uv.y,1-uv.y,step(0.5,v1));
    vec2 face=vec2(0.78,0.5);
    uv=face+(uv-face)*vec2(1+(v2-0.5)*0.24,1-(v2-0.5)*0.2);
    float tail=1-uv.x;
    uv.y+=sin(uv.x*(7+4*v3)-t*(13+6*v2)+seed)*0.05*tail*tail;
    vec2 g=vec2(1-uv.x,uv.y);
    // Masked rather than returned from early, as in radialMask.
    float inside=step(0.0,g.x)*step(g.x,1.0)*step(0.0,g.y)*step(g.y,1.0);
    g=clamp(g,vec2(0.0),vec2(1.0));
    float m=mask(_Ghost,g);
    vec2 headLocal=(uv-face)/vec2(0.17,0.32);
    float headRadius=length(headLocal);
    // The face is drawn straight onto the soul's own flame, with no pale disc behind it (Mortis, 2026-11-12: it
    // looks better without the circle). Only the source mask's skull sockets are filled, up to the mid tone of the
    // head around them, so the fill draws no outline of its own; the flame's bright crescent stays at the front of
    // the head. Supplied artist variants keep their own faces.
    float nativeFace=(1-step(0.5,uMaterial._AuthoredFace))*(1-bxSmoothstep(0.9,1.05,headRadius));
    // Sampled at fixed mip levels, so the fill can be skipped away from the head.
    if(nativeFace>0.0) m=max(m,min(socketFill(g),0.6)*nativeFace);
    // The face's dark sockets stay inside a solid head, read from a blurred mip.
    float head=bxSmoothstep(0.20,0.32,maskLod(_Ghost,g,2.5))*bxSmoothstep(0.55,0.75,uv.x);
    float body=bxSmoothstep(0.06,0.2,m);
    vec3 c=mix(Deep,Dark,band(m,0.22));
    c=mix(c,Mid,band(m,0.42));
    c=mix(c,Light,band(m,0.7));
    c*=mix(vec3(0.94,1.03,0.98),vec3(1.0,0.97,1.04),v3);
    float faceAA=max(length(fwidth(headLocal))*0.7,0.02);
    float features=min(soulEyes(headLocal,uMaterial._FaceVariant),soulMouth(headLocal,seed));
    c=mix(c,Socket,(1-bxSmoothstep(-faceAA,faceAA,features))*nativeFace);
    float alpha=max(head,body*mix(0.55,1.0,band(m,0.22)));
    // A soft, darker rim hugs the soul, as in Mortis's soul sketches, so it keeps its shape over pale ground.
    // It thins toward the tail, whose wisps would otherwise carry dark smudges.
    float rim=bxSmoothstep(0.10,0.34,maskLod(_Ghost,g,2.2))*(1-body)*(1-alpha)*0.6*mix(0.3,1.0,bxSmoothstep(0.2,0.6,uv.x));
    float glow=maskLod(_Ghost,g,3.5)*0.4*(1-alpha)*(1-rim);
    return vec4(c*alpha+Deep*rim+Mid*glow*0.6,alpha+rim+glow*0.6)*inside;
}

// The cast warmup, and the volley's last contraction (squeeze 0 to 1) just before the release: the reservoir stirring
// inside the caster. Mortis (2026-11-09): the souls are already stored in the body, so nothing flies in.
// A dark pool spreads underfoot and turns, its torn rim lit by the souls. Their light wheels round the caster in one
// vortex, strand by strand, faster and brighter as the reservoir fills, while a heart of light throbs in the body
// ever faster, each beat pushing a faint pressure ring over the ground. In the contraction the vortex and the pool's
// rim are drawn into the heart, which flares as the volley bursts out. All but the soul-light stays translucent and
// low in contrast. t is seconds since the warmup began and runs on through the contraction, so the two join without
// a jump; k is the warmup's progress.
vec4 charge(vec2 p, float t, float k, float squeeze)
{
    float seed=uMaterial._Seed;
    int seedKey=int(seed*100.0);
    float ignite=bxSmoothstep(0.0,0.2,k);
    float build=bxSmoothstep(0.0,1.0,k);
    float contract=bxSmoothstep(0.0,1.0,squeeze);
    float r=length(p);
    float theta=atan(p.y,p.x+0.00001);
    float aa=max(length(fwidth(p)),0.004);

    // The heartbeat quickens from about two beats a second to eight; its phase runs on through the contraction.
    float beatPhase=t*1.7+t*t*3.0;
    float beatAge=fract(beatPhase);
    float pulse=bxSmoothstep(0.0,0.06,beatAge)*exp(-beatAge*5.0);

    // The pool: a turning, torn shadow underfoot.
    float poolRadius=mix(0.52,1.2,build)*mix(1.0,0.55,contract);
    vec2 turned=rotation(t*1.4+r*1.8)*p;
    float coarse=bxValueNoiseQuintic(turned*2.4+vec2(seed*3.1,1.7));
    float fine=bxValueNoise(turned*6.2+vec2(1.3,seed*2.3));
    float edge=r+(coarse-0.5)*0.3+(fine-0.5)*0.08;
    float pool=(1-bxSmoothstep(poolRadius*0.7,poolRadius,edge))*ignite*(0.26+0.12*build+0.08*contract);
    vec4 col=vec4(mix(Ink,Deep,0.3*coarse)*pool,pool);
    // Its rim catches the souls' light in broken arcs, brighter on each beat.
    float rimLight=bell((edge-poolRadius*0.9)/(0.05+0.03*build))*bxSmoothstep(0.3,0.7,fine*0.5+coarse*0.5);
    rimLight*=ignite*(0.18+0.14*build+0.16*pulse*build+0.2*contract);
    col=over(col,vec4(mix(Dark,Mid,0.4*pulse+0.4*contract)*rimLight,rimLight));

    // A faint pressure ring runs out over the ground with every beat; the last ones are drawn back in.
    float ringRadius=0.32+1.05*beatAge;
    float ring=bell((r-ringRadius)/(0.03+0.035*beatAge+aa))*(1-beatAge)*(1-beatAge);
    ring*=build*(1-contract)*0.3;
    float implode=bell((r-mix(1.25,0.12,contract))/(0.05+aa))*contract*(1-contract)*4.0*0.42;
    col=over(col,vec4(mix(Dark,Mid,0.5)*(ring+implode),ring+implode));

    // The vortex: up to seven strands of soul-light chasing round the caster in one direction, each joining in turn,
    // at uneven spacing, radius and length; a fuller reservoir turns more of them (two for a single soul, seven for
    // twenty). They flow like the souls' own trails (the same wisp mask and colour bands), with a rounded head and a
    // tail that thins, ripples and fades along a gentle outward spiral.
    float strands=2.0+5.0*saturate((uMaterial._Souls-1.0)/19.0);
    float spin=t*3.6+t*t*3.4+contract*contract*3.2;
    for(int i=0;i<7;i++)
    {
        float vary=bxHash(ivec2(i,seedKey));
        float shape=bxHash(ivec2(i+17,seedKey));
        float spacing=bxHash(ivec2(i+29,seedKey));
        float join=float(i)/7.0;
        float appear=bxSmoothstep(0.04+0.4*join,0.22+0.4*join,k)*(1-bxSmoothstep(0.8,1.0,squeeze));
        appear*=saturate(strands-float(i));
        float head=TAU*(float(i)+0.55*spacing)/ceil(strands)+seed*2.0+spin*(1+0.2*vary);
        float sweep=mix(0.95,2.1,shape)*(1-0.3*contract);
        float behind=mod(head-theta+PI,TAU)-PI;
        float along=saturate(1-behind/sweep);
        float orbit=mix(0.52,1.02,fract(vary+0.37*float(i)))*mix(0.8,1.0,build)*mix(1.0,0.26,pow(contract,1.2));
        orbit+=0.05*sin(t*5.3+vary*TAU)*(1-contract);
        float centre=orbit*(1+0.22*(1-along))+0.035*sin(behind*5.0-t*9.0+vary*TAU)*(1-along)*(1-contract);
        float across=r-centre;
        float halfWidth=mix(0.085,0.13,shape)*mix(1.0,0.75,contract)*mix(0.3,1.0,pow(along,0.7));
        // Ahead of its head, the distance to the head's centre rounds it off.
        float ahead=max(-behind,0.0)*centre;
        float lateral=length(vec2(across,ahead))/max(halfWidth,aa);
        // The wisp mask at the souls' trail density: about a third of its length and one and a third of its width
        // per cell, so a narrow strand carries one or two broad currents rather than a brush of hairlines.
        float arc=max(behind,0.0)*centre;
        float flow=mask(_Wisp,vec2(arc*0.4-t*1.6+float(i)*0.37+seed,0.5+clamp(across,-0.15,0.15)*1.33));
        float s=1-along;
        float body=(1-bxSmoothstep(0.5,1.0,lateral))*step(behind,sweep)*bxSmoothstep(0.0,0.3,along);
        float alpha=body*(0.55+0.45*bxSmoothstep(0.15,0.5,flow))*mix(0.4,0.85,along)*appear;
        // The head glows: mid tone along most of the strand, pale only in its rounded front.
        float core=(1-bxSmoothstep(0.2,0.75,lateral))*bxSmoothstep(0.6,0.97,along);
        vec3 c=mix(Deep,Dark,band(flow,0.22)*(1-bxSmoothstep(0.75,1.0,lateral)));
        c=mix(c,Mid,saturate(band(flow,0.42)*(1-s*0.6)+core));
        c=mix(c,Light,saturate(band(flow,0.72)*(1-s)*(1-lateral)+band(core,0.72)+contract*0.4));
        col=over(col,vec4(c*alpha,alpha));
    }

    // Soul embers rise from the pool and are pulled in with it.
    for(int i=0;i<9;i++)
    {
        float v=bxHash(ivec2(i+40,seedKey));
        float life=fract(t*(0.8+0.6*v)+v*3.7);
        float angle=v*TAU+spin*0.35;
        vec2 at=(vec2(cos(angle),sin(angle))*mix(0.38,1.08,bxHash(ivec2(i+60,seedKey)))+vec2(0.0,life*0.5))*(1-0.7*contract);
        float d=length(p-at)/(0.04+aa);
        float ember=exp(-d*d)*bxSmoothstep(0.0,0.2,life)*(1-bxSmoothstep(0.55,1.0,life))*ignite*(0.35+0.4*build);
        col=over(col,vec4(mix(Mid,Light,0.6)*ember,ember));
    }

    // The heart of the reservoir, over the body: a soft throb that tightens and flares in the contraction.
    float heartRadius=mix(0.3,0.42,build)*mix(1.0,0.72,contract);
    float heart=exp(-r*r/(heartRadius*heartRadius))*ignite*(0.1+0.14*build+0.24*pulse*build+0.5*contract);
    col=over(col,vec4(mix(Mid,Light,0.25+0.45*pulse*build+0.3*contract)*heart,heart));
    return col;
}

vec4 burst(vec2 p)
{
    float t=uMaterial._Phase;
    float r=length(p);
    vec4 col=vec4(0);
    // The reservoir's last contraction carries straight on from the warmup.
    if(t<0) return charge(p,uMaterial._Gather+0.2333+t,1.0,saturate(1+t/0.2333));
    // Release: a faint spirit-smoke blast at the caster, a thin shock ring running ahead, rays and a flash, all
    // kept translucent so the souls carry the moment. Everything fades well inside the quad, so its square never
    // shows.
    float edgeMask=1-bxSmoothstep(4.4,5.7,r);
    float smokeRadius=0.5+2.3*(1-exp(-t*4.0));
    float smoke=radialMask(_Smoke,p,0.39,smokeRadius,t*0.6+uMaterial._Angle);
    float smokeFade=1-bxSmoothstep(0.3,1.0,t);
    float s=bxSmoothstep(0.24,0.62,smoke)*0.3*smokeFade*bxSmoothstep(0,0.04,t)*edgeMask;
    col=vec4(Ink*s,s);
    // Its rim catches the souls' light.
    float rim=band(smoke,0.7)*smokeFade*0.28*edgeMask;
    col=over(col,vec4(Deep*rim,rim));
    float ringRadius=0.5+4.5*(1-exp(-t*7.5));
    float ring=radialMask(_Ring,p,0.85,ringRadius,uMaterial._Angle);
    float ringFade=1-bxSmoothstep(0.12,0.42,t);
    float wash=bxSmoothstep(0.3,0.62,ring)*0.08*ringFade*edgeMask;
    col=over(col,vec4(Dark*wash,wash));
    // Feathered rather than cut out, so the ring reads as light passing over the ground, not as a flat shape.
    float rr=bxSmoothstep(0.5,0.92,ring)*ringFade*edgeMask*0.42;
    col=over(col,vec4(mix(Dark,Mid,bxSmoothstep(0.6,0.95,ring))*rr,rr));
    float rays=radialMask(_Rays,p,0.53,0.9+2.6*(1-exp(-t*9.0)),uMaterial._Angle*1.7);
    float ry=band(rays,0.3)*exp(-t*7.5)*0.35;
    col=over(col,vec4(mix(Dark,Mid,band(rays,0.45))*ry,ry));
    float flash=exp(-r*r/(0.12+t*2.2))*exp(-t*11.0)*0.65;
    col=over(col,vec4(mix(Mid,Light,flash)*flash,flash));
    return col;
}

// A segment of a soul's trail. U0/U1 place its head and tail ends along the whole trail (0 at the soul).
vec4 trailSegment(vec2 uv)
{
    float t=uMaterial._Phase;
    float s=mix(uMaterial._U1,uMaterial._U0,uv.x);
    float headWidth=pow(saturate(1-uMaterial._U0),0.6);
    float width=pow(saturate(1-s),0.6)/max(headWidth,0.001);
    float across=abs(uv.y-0.5)*2;
    float edge=1-bxSmoothstep(width*0.55,width,across);
    float flow=mask(_Wisp,vec2(s*uMaterial._Length*0.32-t*1.7+uMaterial._Seed,0.5+(uv.y-0.5)*0.8));
    float fade=pow(saturate(1-s),1.1);
    float a=edge*fade*(0.5+0.5*bxSmoothstep(0.15,0.5,flow));
    vec3 c=mix(Deep,Dark,band(flow,0.22));
    c=mix(c,Mid,band(flow,0.5)*(1-s*0.7));
    c=mix(c,Light,band(flow,0.78)*(1-s));
    return vec4(c*a,a)*0.85;
}

// A soul bursting on its target: a small ring, rays and a flash.
vec4 impact(vec2 p)
{
    float t=uMaterial._Phase;
    float r=length(p);
    float ring=radialMask(_Ring,p,0.85,0.18+0.75*(1-exp(-t*10.0)),uMaterial._Angle);
    float a=bxSmoothstep(0.3,0.8,ring)*(1-bxSmoothstep(0.12,0.35,t))*0.5;
    vec4 col=vec4(mix(Dark,Mid,bxSmoothstep(0.55,0.9,ring))*a,a);
    float rays=radialMask(_Rays,p,0.53,0.3+0.6*(1-exp(-t*12.0)),uMaterial._Angle);
    float ry=band(rays,0.3)*exp(-t*10.0)*0.5;
    col=over(col,vec4(Dark*ry,ry));
    float flash=exp(-r*r/(0.03+t*0.5))*exp(-t*14.0)*0.6;
    return over(col,vec4(mix(Mid,Light,flash)*flash,flash));
}

// The cast warmup (charge above).
vec4 gather(vec2 p) { return charge(p,uMaterial._Phase,saturate(uMaterial._Progress),0.0); }

void main()
{
    vec4 col;
    float mode=uMaterial._Mode;
    vec2 p=(vUv-0.5)*uMaterial._Size;
    if(mode<0.5) col=burst(p);
    else if(mode<1.5) col=ghost(vUv,uMaterial._Phase,uMaterial._Seed);
    else if(mode<2.5) col=trailSegment(vUv);
    else if(mode<3.5) col=impact(p);
    else col=gather(p);
    outColor=col*uMaterial._Opacity;
}
