#version 450
// Cursed fire in Mortis's own flame style, as drawn on the lasting fire: navy cores inside pale blue flames, a white
// contour and a soft white glow. Mode 0 is the orb in flight, mode 2 the burst, mode 3 one lasting cursed fire.
layout(location = 0) in vec2 vUv;
layout(location = 0) out vec4 outColor;
#include "BX_ShaderCommon.glsl"
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    float _Phase; // @default = 0.0
    float _Opacity; // @default = 1.0
    float _Seed; // @default = 0.0
    float _Mode; // @default = 0.0
    vec4 _Direction; // @default = vec4(1,0,0,0)
    float _Charge; // @default = 1.0
    float _Radius; // @default = 2.6
    float _Size; // @default = 4.4
    float _Grow; // @default = 1.0
    float _Flip; // @default = 0.0
} uMaterial;
layout(set = 2, binding = 1) uniform sampler2D _FlameA;
layout(set = 2, binding = 2) uniform sampler2D _FlameB;
layout(set = 2, binding = 3) uniform sampler2D _FlameC;

// Sampled from the lasting fire's sprites (Things/CursedFlame): navy core, deep and pale blue fill, white contour.
const vec3 Navy = vec3(0.086,0.133,0.169);
const vec3 Deep = vec3(0.51,0.71,0.859);
const vec3 Pale = vec3(0.725,0.851,0.929);
const vec3 Halo = vec3(1.0);
// The nucleus's inner swirl and the burst's smoke.
const vec3 Cobalt = vec3(0.16,0.31,0.5);
const vec3 Smoke = vec3(0.03,0.055,0.09);
const float PI = 3.14159265;
const float TAU = 6.2831853;

vec4 over(vec4 back, vec4 front) { return front+back*(1-front.a); }
float variation(int n) { return bxHash(ivec2(n,int(uMaterial._Seed*277.0))); }
// A soft profile; squared directly, since pow() of a negative base is undefined.
float bell(float x) { return exp(-x*x); }
float smin(float a, float b, float k) { float h=saturate(0.5+0.5*(b-a)/k); return mix(b,a,h)-k*h*(1.0-h); }

// The sprites' flame: a white contour line outside the shape, a soft white glow beyond it, a blue fill that runs from
// deep at the flame's root (tone 0) to pale at its tips (tone 1), and a navy inner flame wherever core is negative.
// field and core are signed distances in cells, negative inside; px is one pixel in cells. darken (0 to 1) ramps the
// inner flame from the fill through deep blue to navy: a straight blend from a pale fill to navy passes through grey.
vec4 cursedFlame(float field, float core, float px, float tone, float glowAlpha, float darken)
{
    float aa=max(px*0.75,0.0015);
    float line=0.032+px*0.6;
    float outline=1-bxSmoothstep(-aa,aa,field-line);
    float fill=1-bxSmoothstep(-aa,aa,field);
    float navy=(1-bxSmoothstep(-aa,aa,core))*fill;
    vec3 body=mix(Deep,Pale,saturate(tone));
    vec3 c=mix(Halo,body,fill);
    c=mix(c,mix(mix(body,Deep,saturate(darken*2.0)),Navy,saturate(darken*2.0-1.0)),navy);
    float glow=bell(max(field-line,0.0)/(0.075+2.0*px))*(1-outline)*glowAlpha;
    return vec4(c*outline+Halo*glow,outline+glow);
}

// One flame tongue streaming back along -x from its root at (x0, y0): it tapers to a point, curls sideways by curl at
// its tip and ripples by wave. A signed distance in cells, closed off past both ends so its point draws no hairline.
float tongue(vec2 q, float x0, float y0, float len, float width, float curl, float wave, float phase)
{
    float u=(x0-q.x)/len;
    float k=saturate(u);
    float ripple=sin(k*5.5-phase);
    float centre=y0+curl*k*k+wave*ripple*k;
    float slope=(2.0*curl*k+wave*(ripple+5.5*k*cos(k*5.5-phase)))/len;
    float d=(abs(q.y-centre)-width*pow(1.0-k,0.85))/sqrt(1.0+slope*slope);
    return max(d,max(-u*len*0.6,(u-1.0)*len));
}

