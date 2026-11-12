// Preserve the ShaderLab algorithms, including reversed smoothstep edges.
float bxSmoothstep(float a, float b, float x) { float t = clamp((x-a)/(b-a),0.0,1.0); return t*t*(3.0-2.0*t); }
vec2 bxSmoothstep(float a, float b, vec2 x) { vec2 t = clamp((x-a)/(b-a),0.0,1.0); return t*t*(3.0-2.0*t); }
vec3 bxSmoothstep(float a, float b, vec3 x) { vec3 t = clamp((x-a)/(b-a),0.0,1.0); return t*t*(3.0-2.0*t); }
vec4 bxSmoothstep(float a, float b, vec4 x) { vec4 t = clamp((x-a)/(b-a),0.0,1.0); return t*t*(3.0-2.0*t); }
#define saturate(x) clamp(x,0.0,1.0)
// Captures and uploaded textures are top-down in Waylith; effect geometry uses Unity UVs.
#define tex2D(tex,uv) texture(tex,vec2((uv).x,1.0-(uv).y))
#define tex2Dbias(tex,uv) texture(tex,vec2((uv).x,1.0-(uv).y),(uv).w)
#define UNITY_PI 3.14159265358979323846
// A random value in [0,1) per integer lattice point, from integer arithmetic. Value noise built on
// fract(sin(dot(p,k))*43758) breaks into rectangles: the compiler may round the shared corner of two neighbouring
// cells differently (dot(i+(1,0),k) against dot(i,k)+k.x), and the sine of so large an argument turns that last
// bit into a different value on each side of every lattice line.
float bxHash(ivec2 cell)
{
    uvec2 q=uvec2(cell);
    uint h=q.x*0x8da6b343u^q.y*0xd8163841u;
    h=(h^(h>>16))*0x7feb352du;
    h=(h^(h>>15))*0x846ca68bu;
    h^=h>>16;
    return float(h>>8)*(1.0/16777216.0);
}
// Smooth value noise in [0,1) on the integer lattice; seamless across cells.
float bxValueNoise(vec2 p)
{
    vec2 lattice=floor(p), f=p-lattice;
    ivec2 i=ivec2(lattice);
    f=f*f*(3.0-2.0*f);
    return mix(mix(bxHash(i),bxHash(i+ivec2(1,0)),f.x),mix(bxHash(i+ivec2(0,1)),bxHash(i+ivec2(1,1)),f.x),f.y);
}
// The same with a quintic fade, as BX_FoldShift's softNoise.
float bxValueNoiseQuintic(vec2 p)
{
    vec2 lattice=floor(p), f=p-lattice;
    ivec2 i=ivec2(lattice);
    f=f*f*f*(f*(f*6.0-15.0)+10.0);
    return mix(mix(bxHash(i),bxHash(i+ivec2(1,0)),f.x),mix(bxHash(i+ivec2(0,1)),bxHash(i+ivec2(1,1)),f.x),f.y);
}
