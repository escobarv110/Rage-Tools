cbuffer CloudVars : register(b0)
{
    float4x4 ViewProjNoTrans;
    float4 Scale;
    float4 SunColour;
    float4 UvOffset;
    float4 CamPos;

    float4 SunDir;
    float4 CloudColour;
    float4 LightColour;
    float4 AmbientColour;
    float4 SkyColour;
    float4 BounceColour;
    float4 EastMinusWestColour;
    float4 WestColour;
    float4 DensityShiftScale;
    float4 ScaleDiffuseFillAmbientWrap;
    float4 Piercing;
    float4 Rescale12;
    float4 Rescale3Offset1;
    float4 Offset23;
    float4 AnimScale12;
    float4 AnimScale3Flags;
    float4 AnimCombine;
    float4 AnimSculpt;
    float4 AnimBlend;

    float4 GFogParams0;
    float4 GFogParams1;
    float4 GFogParams2;
    float4 GFogSunDir;
    float4 GFogMoonDir;
    float4 GFogColSun;
    float4 GFogColAtmo;
    float4 GFogColGround;
    float4 GFogColHaze;
    float4 GFogColMoon;
}

Texture2D DensityTex : register(t0);
Texture2D DetailDensityTex : register(t1);
Texture2D DetailDensity2Tex : register(t2);
SamplerState WrapLinear : register(s0);

struct VS_Input
{
    float3 Pos : POSITION;
    float3 Nrm : NORMAL;
    float4 Tan : TANGENT;
    float4 Col : COLOR0;
    float4 Col1 : COLOR1;
    float2 Uv : TEXCOORD0;
    float2 Uv1 : TEXCOORD1;
};

struct PS_Input
{
    float4 Pos : SV_POSITION;
    float2 Uv : TEXCOORD0;
    float Alpha : TEXCOORD1;
    float3 Dir : TEXCOORD2;
    float3 Nrm : TEXCOORD3;
    float HazeScale : TEXCOORD4;
    float4 Uv23 : TEXCOORD5;
    float2 DensityAdjust : TEXCOORD6;
};

float ComputeGlobalVolumetricFogValue_Crytek(float3 cameraToWorldPos, out float dist)
{
    const float threshold = 0.01;
    float fullDist = length(cameraToWorldPos);
    dist = max(0, fullDist - GFogParams0.x);
    float deltaZ = cameraToWorldPos.z * (dist / max(fullDist, 1e-4));
    float t = (GFogParams2.z * deltaZ);
    float fogInt = (abs(deltaZ) > threshold) ? (1.0 - exp(-t)) / t : 1.0;
    float val = min(1.0f, GFogParams1.w * dist * fogInt);
    return 1.0 - saturate(exp(val));
}

float4 CalcFogData(float3 eyeRayToPoint, float hazeScale)
{
    if (GFogParams0.w < 0.5) return float4(0, 0, 0, 0);
    float dist = 0.0f;
    float groundFogAmount = ComputeGlobalVolumetricFogValue_Crytek(eyeRayToPoint, dist) * GFogParams2.y;
    float3 nray = normalize(eyeRayToPoint);
    float moonAmount = pow(saturate(dot(nray, GFogMoonDir.xyz)), GFogMoonDir.w);
    float sunAmount = pow(saturate(dot(nray, GFogSunDir.xyz)), GFogSunDir.w);
    float horizonHazeBlend = hazeScale * GFogParams1.y * (1 - groundFogAmount);
    float horizonHazeAmount = horizonHazeBlend * (1 - exp(GFogParams1.x * max(0, dist - GFogParams2.x)));
    float finalFogBlend = saturate(horizonHazeAmount + groundFogAmount);
    float atmosphereBlend = 1.0 - exp(-GFogParams1.z * dist);
    float3 atmosphereAndMoonColor = lerp(GFogColAtmo.rgb, GFogColMoon.rgb, moonAmount);
    float3 atmosphereColor = lerp(atmosphereAndMoonColor, GFogColSun.rgb, sunAmount);
    float3 groundFogAtmoColor = lerp(GFogColGround.rgb, atmosphereColor, atmosphereBlend);
    float3 groundFogHazeAtmoColor = lerp(groundFogAtmoColor, GFogColHaze.rgb, horizonHazeBlend);
    return float4(groundFogHazeAtmoColor, finalFogBlend);
}

float3 HemisphericLighting(float3 N, float hemiIntensity)
{
    float3 light = WestColour.rgb + saturate(N.x * 0.5 + 0.5) * EastMinusWestColour.rgb;
    const float groundWrap = 0.25;
    const float skyWrap = 0.75;
    light += BounceColour.rgb * saturate(-N.z / (1 + groundWrap) + groundWrap / (1 + groundWrap));
    light += SkyColour.rgb * saturate(N.z / (1 + skyWrap) + skyWrap / (1 + skyWrap));
    return hemiIntensity * light;
}