float orbWake(float distanceBehind, float time, float seed)
{
    // The bend travels from the shoulder down the wake and grows as it leaves the head.
    float phase=distanceBehind*4.4-time*7.2+seed*2.6;
    return bxSmoothstep(0.13,1.5,distanceBehind)*(0.20*sin(phase)+0.055*sin(1.6*phase+1.3));
}

// The dense navy head follows the projectile. Its curved wake narrows behind it, with two unequal tongues
// wrapped around the shoulder and a few bits of flame and embers peeling away.
vec4 orb(vec2 p, float px)
{
    float t=uMaterial._Phase, seed=uMaterial._Seed;
    const float OrbScale=0.5;
    float scale=OrbScale*1.15*mix(0.18,1.0,bxSmoothstep(0.0,1.0,uMaterial._Charge));
    p/=scale;
    px/=scale;
    vec2 heading=normalize(uMaterial._Direction.xy+vec2(0.0001,0));
    vec2 local=vec2(dot(p,heading),dot(p,vec2(-heading.y,heading.x)));
    vec2 q=local;
    q.y-=orbWake(-q.x,t,seed);
    // A slight stretch along the flight direction and a gentle roll keep the silhouette alive.
    float roll=0.11*sin(t*4.2+seed);
    vec2 head=vec2(q.x*0.94,q.y-q.x*roll);
    float r=length(head);
    float theta=atan(head.y,head.x+0.00001);
    float aa=max(px*0.75,0.0015);

    float phase=t*7.2+seed*2.6;
    float body=r-0.42-0.013*sin(4.0*theta-phase*0.6);
    body=smin(body,tongue(q,0.15,0.015,1.53+0.11*sin(phase),0.25,-0.04,0.035,phase),0.14);
    body=smin(body,tongue(q,0.06,0.18,1.04+0.10*sin(phase+1.9),0.15,0.16,0.04,phase+1.9),0.13);
    body=smin(body,tongue(q,-0.04,-0.17,0.83+0.09*sin(phase+3.5),0.14,-0.13,0.035,phase+3.5),0.11);

    // A shed flame starts on the wake as it was at release, then drifts and thins independently.
    for(int i=0;i<3;i++)
    {
        float duration=mix(0.40,0.57,variation(i+3));
        float life=fract(t/duration+variation(i+9));
        float behind=mix(0.95,1.16,variation(i+15));
        float side=variation(i+21)-0.5;
        float release=orbWake(behind,t-life*duration,seed);
        vec2 at=vec2(-behind-life*0.85,release+side*(0.35+life*0.7));
        float size=0.115*bxSmoothstep(0.0,0.19,life)*(1-life)+0.0001;
        vec2 drop=(local-at)/size;
        float flame=smin(length(drop)-1.0,tongue(drop,0.3,0.0,2.2,0.8,side*0.9,0.0,0.0),0.35)*size;
        body=min(body,flame+(1-bxSmoothstep(0.0,0.03,size))*0.5);
    }
    vec4 col=cursedFlame(body,body+0.1,px,bxSmoothstep(0.1,1.4,-q.x),0.3,1.0);

    // The nucleus: navy, a slow cobalt swirl inside, a thin crescent of blue light on its leading side.
    float nucleus=r-0.3;
    float inside=1-bxSmoothstep(-aa,aa,nucleus);
    float swirl=0.5+0.5*sin(2.0*theta-r*15.0+t*7.0+seed);
    vec3 heart=mix(Navy,Cobalt,bxSmoothstep(0.6,0.95,swirl)*bxSmoothstep(0.04,0.2,r)*0.8);
    float lead=saturate(head.x/max(r,0.001)*0.6+0.4);
    heart=mix(heart,Deep,bell((nucleus+0.032)/(0.02+px))*lead*0.9);
    col=over(col,vec4(heart*inside,inside));

    float embers=0.0;
    for(int i=0;i<6;i++)
    {
        float life=fract(t*(0.97+0.51*variation(i+7))+float(i)*0.153+variation(i+24));
        vec2 at=vec2(-0.5-life*1.7,(variation(i+32)-0.5)*(0.7+life*0.7));
        at.y+=orbWake(0.8,t-life,seed);
        vec2 delta=(local-at)/vec2(0.048+px,0.023+px);
        embers=max(embers,exp(-dot(delta,delta))*bxSmoothstep(0.0,0.16,life)*(1-bxSmoothstep(0.4,1.0,life)));
    }
    return over(col,vec4(Halo*embers*0.9,embers*0.9));
}

