#version 450
// Vanquish, in the souls' flat cyan bands like the Life Vanquisher itself (a sword forged from souls). The thrust
// itself leaves no light; only its contact does, kept translucent (Mortis, 2026-11-09).
// Mode 1: the impact. 2: a kill's soul taken in by the wielder.
layout(location=0) in vec2 vUv;
layout(location=0) out vec4 outColor;
#include "BX_ShaderCommon.glsl"
layout(set=2,binding=0) uniform MaterialUniforms
{
    float _Phase; // @default = 0.0
    float _Mode; // @default = 1.0
    float _Opacity; // @default = 1.0
    float _Kill; // @default = 0.0
    float _Seed; // @default = 0.0
    float _Size; // @default = 2.6
} uMaterial;
// Piloto Studio masks in alpha (Common/Textures/Effects/Piloto/SOURCES.json).
layout(set=2,binding=1) uniform sampler2D _ShardTex;
layout(set=2,binding=2) uniform sampler2D _RingTex;
layout(set=2,binding=3) uniform sampler2D _RaysTex;

const vec3 Light=vec3(0.678,0.984,1.0);
const vec3 Mid=vec3(0.467,0.773,0.788);
const vec3 Dark=vec3(0.267,0.698,0.722);
const vec3 Deep=vec3(0.05,0.24,0.27);

vec4 over(vec4 back, vec4 front) { return front+back*(1-front.a); }
float band(float x, float edge) { return bxSmoothstep(edge-0.03,edge+0.03,x); }
mat2 rotation(float a) { float c=cos(a), s=sin(a); return mat2(c,s,-s,c); }
float maskOf(sampler2D tex, vec2 uv)
{
    float inside=step(0.0,uv.x)*step(uv.x,1.0)*step(0.0,uv.y)*step(uv.y,1.0);
    // Sample before masking so neighbouring pixels agree on the mip level at the effect's edge.
    return tex2D(tex,clamp(uv,vec2(0.0),vec2(1.0))).a*inside;
}
float radialMask(sampler2D tex, vec2 p, float peak, float radius, float spin)
{
    return maskOf(tex,rotation(spin)*p/(2*radius/peak)+0.5);
}

