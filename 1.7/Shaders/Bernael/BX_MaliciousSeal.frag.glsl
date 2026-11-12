#version 450
// Mortis's review (2026-11-09): four much bigger spikes, and a minimalist eye in place of the rune. The original
// timing and palette remain.
layout(location = 0) in vec2 vUv;
layout(location = 0) out vec4 outColor;
#include "BX_ShaderCommon.glsl"
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    float _Phase; // @default = 0.0
    float _Inscribe; // @default = 1.0
    float _Close; // @default = 0.0
    float _Opacity; // @default = 1.0
    float _Seed; // @default = 0.0
    float _Mode; // @default = 2.0
    float _Length; // @default = 2.0
    float _Size; // @default = 1.0
} uMaterial;


// Caligo palette: a navy seal with pale blue detail, a white contour and restrained glow.
const vec3 Navy = vec3(0.086,0.133,0.169);
const vec3 Deep = vec3(0.51,0.71,0.859);
const vec3 Pale = vec3(0.725,0.851,0.929);
// The emblem is measured in its ring's radius, and its quad reaches this far from the middle, clear of the spikes'
// tips and glow. MaliciousSealMapComponent sizes the brand by it.
const float Extent = 2.1;
// Contour and glow widths in cells, thinner than Dire Orb's since the brand is small.
// The contour stays thinner than the navy ring, so the palette reads at a distance instead of a white outline.
const vec2 BrandLine = vec2(0.015,0.026);
const vec2 ChainLine = vec2(0.022,0.04);
// MaliciousSealMapComponent.ChainWidth, the chain quad's width in cells.
const float ChainWidth = 0.8;
// Shapes measured from the concept, with the ring's radius as 1 and +y up.
const float RingHalf = 0.11;
vec4 over(vec4 back, vec4 front)
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
    return mix(b,a,h)-k*h*(1-h);
}
float segment(vec2 p, vec2 a, vec2 b)
{
    vec2 pa=p-a, ba=b-a;
    return length(pa-ba*saturate(dot(pa,ba)/dot(ba,ba)));
}
float box(vec2 p, vec2 size, float round)
{
    vec2 q=abs(p)-size+round;
    return length(max(q,0))+min(max(q.x,q.y),0)-round;
}
// Close enough to an ellipse's distance for fills, contours and glow while its radii are alike.
float ellipse(vec2 p, vec2 radii)
{
    float k0=length(p/radii);
    float k1=length(p/(radii*radii));
    return k0*(k0-1)/max(k1,1e-5);
}
// Narrowing from topWidth at y = +height to bottomWidth at y = -height.
float trapezoid(vec2 p, float bottomWidth, float topWidth, float height)
{
    vec2 k1=vec2(topWidth,height), k2=vec2(topWidth-bottomWidth,2*height);
    p.x=abs(p.x);
    vec2 ca=vec2(p.x-min(p.x,p.y<0?bottomWidth:topWidth),abs(p.y)-height);
    vec2 cb=p-k1+k2*saturate(dot(k1-p,k2)/dot(k2,k2));
    float s=cb.x<0&&ca.y<0?-1:1;
    return s*sqrt(min(dot(ca,ca),dot(cb,cb)));
}
// A circle of radius ra at a, joined by tangents to one of radius rb at b.
float cone(vec2 p, vec2 a, vec2 b, float ra, float rb)
{
    vec2 ab=b-a;
    float h=max(length(ab),1e-4);
    vec2 d=ab/h;
    p-=a;
    vec2 q=vec2(abs(d.x*p.y-d.y*p.x),dot(p,d));
    float s=clamp((ra-rb)/h,-0.99,0.99);
    float c=sqrt(1-s*s);
    float k=c*q.y-s*q.x;
    if(k<0) return length(q)-ra;
    if(k>c*h) return length(q-vec2(0,h))-rb;
    return c*q.x+s*q.y-ra;
}
// A diamond along +y with points at a and b, widest (width) at m.
float kite(vec2 p, float a, float m, float b, float width)
{
    p.x=abs(p.x);
    vec2 A=vec2(0,a), M=vec2(width,m), B=vec2(0,b);
    float d=min(segment(p,A,M),segment(p,M,B));
    bool inside=(M.x-A.x)*(p.y-A.y)-(M.y-A.y)*(p.x-A.x)>0&&(B.x-M.x)*(p.y-M.y)-(B.y-M.y)*(p.x-M.x)>0;
    return inside?-d:d;
}