const int BlastBillowCount = 13;

// A roll of hot gas is carried by the expanding cloud. It widens as it travels,
// folds sideways, and cools at its own rate. Distances are in cells.
vec4 blastBillow(vec2 p, float age, float radius, int index)
{
    float v=variation(index+300), w=variation(index+320), push=variation(index+360);
    float turn=variation(index+340)*2.0-1.0;
    float outer=1.0-step(9.0,float(index));
    float time=max(age-0.006-0.047*v,0.0);
    float advance=1.0-exp(-time*mix(7.6,11.2,w));
    float cooling=bxSmoothstep(0.16+0.13*v,0.35+0.17*w,time);

    // Stagger rolls around the cloud, with slower ones still burning inside the front.
    float angle=variation(280)*TAU+float(index)*2.39996+(v-0.5)*0.78;
    angle+=turn*0.34*advance*advance;
    vec2 way=vec2(cos(angle),sin(angle));
    vec2 tangent=vec2(-way.y,way.x);
    float reach=mix(mix(0.18,0.58,push),mix(0.57,0.94,push),outer);
    float travel=radius*(0.045+reach*advance);
    vec2 offset=p-way*travel;
    vec2 local=vec2(dot(offset,way),dot(offset,tangent));

    float size=radius*mix(0.12,0.28,v)*mix(0.86,1.0,outer)*(0.16+0.84*advance)*(1.0-0.66*cooling);
    float curl=bxSmoothstep(-0.55,0.82,local.x/size)*turn;
    local.y-=size*0.54*curl;
    vec2 shape=local/size;
    float phase=w*TAU-time*14.0;
    float crown=length(local*vec2(1.19,0.89))-size*(1.0+0.10*sin(shape.y*5.2+phase));
    float side=turn<0.0 ? -1.0 : 1.0;
    float rolled=tongue(vec2(-local.y*side,local.x),-size*0.16,size*0.21,
        size*(0.92+0.53*w),size*0.40,-size*(0.44+0.22*v),size*0.13,phase);
    float field=smin(crown,rolled,size*0.30);
    // The shoulder rolls over and opens a notch as it peels away.
    float scoop=length((local-size*vec2(-0.29,side*0.88))*vec2(1.05,1.18))
        -size*(0.43+0.19*w)*bxSmoothstep(0.068,0.24,time);
    field=-smin(-field,scoop,size*0.18);

    float shadow=length((local+size*vec2(0.22,0.15*turn))*vec2(1.36,0.94))-size*(0.62-0.18*cooling);
    float shadowEdge=local.x+size*(0.05+0.19*sin(shape.y*2.8+phase));
    float core=-smin(-shadow,-shadowEdge,size*0.23);
    // Light catches the curved shoulder and the smaller fold tucked behind it.
    float shoulder=shape.y+0.16*turn;
    float crest=shape.x-0.25-0.24*shoulder*shoulder+0.09*sin(shoulder*3.6+phase);
    float leading=bell(crest/0.14);
    float inner=bell((crest+0.41+0.11*shoulder*turn)/0.18);
    vec2 domeShape=shape*vec2(1.19,0.89);
    float dome=bxSmoothstep(0.0,0.80,1.0-dot(domeShape,domeShape))
        *bxSmoothstep(-0.53,0.43,shape.x-0.21*shoulder*turn);
    float fold=saturate(0.68*leading+0.29*inner+0.39*dome)
        *(1.0-bxSmoothstep(0.62,1.08,abs(shoulder)))*(1.0-0.42*cooling);
    float opacity=bxSmoothstep(0.007,0.053,time)*(1.0-cooling);
    field+=radius*0.62*(1.0-bxSmoothstep(0.0,0.12,opacity));
    return vec4(field,max(core,field+radius*0.022),opacity,fold);
}

