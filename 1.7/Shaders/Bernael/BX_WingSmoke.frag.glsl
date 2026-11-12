#version 450
// Wing Burst's smoke: the powdery residue its wings scatter, as soft, translucent dark smoke close to the game's own.
// Mortis (2026-11-09): the earlier flat toon smoke with crimson contours was too dense and looked out of place, and
// the caster must not vanish into it. The caster now stays in view and only trails a ghostly streamer of this smoke.
// Mode 1: one puff of the warmup, take-off, flight wake or landing cloud. 2: the lingering haze over one smoke cell.
// 3: the continuous streamer trailing from the flying caster.
layout(location = 0) in vec2 vUv;
layout(location = 0) out vec4 outColor;
#include "BX_ShaderCommon.glsl"
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    float _Phase; // @default = 0.0
    float _Mode; // @default = 1.0
    float _Seed; // @default = 0.0
    float _Opacity; // @default = 1.0
    float _Age; // @default = 0.0
    float _Length; // @default = 2.0
} uMaterial;
// Piloto Studio masks in alpha (Common/Textures/Effects/Piloto/SOURCES.json): four soft smoke puffs in a 2x2 atlas.
layout(set = 2, binding = 1) uniform sampler2D _Puffs;

// A warm, near-black soot; denser parts catch a little more light, as the game's smoke does.
const vec3 Soot = vec3(0.040,0.032,0.036);
const vec3 Ash = vec3(0.155,0.135,0.145);

mat2 rotation(float a) { float c=cos(a), s=sin(a); return mat2(c,s,-s,c); }

// The density of one atlas puff, chosen and turned by seed, at uv in the quad's 0-1 square. The puffs still hold
// some density at their tile edges, so each fades out on a circle well inside its square. Sampled at every pixel.
float puffDensity(vec2 uv, float seed, float turn)
{
    vec2 q=rotation(seed*2.4+turn)*(uv-0.5)+0.5;
    float circle=1-bxSmoothstep(0.30,0.47,length(q-0.5));
    float tile=mod(floor(seed*3.7),4.0);
    vec2 at=(clamp(q,vec2(0.004),vec2(0.996))+vec2(mod(tile,2.0),floor(tile/2.0)))*0.5;
    return texture(_Puffs,vec2(at.x,1-at.y)).a*circle;
}

// One puff swelling out and thinning away over its life (_Age 0 to 1): it thins from its edges inward, as smoke
// disperses, rather than fading as one flat sheet.
vec4 puff(vec2 uv)
{
    float age=saturate(uMaterial._Age);
    float d=puffDensity(uv,uMaterial._Seed,age*0.5*(fract(uMaterial._Seed*7.1)-0.5));
    float thin=age*age*0.45;
    float density=saturate((d-thin)/(1-thin));
    density=pow(density,0.9)*bxSmoothstep(0.0,0.1,age)*(1-bxSmoothstep(0.55,1.0,age));
    vec3 c=mix(Soot,Ash,bxSmoothstep(0.35,0.95,d)*0.65);
    float a=density*0.62;
    return vec4(c*a,a);
}

// The haze over one smoke cell: a large, slowly churning puff whose strength comes in _Opacity.
vec4 haze(vec2 uv)
{
    float t=uMaterial._Phase;
    float d=puffDensity(uv,uMaterial._Seed,t*0.05);
    d=max(d,puffDensity(uv,uMaterial._Seed+1.3,-t*0.04)*0.8);
    vec3 c=mix(Soot,Ash,bxSmoothstep(0.35,0.95,d)*0.5);
    float a=pow(d,0.85);
    return vec4(c*a,a);
}

// The streamer's UV runs from the caster to its tail. _Length gives that coordinate a scale in cells.
vec4 trail(vec2 uv)
{
    float t=uMaterial._Phase;
    float seed=uMaterial._Seed;
    float s=saturate(uv.x);
    float d=s*uMaterial._Length;
    // The same world-space flow crosses every joint, with a broader billow close to the wings.
    float flow=d-t*5.4;
    float roll=bxValueNoiseQuintic(vec2(flow*1.25,seed));
    float sway=(roll-0.5)*(0.08+0.39*s);
    float width=mix(0.73,0.25,pow(s,1.35))*(0.84+0.30*roll);
    float y=((uv.y-0.5)*2-sway)/width;
    float billows=bxValueNoiseQuintic(vec2(flow*2.2+seed,y*1.6+seed*3.7));
    float detail=bxValueNoise(vec2(flow*4.4-y*0.9,y*3.1+seed));
    float edge=1-bxSmoothstep(0.21,0.99,abs(y)+(billows-0.5)*0.36);
    float smoke=edge*(0.62+0.38*billows);

    // The two sides of the cloak peel into wisps as the middle wears away.
    float curl=sin(flow*2.8+seed)*0.18;
    float upper=1-bxSmoothstep(0.05,0.37,abs(y-0.35-curl));
    float lower=1-bxSmoothstep(0.07,0.33,abs(y+0.32-curl));
    float threads=max(upper*bxSmoothstep(0.22,0.70,billows),lower*bxSmoothstep(0.28,0.66,detail));
    float fray=bxSmoothstep(0.24,0.83,s);
    float fill=mix(smoke,max(threads,smoke*0.38),fray);
    float root=bxSmoothstep(0.16,0.54,d);
    float tail=1-bxSmoothstep(0.48,1.0,s);
    float a=fill*root*tail*0.91;
    vec3 c=mix(Soot,Ash,bxSmoothstep(0.30,0.85,billows*0.68+detail*0.32)*(0.60+0.26*fray));
    return vec4(c*a,a);
}

void main()
{
    float mode=uMaterial._Mode;
    vec4 col;
    if(mode<1.5) col=puff(vUv);
    else if(mode<2.5) col=haze(vUv);
    else col=trail(vUv);
    outColor=col*uMaterial._Opacity;
}