// Fills a shape, rims it in a solid white contour and wraps that in a soft white glow, as Dire Orb paints
// its flames. d is the distance to the shape and px one screen pixel, both in the shape's units, and
// widths holds its contour and glow widths.
vec4 paint(float d, vec3 fill, float px, vec2 widths)
{
    float aa=px*0.75;
    float stroke=max(widths.x,px*1.3);
    float halo=max(widths.y,px*2);
    float inside=1-bxSmoothstep(-aa,aa,d);
    float rim=1-bxSmoothstep(-aa,aa,d-stroke);
    vec3 c=mix(vec3(1,1,1),fill,inside);
    float g=max(d-stroke,0);
    float glow=0.45*exp2(-g*g/(halo*halo)*1.4427)*(1-bxSmoothstep(halo*1.8,halo*2.8,g))*(1-rim);
    return vec4(c*rim+glow,rim+glow);
}

// Four long cardinal spikes, reaching out three quarters of the ring's radius beyond it.
float ringShape(vec2 p)
{
    float d=abs(length(p)-1)-RingHalf;
    // Folded into one quadrant, so each line draws a spike on every side.
    vec2 q=abs(p);
    d=min(d,kite(q,0.8,1,1.78,0.165));
    d=min(d,kite(q.yx,0.8,1,1.78,0.165));
    return d;
}

// A lens of two circles of radius r whose centres lie d either side of the middle along y: its points lie on x.
float vesica(vec2 p, float r, float d)
{
    p=abs(p.yx);
    float b=sqrt(r*r-d*d);
    return ((p.y-b)*d>p.x*b)?length(p-vec2(0.0,b)):length(p-vec2(-d,0.0))-r;
}
// An almond eye and a single dark pupil, readable at normal play zoom.
float eyeShape(vec2 p) { return vesica(p,0.876,0.576); }
float pupilShape(vec2 p) { return length(p)-0.2; }
// Heats a colour through the palette's blue to white, so a flash never passes through grey.
vec3 heat(vec3 c, float flash)
{
    return flash<0.5?mix(c,Deep,flash*2):mix(Deep,vec3(1,1,1),flash*2-1);
}

struct Pose
{
    float ringScale;
    float squash;
    float pulse;
    float flash;
};

vec4 emblem(vec2 p, float px, vec2 widths, Pose o)
{
    // A translucent navy plate inside the ring gives the seal one dark mass, like a wax seal.
    float plate=(length(p/o.ringScale)-(1-RingHalf))*o.ringScale;
    vec4 col=vec4(heat(Navy,o.flash*0.5),1)*(1-bxSmoothstep(-px,px,plate))*0.72;
    float ring=ringShape(p/o.ringScale)*o.ringScale;
    col=over(col,paint(ring,heat(Navy,o.flash),px,widths));
    // The eye flinches with each pulse (squash) and its white brightens.
    vec2 local=p/vec2(1,o.squash);
    vec3 white=mix(Deep,Pale,0.45+o.pulse*0.4);
    col=over(col,paint(eyeShape(local)*o.squash,heat(white,o.flash),px,widths*0.8));
    float pupil=1-bxSmoothstep(-px,px,pupilShape(local)*o.squash);
    return over(col,vec4(Navy*pupil,pupil));
}