// Scale the contour by the change in the distance field across a pixel and let
// the white-hot edge fade to pale blue as the gas cools.
vec4 paintGroundFlame(float field, float core, float px, float tone, float fold, float darken, float rimHeat)
{
    float fieldPx=clamp(length(vec2(dFdx(field),dFdy(field))),px*0.40,px*1.60);
    float corePx=clamp(length(vec2(dFdx(core),dFdy(core))),px*0.40,px*1.60);
    float edgeAA=max(fieldPx*0.72,0.0015);
    float coreAA=max(corePx*0.76,0.0015);
    float line=(mix(0.011,0.023,rimHeat)/max(px,0.001)+mix(0.22,0.52,rimHeat))*fieldPx;
    float outline=1-bxSmoothstep(-edgeAA,edgeAA,field-line);
    float fill=1-bxSmoothstep(-edgeAA,edgeAA,field);
    float heartFill=1-bxSmoothstep(-coreAA,coreAA,core);
    float lit=bxSmoothstep(0.06,0.91,fold);
    vec3 body=mix(mix(Cobalt,Deep,0.65+0.25*tone),Pale,0.24+0.48*tone);
    body=mix(body,mix(Pale,Halo,0.23+0.15*rimHeat),0.82*lit);
    float heartRim=1-bxSmoothstep(0.006,0.073,-core);
    vec3 heart=mix(Navy,Cobalt,saturate(0.72*heartRim+0.36*lit));
    heart=mix(mix(body,Deep,saturate(darken*2.0)),heart,saturate(darken*2.0-1.0));
    vec3 rim=mix(mix(Deep,Pale,0.53),Halo,rimHeat);
    vec3 color=mix(rim,mix(body,heart,heartFill),fill);
    float glow=bell(max(field-line,0.0)/(0.063+2.0*fieldPx))*(1-outline)*mix(0.10,0.27,rimHeat);
    return vec4(color*outline+rim*glow,outline+glow);
}

// The whole cloud expands out of the impact first. Its interior cools and
// thins as the rolls at the edge keep spreading and begin to separate.
vec4 groundFlames(vec2 p, float px, float age, float radius, vec2 heading)
{
    float advance=1.0-exp(-age*8.1);
    vec2 local=p-heading*(radius*0.095*advance*advance);
    float r=length(local);
    vec2 outward=local/max(r,0.001);
    float seed=uMaterial._Seed;
    float reach=radius*(0.046+0.79*advance);
    float bulge=bxValueNoiseQuintic(outward*2.55+vec2(seed*1.8,seed*2.1));
    float edge=reach*mix(0.75,1.14,bulge);
    vec2 flow=local/max(reach,0.001);
    vec2 warp=0.22*sin(flow.yx*4.5+vec2(seed,seed*1.32)-age*7.0);
    vec2 rolledFlow=flow+warp;
    float billow=bxValueNoiseQuintic(rolledFlow*3.45+vec2(seed,age*1.8));
    // The finer rolls stretch with the cloud and shade its interior.
    float eddies=bxValueNoiseQuintic((rolledFlow+0.40*warp.yx)*7.7+vec2(seed*1.42,-age*2.4));
    float density=mix(billow,eddies,0.28);
    float centerLive=bxSmoothstep(0.006,0.043,age)
        *(1.0-bxSmoothstep(0.092+0.043*billow,0.213+0.093*billow,age));
    float field=r-edge+radius*0.89*(1.0-bxSmoothstep(0.01,0.33,centerLive));
    float core=max(field+radius*0.085,(0.50-density)*radius*0.31);
    float weight=exp(-clamp(field,-0.19,3.0)*11.0);
    float opacity=weight*centerLive;
    float folds=opacity*0.74*bxSmoothstep(0.23,0.65,density)*(1.0-0.62*bxSmoothstep(0.65,0.94,density));
    float join=radius*mix(0.11,0.032,bxSmoothstep(0.095,0.31,age));
    for(int i=0;i<BlastBillowCount;i++)
    {
        vec4 puff=blastBillow(local,age,radius,i);
        field=smin(field,puff.x,join);
        core=smin(core,puff.y,join*0.42);
        float near=exp(-clamp(puff.x,-0.19,3.0)*11.0);
        weight+=near;
        opacity+=near*puff.z;
        folds+=near*puff.z*puff.w;
    }

    core=max(core,field+radius*0.022);
    float tone=mix(0.25,0.78,bxSmoothstep(radius*0.12,radius*1.12,r));
    float darken=bxSmoothstep(0.043,0.211,age)*0.87;
    float rimHeat=mix(0.38,1.0,1.0-bxSmoothstep(0.095,0.33,age));
    float fold=saturate(folds/(opacity+1e-6)*mix(0.78,1.22,eddies));
    vec4 flame=paintGroundFlame(field,core,px,tone,fold,darken,rimHeat);
    float heat=bell(r/(radius*0.26+reach*0.33))*(1.0-bxSmoothstep(0.025,0.125,age));
    flame.rgb=mix(flame.rgb,Halo*flame.a,heat*0.84);
    return flame*(opacity/(weight+1e-6));
}