float MieScattering(float3 view, float3 lightDir)
{
    float g = clamp(DensityShiftScale.z, -0.98, 0.98);
    float g2 = g * g;
    float phaseK = (3.0 * (1.0 - g2)) / (2.0 * (2.0 + g2));
    float cosTheta = dot(view, lightDir);
    float phase = (1.0 + cosTheta * cosTheta) / pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5);
    return phase * phaseK;
}

PS_Input VSMain(VS_Input i)
{
    PS_Input o;

    float3 p = i.Pos * Scale.xyz;
    o.Pos = mul(float4(p, 1.0), ViewProjNoTrans);

    o.Pos.z = max(o.Pos.z, 1e-6 * o.Pos.w);
    float2 scroll = UvOffset.xy;
    o.Uv = i.Uv * Rescale12.xy + Rescale3Offset1.zw + scroll * AnimScale12.xy;
    o.Uv23.xy = i.Uv * Rescale12.zw + Offset23.xy + scroll * AnimScale12.zw;
    o.Uv23.zw = i.Uv * Rescale3Offset1.xy + Offset23.zw + scroll * AnimScale3Flags.xy;
    o.DensityAdjust = float2(i.Col.g, i.Col.b);

    o.Alpha = i.Col.a * Scale.w;
    o.Dir = p;
    o.Nrm = i.Nrm;
    o.HazeScale = 1.0 - i.Col.r;
    return o;
}

float4 PSMain(PS_Input i) : SV_TARGET
{

    float density;
    if (AnimScale3Flags.z > 0.5)
    {
        float3 animDensity;
        animDensity.x = DensityTex.Sample(WrapLinear, i.Uv).g;
        animDensity.y = DetailDensityTex.Sample(WrapLinear, i.Uv23.xy).g;
        animDensity.z = DetailDensity2Tex.Sample(WrapLinear, i.Uv23.zw).g;
        animDensity = 1.0 - animDensity * animDensity;
        animDensity *= AnimBlend.xyz;
        float3 combineDensity = animDensity * AnimCombine.xyz;
        float3 sculptDensity = animDensity * AnimSculpt.xyz;
        float combinedDensity = i.DensityAdjust.x * max(combineDensity.x, max(combineDensity.y, combineDensity.z));
        float sculptedDensity = i.DensityAdjust.y + sculptDensity.x + sculptDensity.y + sculptDensity.z;
        density = combinedDensity - ((1.0 - combinedDensity) * sculptedDensity);
    }
    else
    {
        density = 1.0 - DensityTex.Sample(WrapLinear, i.Uv).g;
    }
    float3 view = normalize(i.Dir);
    float3 c;
    float a;
    if (SunDir.w > 0.5)
    {

        density = saturate((density - DensityShiftScale.x) * DensityShiftScale.y);
        a = saturate(density * i.Alpha);
        float3 N = normalize(i.Nrm);
        float3 L = SunDir.xyz;

        float3 fill = HemisphericLighting(N, ScaleDiffuseFillAmbientWrap.y);
        float wrap = ScaleDiffuseFillAmbientWrap.w;
        float wrapLight = saturate((dot(N, L) + wrap) / (1.0 + wrap));
        float3 diffuse = LightColour.rgb * wrapLight * ScaleDiffuseFillAmbientWrap.x;
        c = CloudColour.rgb * (fill + diffuse + AmbientColour.rgb * ScaleDiffuseFillAmbientWrap.z);

        float thickness = 1.0 - density;
        thickness = saturate((1.0 - Piercing.w) + thickness * Piercing.w);

        float3 scattering = MieScattering(view, L) * LightColour.rgb * DensityShiftScale.w * thickness;

        float pierceA = pow(saturate(dot(view, L)), max(Piercing.x, 0.01));
        float3 pv = -view;
        float3 px = dot(pv, L) * L;
        float3 pd = normalize(pv - px + 1e-5);
        float pierceB = saturate(dot(N, pd)) * Piercing.z;
        float pierceC = lerp(pierceB * pierceA, pierceA, pierceA);
        float3 piercing = pierceC * thickness * LightColour.rgb * Piercing.y;
        c += scattering + piercing;
        c *= CloudColour.w;
    }
    else
    {

        a = saturate(density * i.Alpha);
        c = SunColour.rgb;
    }

    float4 fd = CalcFogData(view * 25000.0, i.HazeScale);
    c = lerp(c, fd.rgb, fd.a);
    return float4(c, a);
}