// The chain the seal throws from a sealed corpse (uv.x 0) to the pawn it spreads to (uv.x 1). Links run
// out as it grows; then a pulse of light runs down it to the victim over the first half of its release,
// and it falls apart link by link from the corpse outward.
vec4 chain(vec2 uv, float px)
{
    float L=max(uMaterial._Length,0.3);
    float s=uv.x*L;
    float y=(uv.y-0.5)*ChainWidth;
    float close=saturate(uMaterial._Close);
    float reach=saturate(uMaterial._Inscribe)*L;
    float pulse=saturate(close/0.5)*L;
    float dissolve=saturate((close-0.5)/0.5)*(L+0.3);
    // A slight sway, pinned at both ends.
    y-=0.04*sin(saturate(s/L)*UNITY_PI)*sin(s*2.3-uMaterial._Phase*4+uMaterial._Seed);
    // Face-on links alternate with links seen edge-on, each reaching into its neighbours.
    const float Pitch=0.4;
    float faces=1e3, edges=1e3;
    float nearest=floor(s/Pitch);
    for(int k=-1;k<=1;k++)
    {
        float n=nearest+k;
        float at=(n+0.5)*Pitch;
        float size=saturate((reach-at)/0.3)*(1-saturate((dissolve-at)/0.3));
        if(n<0||at>L-0.12||size<0.01) continue;
        vec2 local=vec2(s-at,y)/size;
        vec2 bar=vec2(local.x-clamp(local.x,-0.1,0.1),local.y);
        if(fmod(n,2)<0.5) faces=min(faces,(abs(length(bar)-0.1)-0.026)*size);
        else edges=min(edges,(length(vec2(local.x-clamp(local.x,-0.19,0.19),local.y))-0.034)*size);
    }
    float light=exp2(-sq((s-pulse)/0.3))*step(0.001,close)*(1-saturate((close-0.5)/0.15));
    vec3 fill=mix(Navy,vec3(1,1,1),light);
    return over(paint(faces,fill,px,ChainLine),paint(edges,fill,px,ChainLine));
}

// The seal branded over a victim's head. It stamps down white hot (MaliciousSealMapComponent shrinks its
// quad onto the pawn and lands it at 0.55 of uMaterial._Inscribe), then the eye pulses once a second.
vec4 brand(vec2 p, float px, vec2 widths)
{
    float t=uMaterial._Phase, stamp=saturate(uMaterial._Inscribe), fade=saturate(uMaterial._Close);
    p.y-=0.03*sin(t*UNITY_PI);
    float c=fract(t+0.45);
    float jolt=exp2(-c*12);
    Pose o;
    o.ringScale=1+0.025*exp2(-c*10);
    o.squash=1-0.05*jolt;
    o.pulse=(0.18+0.82*exp2(-c*8))*(1-fade);
    o.flash=stamp<0.55?0.6+0.4*sq(stamp/0.55):exp2(-sq((stamp-0.55)/0.2)*3);
    vec4 col=emblem(p,px,widths,o);
    // As it lands, a thin ring of pale light runs out from the seal.
    float k=saturate((stamp-0.55)/0.45);
    if(stamp>0.55&&k<1)
    {
        float wave=abs(length(p)-mix(1.08,1.55,1-sq(1-k)))-0.035*(1-k);
        col=over(paint(wave,Pale,px,widths*(1-k*0.6))*(1-k),col);
    }
    return col*bxSmoothstep(0,0.3,stamp)*(1-fade);
}

vec4 bxFragment(vec2 uv)
{
    // Emblem units and chain cells, and one screen pixel in each, measured outside any branch.
    vec2 e=(uv-0.5)*2*Extent;
    vec2 c=vec2(uv.x*max(uMaterial._Length,0.3),uv.y*ChainWidth);
    float pxE=sqrt(max(dot(dFdx(e),dFdx(e)),dot(dFdy(e),dFdy(e))));
    float pxC=sqrt(max(dot(dFdx(c),dFdx(c)),dot(dFdy(c),dFdy(c))));
    float cells=max(uMaterial._Size,0.01)/(2*Extent);
    bool strip=uMaterial._Mode<1.5;
    vec4 col=strip?chain(uv,pxC):brand(e,pxE,BrandLine/cells);
    // The brand fades on a circle and the chain at its sides, so a glow never shows the quad's outline.
    vec2 edge=abs(uv-0.5)*2;
    col*=1-bxSmoothstep(0.93,1,strip?edge.y:length(edge));
    return col*uMaterial._Opacity;
}

void main() { outColor=bxFragment(vUv); }