// A brief pale front skims the ground ahead of the luminous cloud.
vec4 blastPressure(float r, vec2 outward, float px, float age, float radius, float seed)
{
    float reach=radius*1.15*(1.0-exp(-age*18.0));
    float width=radius*(0.025+0.18*age)+px;
    float streak=bell((r-reach)/width);
    float dust=bxValueNoiseQuintic(outward*5.3+vec2(seed*2.8,-seed));
    float live=bxSmoothstep(0.005,0.028,age)*(1.0-bxSmoothstep(0.049,0.176,age));
    float opacity=streak*mix(0.13,1.0,bxSmoothstep(0.22,0.79,dust))*live*0.21;
    vec3 tint=mix(Deep,Pale,0.48);
    return vec4(tint*opacity,opacity);
}

// A compact flash at contact. The flame, embers and low smoke run outward along the ground, leaving cursed fires.
vec4 burst(vec2 p, float px)
{
    float age=uMaterial._Phase, radius=uMaterial._Radius, seed=uMaterial._Seed;
    // Give the flame time to roll after the initial flash.
    float flameAge=age-0.26*max(age-0.022,0.0);
    vec2 heading=normalize(uMaterial._Direction.xy+vec2(0.0001,0));
    float life=saturate(age/0.95);
    float r=length(p);
    float window=1-bxSmoothstep(0.62,0.9,r*2.0/uMaterial._Size);

    float smoke=0.0;
    float drift=1-exp(-flameAge*5.6);
    for(int i=0;i<7;i++)
    {
        float v=variation(i+460), w=variation(i+480);
        float angle=(float(i)+0.77*v)*TAU/7.0+seed;
        vec2 way=vec2(cos(angle),sin(angle));
        vec2 at=radius*(way*(0.06+(0.45+0.32*w)*drift)+heading*0.07*drift);
        float size=radius*(0.20+0.16*w)*(0.61+0.71*drift);
        smoke=max(smoke,bell(length(p-at)/size));
    }
    float billow=bxValueNoiseQuintic(p*(1.9-0.32*drift)+vec2(seed*2.3,-flameAge*1.35));
    smoke*=(0.32+0.68*billow)*bxSmoothstep(0.022,0.18,age)*(1-bxSmoothstep(0.47,0.99,life))*0.37*window;
    vec4 pressure=blastPressure(r,p/max(r,0.001),px,age*0.88,radius,seed)*window;
    float reflectedLight=bell(r/(radius*(0.27+0.65*drift)))*(1.0-bxSmoothstep(0.065,0.28,flameAge));
    vec3 smokeColor=mix(mix(Smoke,Navy,0.5*billow),Cobalt,0.22*reflectedLight);
    vec4 col=over(pressure,vec4(smokeColor*smoke,smoke));
    float flameWindow=1-bxSmoothstep(0.76,0.89,r*2.0/uMaterial._Size);
    col=over(col,groundFlames(p,px,flameAge,radius,heading)*flameWindow);

    float embers=0.0;
    for(int i=0;i<7;i++)
    {
        float angle=(float(i)+variation(i+110)*0.82)*TAU/7.0+seed;
        vec2 way=normalize(vec2(cos(angle),sin(angle))+heading*0.17);
        float flight=max(0.0,age*0.86-variation(i+130)*0.043);
        float travel=radius*(0.63+0.55*variation(i+151))*(1-exp(-flight*10.6));
        vec2 delta=p-way*travel;
        float width=mix(0.021,0.039,variation(i+173))+px;
        float streak=width*(1.7+2.8*exp(-flight*8.0));
        vec2 spark=vec2(dot(delta,way)/streak,dot(delta,vec2(-way.y,way.x))/width);
        embers=max(embers,exp(-dot(spark,spark))*bxSmoothstep(0.003,0.034,flight)*(1-bxSmoothstep(0.11,0.34,flight)));
    }
    col=over(col,vec4(Halo*embers*0.58,embers*0.58));

    float flashRadius=radius*mix(0.09,0.32,bxSmoothstep(0.0,0.048,age));
    float flash=(1-bxSmoothstep(flashRadius*0.23,flashRadius,r))*(1-bxSmoothstep(0.023,0.081,age))*0.94;
    float bloom=bell(r/(flashRadius*1.42))*exp(-age*19.0)*0.46;
    col=over(col,vec4(Halo*bloom,bloom));
    return over(col,vec4(Halo*flash,flash));
}

