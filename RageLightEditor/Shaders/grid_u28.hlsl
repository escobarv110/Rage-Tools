cbuffer GridVars : register(b0)
{
    float4x4 ViewProj;
    float4 CamPos;
    float4 Spacing;
    float4 Fade;
    float4 MinorCol;
    float4 MajorCol;
    float4 AxisXCol;
    float4 AxisYCol;
}

struct PS_Input
{
    float4 Pos : SV_POSITION;
    float3 World : TEXCOORD0;
};

PS_Input VSMain(uint id : SV_VertexID)
{
    float2 c[6] = { float2(-1, -1), float2(1, -1), float2(1, 1), float2(-1, -1), float2(1, 1), float2(-1, 1) };
    float3 w = float3(c[id] * Spacing.z, 0.0);
    PS_Input o;
    o.Pos = mul(float4(w, 1.0), ViewProj);
    o.World = w;
    return o;
}

float LineMask(float2 p, float spacing)
{
    float2 q = p / spacing;
    float2 d = max(fwidth(q), 1e-6);
    float2 g = abs(frac(q - 0.5) - 0.5) / d;
    return 1.0 - saturate(min(g.x, g.y) - 0.25);
}

float CellFade(float2 p, float spacing)
{
    float2 d = fwidth(p / spacing);
    float px = 1.0 / max(max(d.x, d.y), 1e-6);
    return saturate((px - 6.0) / 10.0);
}

float4 PSMain(PS_Input i) : SV_TARGET
{
    float2 p = i.World.xy;
    float minor = LineMask(p, Spacing.x) * CellFade(p, Spacing.x);
    float major = LineMask(p, Spacing.y) * CellFade(p, Spacing.y);
    float ax = 1.0 - saturate(abs(p.y) / max(fwidth(p.y), 1e-6) - 0.35);
    float ay = 1.0 - saturate(abs(p.x) / max(fwidth(p.x), 1e-6) - 0.35);

    float dist = length(i.World - CamPos.xyz);
    float fade = saturate(1.0 - (dist - Fade.x) / max(Fade.y, 1e-3));
    float edge = saturate((Spacing.z - max(abs(p.x), abs(p.y))) / (Spacing.z * 0.08));
    fade *= edge;

    float a = max(max(minor * MinorCol.a, major * MajorCol.a), max(ax, ay)) * fade;
    if (a <= 0.002) discard;
    float3 rgb;
    if (max(ax, ay) > 0.001) rgb = ax >= ay ? AxisXCol.rgb : AxisYCol.rgb;
    else if (major > 0.001) rgb = MajorCol.rgb;
    else rgb = MinorCol.rgb;
    return float4(rgb, a);
}
