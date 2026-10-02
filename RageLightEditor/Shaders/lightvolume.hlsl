cbuffer VolumeVars : register(b0)
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
    float3 Pos     : POSITION;
    float4 LightPR : TEXCOORD0;
    float4 DirCosO : TEXCOORD1;
    float4 InCosI  : TEXCOORD2;
    float4 OutExp  : TEXCOORD3;
    float4 Misc    : TEXCOORD4;
};

struct PS_Input
{
    float4 Pos     : SV_POSITION;
    float3 World   : TEXCOORD0;
    float4 LightPR : TEXCOORD1;
    float4 DirCosO : TEXCOORD2;
    float4 InCosI  : TEXCOORD3;
    float4 OutExp  : TEXCOORD4;
    float4 Misc    : TEXCOORD5;
};

PS_Input VSMain(VS_Input i)
{
    PS_Input o;
    o.Pos = mul(float4(i.Pos, 1.0), ViewProj);
    o.World = i.Pos;
    o.LightPR = i.LightPR; o.DirCosO = i.DirCosO; o.InCosI = i.InCosI; o.OutExp = i.OutExp; o.Misc = i.Misc;
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

float RagePowApprox(float a, float b)
{
    return a / ((1.0 - b) * a + b);
}

bool InsideCone(float3 pt, float3 a, float cosSq)
{
    float d = dot(pt, a);
    return d >= 0.0 && d * d >= cosSq * dot(pt, pt);
}

float4 PSMain(PS_Input i) : SV_TARGET
{
    float3 eye = CameraPos.xyz;
    float3 ray = i.World - eye;
    float3 rd = ray / max(length(ray), 1e-4);

    float3 P = i.LightPR.xyz;
    float R = max(i.LightPR.w, 0.01);
    float3 q = eye - P;
    float qv = dot(q, rd);
    float qq = dot(q, q);
    float disc = qv * qv - (qq - R * R);
    if (disc <= 0.0) discard;
    float sq = sqrt(disc);
    float lo = max(-qv - sq, 0.0);
    float hi = -qv + sq;

    float sceneAlong = SceneViewDistance(i.Pos.xy) / max(dot(rd, CameraFwd.xyz), 0.05);
    hi = min(hi, sceneAlong);
    if (hi <= lo + 1e-4) discard;

    bool spot = i.Misc.y > 1.5 && i.Misc.y < 2.5;
    float3 a = normalize(i.DirCosO.xyz);
    float cosO = i.DirCosO.w;
    float cosI = i.InCosI.w;
    if (spot)
    {
        float c2 = cosO * cosO;
        float va = dot(rd, a), qa = dot(q, a);
        float A = va * va - c2;
        float B = va * qa - c2 * qv;
        float C = qa * qa - c2 * qq;
        float r1 = lo, r2 = hi;
        float g = B * B - A * C;
        if (g >= 0.0 && abs(A) > 1e-7)
        {
            float sg = sqrt(g);
            r1 = (-B - sg) / A;
            r2 = (-B + sg) / A;
            if (r1 > r2) { float t = r1; r1 = r2; r2 = t; }
        }
        float p0 = lo, p1 = clamp(r1, lo, hi), p2 = clamp(r2, lo, hi), p3 = hi;
        float s0 = 1e9, s1 = -1e9;
        if (p1 > p0 && InsideCone(q + rd * ((p0 + p1) * 0.5), a, c2)) { s0 = min(s0, p0); s1 = max(s1, p1); }
        if (p2 > p1 && InsideCone(q + rd * ((p1 + p2) * 0.5), a, c2)) { s0 = min(s0, p1); s1 = max(s1, p2); }
        if (p3 > p2 && InsideCone(q + rd * ((p2 + p3) * 0.5), a, c2)) { s0 = min(s0, p2); s1 = max(s1, p3); }
        if (s1 <= s0 + 1e-4) discard;
        lo = s0; hi = s1;
    }

    const int steps = 24;
    float dt = (hi - lo) / steps;
    float invSqrR = 1.0 / (R * R);
    float coneScale = 1.0 / max(cosI - cosO, 1e-4);
    float coneOffset = -cosO * coneScale;
    float accum = 0.0;
    [loop]
    for (int j = 0; j < steps; j++)
    {
        float3 pt = q + rd * (lo + (j + 0.5) * dt);
        float distSq = dot(pt, pt);
        float att = RagePowApprox(saturate(1.0 - distSq * invSqrR), i.Misc.x);
        if (spot)
        {
            float cosAng = dot(pt * rsqrt(max(distSq, 1e-8)), a);
            att *= saturate(cosAng * coneScale + coneOffset);
        }
        accum += att;
    }

    float3 temp1 = q - rd * min(0.0, qv);
    float temp2 = 1.0 - dot(temp1, temp1) * invSqrR;
    float3 grad = lerp(i.OutExp.rgb, i.InCosI.rgb, RagePowApprox(saturate(temp2 * temp2), max(i.OutExp.w, 1e-3)));

    float3 rad = grad * max(1.0, hi - lo) * (accum / steps) * Params.x;
    return float4(rad, 1.0);
}