// One lasting cursed fire: the authored flames, flickering between frames like vanilla fire but swaying, and
// rising out of the ground when the burst lights it. Mip levels follow the undistorted quad, so squashing a young
// flame never selects a blurred, square mip.
vec2 gradX, gradY;
vec4 flameTexel(sampler2D tex, vec2 uv) { return textureGrad(tex,vec2(uv.x,1-uv.y),gradX,gradY); }
vec4 frame(vec2 uv, float time)
{
    if(any(lessThan(uv,vec2(0)))||any(greaterThan(uv,vec2(1)))) return vec4(0);
    float f=mod(time*6.0,3.0);
    float blend=bxSmoothstep(0.0,0.12,fract(f));
    vec4 a,b;
    if(f<1) { a=flameTexel(_FlameC,uv); b=flameTexel(_FlameA,uv); }
    else if(f<2) { a=flameTexel(_FlameA,uv); b=flameTexel(_FlameB,uv); }
    else { a=flameTexel(_FlameB,uv); b=flameTexel(_FlameC,uv); }
    a.rgb*=a.a; b.rgb*=b.a;
    return mix(a,b,blend);
}
vec4 standing(vec2 uv)
{
    float t=uMaterial._Phase, s=uMaterial._Seed;
    float grow=saturate(uMaterial._Grow);
    gradX=dFdx(uv); gradY=dFdy(uv);
    if(uMaterial._Flip>0.5) uv.x=1-uv.x;
    float rise=bxSmoothstep(0,1,grow);
    // Grow from the base: stretch the sampled height while young.
    uv.y=uv.y/max(rise,0.05);
    float breathe=1+0.05*sin(t*7.3+s*3.1);
    uv.y/=breathe;
    uv.x=(uv.x-0.5)/(1+0.03*sin(t*5.1+s))+0.5;
    uv.x+=sin(uv.y*4.2-t*4.6+s)*0.035*uv.y*uv.y;
    return frame(uv,t+s)*bxSmoothstep(0,0.25,grow);
}

void main()
{
    vec4 col;
    float size=uMaterial._Size;
    vec2 p=(vUv-0.5)*size;
    float px=sqrt(max(dot(dFdx(p),dFdx(p)),dot(dFdy(p),dFdy(p))));
    if(uMaterial._Mode>2.5) col=standing(vUv);
    else if(uMaterial._Mode>1.5) col=burst(p,px);
    else col=orb(p,px);
    float bound=max(abs(vUv.x-0.5),abs(vUv.y-0.5))*2;
    outColor=col*(1-bxSmoothstep(0.95,1,bound))*uMaterial._Opacity;
}
