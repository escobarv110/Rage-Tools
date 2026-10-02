cbuffer ShaftVars : register(b0)
{
    float4x4 ViewProj;
    float4 CameraPos;
    float4 CameraFwd;
    float4 ProjParams;
    float4 Params;
}

Texture2D<float> SceneDepthTex : register(t25);
Texture2DMS<float> SceneDepthMS : register(t26);

struct VS_Input
{
    float3 Pos    : POSITION;
    float4 Centre : TEXCOORD0;
    float4 AxisX  : TEXCOORD1;
    float4 AxisY  : TEXCOORD2;
    float4 AxisD  : TEXCOORD3;
    float4 Colour : COLOR0;
};

struct PS_Input
{
    float4 Pos    : SV_POSITION;
    float3 World  : TEXCOORD0;
    float4 Centre : TEXCOORD1;
    float4 AxisX  : TEXCOORD2;
    float4 AxisY  : TEXCOORD3;
    float4 AxisD  : TEXCOORD4;
    float4 Colour : COLOR0;
};

PS_Input VSMain(VS_Input i)
{
    PS_Input o;
    o.Pos = mul(float4(i.Pos, 1.0), ViewProj);
    o.World = i.Pos;
    o.Centre = i.Centre; o.AxisX = i.AxisX; o.AxisY = i.AxisY; o.AxisD = i.AxisD; o.Colour = i.Colour;
    return o;
}

float SceneViewDistance(float2 screenPos)
{
    float z;
    if (CameraFwd.w > 1.5)      z = SceneDepthMS.Load(int2(screenPos), 0);
    else if (CameraFwd.w > 0.5) z = SceneDepthTex.Load(int3(int2(screenPos), 0));
    else return 1e6;
    if (z <= 0.0) return 1e6;
    return ProjParams.y / (z + ProjParams.x);
}

float4 PSMain(PS_Input i) : SV_TARGET
{
    float3 eye = CameraPos.xyz;
    float3 ray = i.World - eye;
    float rayLen = length(ray);
    float3 rd = ray / max(rayLen, 1e-4);

    float3 X = i.AxisX.xyz, Y = i.AxisY.xyz, D = i.AxisD.xyz;
    float det = dot(X, cross(Y, D));
    if (abs(det) < 1e-9) discard;
    float3 dX = cross(Y, D) / det, dY = cross(D, X) / det, dD = cross(X, Y) / det;
    float3 o = eye - i.Centre.xyz;
    float3 lo = float3(dot(o, dX), dot(o, dY), dot(o, dD));
    float3 ld = float3(dot(rd, dX), dot(rd, dY), dot(rd, dD));

    float3 bmin = float3(-1, -1, 0), bmax = float3(1, 1, 1);
    float3 inv = 1.0 / (abs(ld) > 1e-6 ? ld : (ld >= 0 ? 1e-6 : -1e-6));
    float3 ta = (bmin - lo) * inv, tb = (bmax - lo) * inv;
    float3 tn = min(ta, tb), tf = max(ta, tb);
    float s0 = max(max(tn.x, tn.y), tn.z);
    float s1 = min(min(tf.x, tf.y), tf.z);
    s0 = max(s0, 0.0);

    float2 px = i.Pos.xy;
    float viewDist = SceneViewDistance(px);
    float sceneAlong = viewDist / max(dot(rd, CameraFwd.xyz), 0.05);
    s1 = min(s1, sceneAlong);
    if (s1 <= s0 + 1e-4) discard;

    float soft = saturate(i.Centre.w);
    int densityType = (int)round(i.Colour.a);
    float pathLen = s1 - s0;
    float z0 = saturate(lo.z + ld.z * s0);
    float z1 = saturate(lo.z + ld.z * s1);
    float d0 = 1.0 - z0;
    float d1 = 1.0 - z1;
    float integral1 = (d0 + d1) * 0.5;
    float integral2 = (d0 * d0 + d0 * d1 + d1 * d1) / 3.0;
    float acc = pathLen;
    if (densityType == 4 || densityType == 5) acc = pathLen * integral1;
    else if (densityType == 6 || densityType == 7) acc = pathLen * integral2;
    if (densityType != 0) acc *= lerp(1.0, acc, soft);
    if (Params.w > 0.5) acc = pathLen;
    float3 rad = i.Colour.rgb * acc * Params.z;
    return float4(rad, 1.0);
}
