cbuffer PhotoVars : register(b0)
{
    float4x4 ViewProj;

    float4 Tint;

    float4 Frame;
}

Texture2D Photo : register(t0);
SamplerState Samp : register(s0);

struct VS_Input
{
    float3 Position : POSITION;
    float2 Uv : TEXCOORD0;
};

struct PS_Input
{
    float4 Pos : SV_POSITION;
    float2 Uv : TEXCOORD0;
};

PS_Input VSMain(VS_Input input)
{
    PS_Input o;
    o.Pos = mul(float4(input.Position, 1.0), ViewProj);
    o.Uv = input.Uv;
    return o;
}

float4 PSMain(PS_Input input) : SV_TARGET
{
    float b = Frame.x;
    if (Frame.y > 0.5)
    {

        float2 d = min(input.Uv, 1.0 - input.Uv);
        float m = min(d.x, d.y);
        if (m < b * 0.25) return float4(0.06, 0.06, 0.07, Tint.a);
        if (m < b) return float4(0.96, 0.96, 0.94, Tint.a);
    }

    float2 uv = saturate((input.Uv - b) / max(1.0 - 2.0 * b, 1e-4));
    float4 c = Photo.Sample(Samp, uv);
    float a = c.a * Tint.a;
    if (Frame.z > 0.0)
    {
        float r = length((input.Uv - 0.5) * 2.0);
        a *= 1.0 - smoothstep(1.0 - Frame.z, 1.0, r);
    }
    return float4(c.rgb * Tint.rgb, a);
}