// Positive x follows the blade. The light spills down the line of the thrust and breaks into seeded fragments.
vec4 impact(vec2 p)
{
    float t=uMaterial._Phase, kill=uMaterial._Kill;
    float seed=uMaterial._Seed;
    float r=length(p);
    vec4 col=vec4(0);
    float ringR=0.15+(0.75+0.55*kill)*(1-exp(-t*11.0));
    float ring=radialMask(_RingTex,p,0.85,ringR,seed);
    float rr=bxSmoothstep(0.37,0.76,ring)*(1-bxSmoothstep(0.11,0.34,t))*0.36;
    col=over(col,vec4(mix(Dark,Mid,band(ring,0.75))*rr,rr));
    float rays=radialMask(_RaysTex,p,0.53,0.35+(0.6+0.4*kill)*(1-exp(-t*12.0)),seed*1.7);
    float ry=bxSmoothstep(0.23,0.57,rays)*exp(-t*9.0)*0.36;
    col=over(col,vec4(Dark*ry,ry));
    // Two shards from the 2x2 atlas, thrown out and shrinking away.
    for(int i=0;i<2;i++)
    {
        float k=float(i);
        float grow=0.45+0.75*(1-exp(-t*14.0));
        vec2 drift=vec2(0.46,(k-0.5)*0.35)*(1-exp(-t*7.0));
        vec2 q=rotation(seed*3.1+k*2.6)*(p-drift)/((0.95+0.45*kill)*grow)+0.5;
        float tile=mod(floor(seed*7.0)+k,4.0);
        vec2 at=(clamp(q,vec2(0.002),vec2(0.998))+vec2(mod(tile,2.0),floor(tile/2.0)))*0.5;
        float inside=all(greaterThan(q,vec2(0)))&&all(lessThan(q,vec2(1)))?1:0;
        float s=tex2D(_ShardTex,at).a*inside;
        float a=bxSmoothstep(0.37,0.65,s)*(1-bxSmoothstep(0.14,0.33,t))*0.43;
        col=over(col,vec4(mix(Dark,Mid,band(s,0.85))*a,a));
    }

    float reach=0.36+(0.91+0.27*kill)*(1-exp(-t*13.0));
    float x=(p.x+0.27)/reach;
    float along=saturate(x);
    float scroll=p.x-t*3.3;
    float surge=bxValueNoiseQuintic(vec2(scroll*4.2+seed,p.y*8.1+seed*0.7));
    float bend=sin(along*8.4-t*17.0+seed)*along*0.14;
    float width=mix(0.15,0.044,along);
    float ribbon=1-bxSmoothstep(width*0.30,width,abs(p.y-bend));
    float side=1-bxSmoothstep(0.025,width*0.71+0.018,abs(p.y+bend+(0.08+0.19*along)*sin(seed+1.3)));
    float span=bxSmoothstep(-0.08,0.18,x)*(1-bxSmoothstep(0.71,1.04,x));
    float energy=max(ribbon,side*0.62)*span*(0.44+0.56*surge)*(1-bxSmoothstep(0.12,0.41,t));
    vec3 current=mix(Dark,Light,bxSmoothstep(0.21,0.84,surge));
    col=over(col,vec4(current*energy,energy));

    // Each contact keeps its own pattern. Pockets erode as the energy travels instead of flickering at random.
    vec2 moving=p-vec2(t*1.7,0);
    float coarse=bxValueNoiseQuintic(moving*vec2(7.0,9.0)+vec2(seed,seed*0.71));
    float detail=bxValueNoise(moving*vec2(17.0,19.0)+vec2(seed*0.3,19.7));
    float threshold=mix(-0.10,1.07,bxSmoothstep(0.055,0.38+kill*0.044,t));
    float field=mix(coarse,detail,0.34)-threshold;
    float remaining=bxSmoothstep(-0.045,0.063,field);
    float burning=(1-bxSmoothstep(0.018,0.16,abs(field)))*remaining;
    col.rgb=col.rgb*remaining+Mid*col.a*burning*0.32;
    col.a*=remaining;
    float flash=exp(-r*r/(0.025+t*0.35))*exp(-t*17.0)*0.64;
    return over(col,vec4(mix(Mid,Light,flash)*flash,flash));
}

// A kill's soul taken in by the wielder (Mortis, 2026-11-09: shown on the wielder rather than as a ghost flying across).
// Soul-light swirls in close around them with a few motes riding it, sinks into the body and glows there a moment.
// p is in cells from the wielder; _Phase counts seconds since the absorption began.
vec4 absorb(vec2 p)
{
    float t=uMaterial._Phase;
    float r=length(p);
    float angle=atan(p.y,p.x);
    float k=bxSmoothstep(0.0,0.55,t);
    float radius=mix(0.95,0.12,k);
    float arms=0.5+0.5*sin(angle*3.0-t*12.0+r*7.0+uMaterial._Seed);
    float ring=exp(-pow((r-radius)/(0.06+0.08*(1-k)),2.0))*(0.45+0.55*arms);
    ring*=bxSmoothstep(0.0,0.08,t)*(1-bxSmoothstep(0.45,0.62,t))*0.5;
    vec4 col=vec4(mix(Dark,Mid,arms)*ring,ring);
    for(int i=0;i<5;i++)
    {
        float n=float(i);
        float th=n*1.2566+uMaterial._Seed+t*5.5+(1-k)*1.6;
        float mr=mix(0.9+0.1*sin(n*2.3),0.08,bxSmoothstep(0.0,0.6,t-n*0.025));
        vec2 at=vec2(cos(th),sin(th))*mr;
        vec2 d=p-at;
        float mote=exp(-dot(d,d)/0.004)*(1-bxSmoothstep(0.5,0.66,t))*bxSmoothstep(0.0,0.05,t)*0.75;
        col=over(col,vec4(mix(Mid,Light,0.5)*mote,mote));
    }
    float glow=exp(-r*r/0.16)*bxSmoothstep(0.2,0.5,t)*(1-bxSmoothstep(0.5,0.9,t))*0.4;
    return over(col,vec4(Mid*glow,glow));
}

void main()
{
    vec4 col;
    if(uMaterial._Mode<1.5) col=impact((vUv-0.5)*uMaterial._Size);
    else col=absorb((vUv-0.5)*uMaterial._Size);
    outColor=col*uMaterial._Opacity;
}
