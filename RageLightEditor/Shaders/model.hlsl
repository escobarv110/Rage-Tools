cbuffer SceneVars : register(b0)
{
    float4x4 ViewProj;
    float4 CameraPos;
    float4 AmbientColour;
    uint LightCount;
    uint RenderMode;
    float LightsMultiplier;
    float Exposure;
    float4x4 ShadowMatrices[8];

    float3 GlobalLightDir; float GlobalLightHdr;
    float4 LightDirColour;
    float4 LightDirAmbColour;
    float4 LightNaturalAmbUp;
    float4 LightNaturalAmbDown;
    float4 LightArtificialAmbUp;
    float4 LightArtificialAmbDown;
    uint UseTimecycle;
    uint UseSunShadow;
    float SunShadowStrength;
    float AmbientDownWrap;
    float OoOnePlusAmbientDownWrap;
    float3 FogColour;
    float FogDensity;
    float FogStart;
    float SceneTime;

    float BumpTiltLimit;

    float ShadowJitterOff;

    float ScenePad1, ScenePad2, ScenePad3;
    float4x4 SunMatrix;
    float4 SunPos;

    float4 ShadowQuality;

    float4 WaterParams;

    float4 WaterFogParams;

    float4 ProjParams;

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

    float4x4 SunCascadeMatrix[4];
    float4 SunCascadeDepths;
    float4 SunCascadeTexel;
    float4 SunCascadeParams;

    float4 ClipPlane;
    float4 ReflectionParams;

    float4 ReflectRect0;
    float4 ReflectRect1;
    float4 ReflectScale;

    float4 EyeRoomParams;
    float4 EyeRoomScales;
    float4 EyeRoomAmbUp;
    float4 EyeRoomAmbDown;

    float4 CycleArtIntAmbUp;
    float4 CycleArtIntAmbDown;

    float4 InteriorAmbParams;
}

cbuffer ObjectVars : register(b1)
{
    float4x4 World;
    float4 MatDiffuse;
    float Bumpiness;
    float EmissiveMult;
    float SpecIntensity;
    float FadeAlpha;
    uint HasDiffuseTex;
    uint HasBumpTex;
    uint HasSpecTex;
    uint AlphaMode;
    float3 SpecMapIntMask;
    uint IsSelectedMesh;
    uint MeshLightCount;
    float SpecFalloffMult;
    float SpecFresnel;
    float SpecFresnelMult;

    float4 DetailSettings;
    uint HasDetailTex;
    uint IsTerrain;
    uint TerrainBlendMode;
    uint HasTintPalette;
    float TintPaletteV;
    uint DecalKind;
    float IsMirror;
    uint HasLayerBump;

    float4 AnimUV0;
    float4 AnimUV1;
    float4 DecalMask;

    float4 AmbientScales;
    float4 ArtIntAmbUp;
    float4 ArtIntAmbDown;
    float4 L2Params;

    float4 FurParams;
    float4 FurParams2;
    float4 FurParams3;
    float4 FurParams4;
    uint4 MeshLightIndices[16];
}

float2 AnimateUVs(float2 uv)
{
    return float2(dot(AnimUV0.xyz, float3(uv, 1.0)),
                  dot(AnimUV1.xyz, float3(uv, 1.0)));
}

struct Light
{
    float3 Position;           float Intensity;
    float3 Colour;             float Falloff;
    float3 Direction;          float FalloffExponent;
    float3 TangentX;           float ConeInnerAngle;
    float3 TangentY;           float ConeOuterAngle;
    float3 CapsuleExtent;      uint Type;
    float3 CullingPlaneNormal; float CullingPlaneOffset;
    uint CullingPlaneEnable;   float ProjTexIndex; float ShadowSlot; float ShadowBlur;
    uint Flags;                float LightPad0; float LightPad1; float LightPad2;
};

#define LF_CAST_SHADOWS      (1u << 6)
#define LF_NO_SPECULAR       (1u << 13)
#define LF_DONT_LIGHT_ALPHA  (1u << 23)

StructuredBuffer<Light> Lights : register(t9);
Texture2DArray SunShadowMap : register(t10);

Texture2D DiffuseTex : register(t0);
Texture2D BumpTex : register(t1);
Texture2D SpecTex : register(t2);
Texture2D ProjTex0 : register(t3);
Texture2D ProjTex1 : register(t4);
Texture2D ProjTex2 : register(t5);
Texture2D ProjTex3 : register(t6);
Texture2DArray ShadowSpotArray : register(t7);
TextureCubeArray ShadowCubeArray : register(t8);

Texture2D DetailTex : register(t11);

Texture2D LayerTex0 : register(t12);
Texture2D LayerTex1 : register(t13);
Texture2D LayerTex2 : register(t14);
Texture2D LayerTex3 : register(t15);
Texture2D LayerBump0 : register(t16);
Texture2D LayerBump1 : register(t17);
Texture2D LayerBump2 : register(t18);
Texture2D LayerBump3 : register(t19);
Texture2D TerrainMaskTex : register(t20);
Texture2D TintPaletteTex : register(t21);

Texture2D WaterBumpTex : register(t22);
Texture2D WaterBump2Tex : register(t23);
Texture2D WaterFogTex : register(t24);

Texture2D<float> SceneDepthTex : register(t25);
Texture2DMS<float> SceneDepthMS : register(t26);

Texture2D SceneColourTex : register(t28);
#define HasSceneColour ScenePad2

Texture2D FurCombo0 : register(t31);
Texture2D FurCombo1 : register(t32);
Texture2D FurCombo2 : register(t33);
Texture2D FurCombo3 : register(t34);
Texture2D FurMaskTex : register(t35);
Texture2D FurHfTex : register(t36);

Texture2D ReflectionTex0 : register(t29);
Texture2D ReflectionTex1 : register(t30);
SamplerState LinearSampler : register(s0);

SamplerState PointSampler : register(s1);

float ComputeGlobalVolumetricFogValue_Crytek(float3 cameraToWorldPos, out float dist)
{
    const float threshold = 0.01;
    float fullDist = length(cameraToWorldPos);
    dist = max(0, fullDist - GFogParams0.x);
    float deltaZ = cameraToWorldPos.z * (dist / max(fullDist, 1e-4));
    float t = (GFogParams2.z * deltaZ);
    float fogInt = (abs(deltaZ) > threshold) ? (1.0 - exp(-t)) / t : 1.0;

    float val = min(1.0f, GFogParams1.w * dist * fogInt);
    float res = 1.0 - saturate(exp(val));
    return res;
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

float3 SampleProjTex(int idx, float2 uv)
{
    if (idx == 0) return ProjTex0.SampleLevel(LinearSampler, uv, 0).rgb;
    if (idx == 1) return ProjTex1.SampleLevel(LinearSampler, uv, 0).rgb;
    if (idx == 2) return ProjTex2.SampleLevel(LinearSampler, uv, 0).rgb;
    return ProjTex3.SampleLevel(LinearSampler, uv, 0).rgb;
}

struct VS_Input
{
    float3 Position : POSITION;
    float3 Normal : NORMAL;
    float4 Tangent : TANGENT;
    float4 Colour : COLOR0;
    float4 Colour1 : COLOR1;
    float2 UV0 : TEXCOORD0;
    float2 UV1 : TEXCOORD1;
};

struct PS_Input
{
    float4 Pos : SV_POSITION;
    float3 WorldPos : TEXCOORD0;
    float3 Normal : NORMAL;
    float4 Tangent : TANGENT;
    float4 Colour : COLOR0;
    float4 Colour1 : COLOR1;
    float2 UV0 : TEXCOORD1;
    float2 UV1 : TEXCOORD2;

    float3 Tint : COLOR2;

    float Clip : SV_ClipDistance0;
};

float3 SrgbToLinear(float3 c);

PS_Input VSMain(VS_Input input)
{
    PS_Input o;
    o.Tint = 1.0;
    if ((HasTintPalette & 3) != 0 && (HasTintPalette & 3) != 3)
    {
        float tu = ((HasTintPalette & 3) == 2) ? input.Colour1.b : input.Colour.b;
        float3 tint = TintPaletteTex.SampleLevel(PointSampler, float2(tu, TintPaletteV), 0).rgb;
        if ((HasTintPalette & 4) == 0) tint = SrgbToLinear(tint);
        o.Tint = tint;
    }
    float4 wpos = mul(float4(input.Position, 1.0), World);

    if (FurParams.w > 0.0)
    {
        if (FurParams2.w >= 0.0)
            wpos = mul(float4(input.Position + input.Normal * FurParams.x, 1.0), World);
        else
        {
            float3 furN = normalize(mul(float4(input.Normal, 0.0), World).xyz);
            if (FurParams2.w < -50.0)
                wpos.xyz += furN * (-FurParams.w * saturate(1.0 - input.Tangent.w));
            else
                wpos.xyz += furN * (FurParams.x * FurParams.w);
        }
    }
    o.WorldPos = wpos.xyz;
    o.Pos = mul(wpos, ViewProj);
    o.Clip = ReflectionParams.x > 0.5 ? dot(ClipPlane.xyz, wpos.xyz) + ClipPlane.w : 1.0;
    o.Normal = normalize(mul(float4(input.Normal, 0.0), World).xyz);
    o.Tangent = float4(normalize(mul(float4(input.Tangent.xyz, 0.0), World).xyz), input.Tangent.w);
    o.Colour = input.Colour;
    o.Colour1 = input.Colour1;
    o.UV0 = input.UV0;
    o.UV1 = input.UV1;
    return o;
}

float4 GetLineSegmentNearestPoint(float3 v, float3 a, float3 b)
{
    float3 ab = b - a;
    float3 av = v - a;
    if (dot(av, ab) <= 0.0f)
    {
        return float4(av, length(av));
    }
    else
    {
        float3 bv = v - b;
        if (dot(bv, ab) >= 0.0f)
        {
            return float4(bv, length(bv));
        }
        else
        {
            float3 abv = cross(ab, av);
            float d = length(abv) / length(ab);
            return float4(normalize(cross(abv, ab)) * d, d);
        }
    }
}

float RagePowApprox(float a, float b)
{
    return a / ((1.0 - b) * a + b);
}

float RageDistanceFalloff(float distSqr, float invMaxDistSqr, float exponent)
{
    return RagePowApprox(saturate(1.0 - distSqr * invMaxDistSqr), exponent);
}

float RageAngularFalloff(float cosAngle, float innerAngle, float outerAngle)
{
    float cosOuter = cos(outerAngle);
    float cosInner = cos(innerAngle);
    float coneScale = 1.0 / max(cosInner - cosOuter, 1e-4);
    float coneOffset = -cosOuter * coneScale;
    return saturate(cosAngle * coneScale + coneOffset);
}

float3 SrgbToLinear(float3 c)
{
    return c <= 0.04045 ? c / 12.92 : pow((c + 0.055) / 1.055, 2.4);
}

float RageFilmicChannel(float x)
{
    const float A = 0.22, B = 0.30, C = 0.10, D = 0.20, E = 0.01, F = 0.30;
    return ((x * (A * x + C * B) + D * E) / (x * (A * x + B) + D * F)) - E / F;
}

float3 RageFilmicTonemap(float3 c)
{
    const float whitePoint = 11.2;
    float ooWhite = 1.0 / RageFilmicChannel(whitePoint);
    return saturate(float3(RageFilmicChannel(c.r), RageFilmicChannel(c.g), RageFilmicChannel(c.b)) * ooWhite);
}

float RageSpecularExponent(float rawExponent)
{
    return max(rawExponent, 1.0);
}

float4 GetDetailValue(float2 uv)
{
    float3 n = DetailTex.Sample(LinearSampler, uv * DetailSettings.zw).xyz;
    n.xy = n.xy * 2.0 - 1.0;
    return float4(n.xyz, n.x);
}

float3 GetDetailBumpAndIntensity(float2 uv, float detailAlpha)
{
    float4 detv = GetDetailValue(uv);
    detv = detv * 0.5 + GetDetailValue(uv * 3.17) * 0.5;
    detv.w = 1.0 + (-detv.w * DetailSettings.x * detailAlpha);
    return float3(detv.xy * DetailSettings.y * detailAlpha, detv.w);
}

float3 GetReflectedDir(float3 camRel, float3 norm)
{
    float3 incident = normalize(camRel);
    float3 refl = normalize(reflect(incident, norm));
    return refl;
}

float RoomSunScale()
{
    return saturate(L2Params.y);
}

float3 RageNaturalAmbient(float downMult)
{
    return LightNaturalAmbUp.rgb * downMult + LightNaturalAmbDown.rgb;
}

float3 RageArtificialAmbient(float downMult)
{
    return LightArtificialAmbUp.rgb * downMult + LightArtificialAmbDown.rgb;
}

float EntityNatScaleRaw() { return AmbientScales.x < 0.0 ? CycleArtIntAmbUp.w   : AmbientScales.x; }

float EntityArtScaleRaw()
{
    float s = AmbientScales.y < 0.0 ? CycleArtIntAmbDown.w : AmbientScales.y;
    return AmbientScales.z > 0.5 ? max(s, InteriorAmbParams.x) : s;
}

float3 EntityArtIntUp()   { return ArtIntAmbUp.w   > 0.5 ? ArtIntAmbUp.rgb   : CycleArtIntAmbUp.rgb; }
float3 EntityArtIntDown() { return ArtIntAmbDown.w > 0.5 ? ArtIntAmbDown.rgb : CycleArtIntAmbDown.rgb; }

bool EntityRoomGraded() { return AmbientScales.z > 0.5 && ArtIntAmbUp.w > 0.5; }

float EyeNatScale()  { return EyeRoomScales.x < 0.0 ? CycleArtIntAmbUp.w   : EyeRoomScales.x; }
float EyeArtScale()  { return max(EyeRoomScales.y < 0.0 ? CycleArtIntAmbDown.w : EyeRoomScales.y, InteriorAmbParams.x); }
float3 EyeArtIntUp()   { return EyeRoomAmbUp.w   > 0.5 ? EyeRoomAmbUp.rgb   : CycleArtIntAmbUp.rgb; }
float3 EyeArtIntDown() { return EyeRoomAmbDown.w > 0.5 ? EyeRoomAmbDown.rgb : CycleArtIntAmbDown.rgb; }

float3 EntityArtificialAmbient(float downMult)
{
    return AmbientScales.z > 0.5
        ? EntityArtIntUp() * downMult + EntityArtIntDown()
        : RageArtificialAmbient(downMult);
}

float ArtificialBakeAdjust_O2(float3 camRel)
{
    if (AmbientScales.z > 0.5 || InteriorAmbParams.z < 0.5) return 1.0;
    return saturate(dot(camRel, camRel) * (1.0 / (500.0 * 500.0)));
}

float3 PrecisionHighlight_S4(float3 c, float3 tint, float2 pixel)
{
    float lum = max(dot(c, float3(0.2126, 0.7152, 0.0722)), 0.0);
    return c * 0.72 + tint * (0.05 + 0.20 * lum);
}

float3 PrecisionCast_U2(float3 c, float3 tint)
{
    const float3 luma = float3(0.2126, 0.7152, 0.0722);
    float lum = max(dot(c, luma), 1e-6);
    float3 hue = tint / max(dot(tint, luma), 1e-4);
    return lerp(c, hue * (lum * 1.15), 0.60);
}

float3 GlobalLighting(float3 diff, float3 norm, float4 vc0, float lf, float3 camRel)
{
    float3 kd = saturate(diff);

    float downMult = max(0.0, (norm.z + AmbientDownWrap) * OoOnePlusAmbientDownWrap);

    float2 bake = InteriorAmbParams.y > 0.5 ? float2(1.0, 1.0) : saturate(float2(vc0.r, vc0.g));
    float natScale = bake.x * EntityNatScaleRaw();
    float artScale = bake.y * ArtificialBakeAdjust_O2(camRel) * EntityArtScaleRaw();

    float3 amb = RageNaturalAmbient(downMult) * natScale;

    amb += LightDirAmbColour.rgb * saturate(dot(GlobalLightDir, norm)) * natScale;

    amb += EntityArtificialAmbient(downMult) * artScale;

    float3 c = kd * (LightDirColour.rgb * lf);
    return c + amb * kd;
}

static const float2 PoissonDisk[16] =
{
    float2(-0.613392,  0.617481), float2( 0.170019, -0.040254),
    float2(-0.299417,  0.791925), float2( 0.645680,  0.493210),
    float2(-0.651784,  0.717887), float2( 0.421003,  0.027070),
    float2(-0.817194, -0.271096), float2(-0.705374, -0.668203),
    float2( 0.977050, -0.108615), float2( 0.063326,  0.142369),
    float2( 0.203528,  0.214331), float2(-0.667531,  0.326090),
    float2(-0.098422, -0.295755), float2(-0.885922,  0.215369),
    float2( 0.566637,  0.605213), float2( 0.039766, -0.396100)
};

float2 ShadowDither(float3 worldPos)
{
    if (ShadowJitterOff > 0.5) return float2(0.0, 1.0);
    float h = frac(sin(dot(worldPos, float3(12.9898, 78.233, 37.719))) * 43758.5453);
    float sa, ca;
    sincos(h * 6.2831853, sa, ca);
    return float2(sa, ca);
}

float2 RotateTap(float2 v, float2 sc)
{
    return float2(v.x * sc.y - v.y * sc.x, v.x * sc.x + v.y * sc.y);
}

float SoftenShadow(float lit)
{
    return smoothstep(0.0, 1.0, saturate(lit));
}

float SunShadowCascadeLit(int ci, float3 worldPos, float3 norm)
{
    float texelWorld = max(SunCascadeTexel[ci], 0.01);
    float3 toSun = normalize(SunPos.xyz - worldPos);
    float ndl = saturate(dot(norm, toSun));

    float3 samplePos = worldPos + norm * (texelWorld * (0.9 + 1.6 * (1.0 - ndl)));

    float4 sp = mul(float4(samplePos, 1.0), SunCascadeMatrix[ci]);
    if (sp.w <= 0.0001) return 1.0;
    float2 uv = sp.xy / sp.w * float2(0.5, -0.5) + 0.5;
    if (uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1) return 1.0;

    float pdist = length(SunPos.xyz - samplePos);

    float biasFloor = (ShadowQuality.y > 0.5) ? 0.15 : 0.015;
    float bias = max(biasFloor, texelWorld * 0.75) + (1.0 - ndl) * texelWorld * 0.75;

    float penumbra = max(ShadowQuality.z, 0.05);
    float radiusTexels = clamp(penumbra / texelWorld, 1.0, 4.0);
    float texel = radiusTexels / 2048.0;
    float2 sc = ShadowDither(worldPos);
    float lit = 0.0;
    [unroll]
    for (int si = 0; si < 16; si++)
    {
        float2 o = RotateTap(PoissonDisk[si], sc) * texel;
        float sdist = SunShadowMap.SampleLevel(LinearSampler, float3(uv + o, ci), 0).r;
        lit += (pdist > sdist + bias) ? 0.0 : 1.0;
    }
    if (ShadowQuality.x > 0.5)
    {

        float2 sc2 = RotateTap(sc, float2(0.7071, 0.7071));
        [unroll]
        for (int sj = 0; sj < 16; sj++)
        {
            float2 o2 = RotateTap(PoissonDisk[sj], sc2) * texel;
            float sdist2 = SunShadowMap.SampleLevel(LinearSampler, float3(uv + o2, ci), 0).r;
            lit += (pdist > sdist2 + bias) ? 0.0 : 1.0;
        }
        lit = SoftenShadow(lit / 32.0);
    }
    else lit = SoftenShadow(lit / 16.0);
    return lit;
}

float SunShadowFactor(float3 worldPos, float3 norm)
{
    if (UseSunShadow == 0) return 1.0;

    int count = max((int)SunCascadeParams.x, 1);
    float viewDist = length(worldPos - CameraPos.xyz);
    int ci = 0;
    [unroll]
    for (int k = 0; k < 3; k++) if (k + 1 < count && viewDist > SunCascadeDepths[k]) ci = k + 1;
    if (viewDist > SunCascadeDepths[count - 1]) return 1.0;

    float lit = SunShadowCascadeLit(ci, worldPos, norm);

    if (ci + 1 < count)
    {
        float far = SunCascadeDepths[ci];
        float band = far * SunCascadeParams.y;
        if (band > 0.0 && viewDist > far - band)
        {
            float t = saturate((viewDist - (far - band)) / band);
            lit = lerp(lit, SunShadowCascadeLit(ci + 1, worldPos, norm), t);
        }
    }

    float sstr = saturate(SunShadowStrength);
    if (ShadowQuality.y > 0.5) return lerp(1.0, lit, sstr);
    if (sstr <= 0.001) return 1.0;
    return pow(saturate(lit), lerp(0.25, 1.0, sstr));
}

float3 SunSpecular(float3 norm, float3 camRel, float specFactor, float shadowLit)
{
    if (shadowLit <= 0.0 || specFactor <= 0.0) return 0;
    float3 refl = normalize(reflect(normalize(camRel), norm));
    float specb = saturate(dot(refl, GlobalLightDir));
    float specp = max(exp(specb * 10.0) - 1.0, 0.0);
    float3 ldr = LightDirColour.rgb / max(LightDirColour.a, 1.0);
    return ldr * (0.00006 * specp * specFactor * shadowLit);
}

float ShadowFactor(Light l, float3 worldPos)
{
    int slot = (int)l.ShadowSlot;
    if (slot < 0) return 1.0;

    float pdist = length(l.Position - worldPos);
    float bias = 0.10 + pdist * 0.02;

    float blur = saturate(l.ShadowBlur);
    float2 sc = ShadowDither(worldPos);
    float lit = 0.0;

    if (slot >= 16)
    {
        float ci = slot - 16;
        float3 sdir = worldPos - l.Position;
        float3 b1 = normalize(cross(sdir, abs(sdir.z) < 0.9 * pdist ? float3(0, 0, 1) : float3(1, 0, 0)));
        float3 b2 = normalize(cross(sdir, b1));

        float r = (0.018 + blur * 0.06) * pdist;
        [unroll]
        for (int i = 0; i < 16; i++)
        {
            float2 o = RotateTap(PoissonDisk[i], sc) * r;
            float3 sd = sdir + b1 * o.x + b2 * o.y;
            float sdist = ShadowCubeArray.SampleLevel(LinearSampler, float4(sd, ci), 0).r;
            lit += (pdist > sdist + bias) ? 0.0 : 1.0;
        }
        return SoftenShadow(lit / 16.0);
    }

    float4 sp = mul(float4(worldPos, 1.0), ShadowMatrices[slot]);
    if (sp.w <= 0.001) return 1.0;
    float2 suv = sp.xy / sp.w * float2(0.5, -0.5) + 0.5;
    if (suv.x < 0 || suv.x > 1 || suv.y < 0 || suv.y > 1) return 1.0;

    float ruv = (2.5 + blur * 9.0) / 1024.0;
    [unroll]
    for (int i2 = 0; i2 < 16; i2++)
    {
        float2 o = RotateTap(PoissonDisk[i2], sc) * ruv;
        float sdist = ShadowSpotArray.SampleLevel(LinearSampler, float3(suv + o, slot), 0).r;
        lit += (pdist > sdist + bias) ? 0.0 : 1.0;
    }
    return SoftenShadow(lit / 16.0);
}

float3 ComputeLight(Light l, int lightIdx, float3 worldPos, float3 camRel, float3 norm, float4 diffuse, float4 specular, float3 refl)
{
    float3 srpos = l.Position - worldPos;
    float ldist = length(srpos);
    if (l.CullingPlaneEnable == 1)
    {
        float d = dot(srpos, l.CullingPlaneNormal) - l.CullingPlaneOffset;
        if (d > 0) return 0;
    }
    if (l.Type == 4)
    {
        float3 ext = l.Direction.xyz * (l.CapsuleExtent.x * 0.5);
        float4 lsn = GetLineSegmentNearestPoint(srpos, ext, -ext);
        ldist = lsn.w;
        srpos.xyz = lsn.xyz;
    }
    if (ldist > l.Falloff) return 0;
    if (ldist <= 0) return 0;
    float3 lcol = l.Colour;
    float3 ldir = srpos / ldist;
    float lamt = 1;

    float invSqrFalloff = 1.0 / max(l.Falloff * l.Falloff, 1e-6);
    lamt *= RageDistanceFalloff(ldist * ldist, invSqrFalloff, l.FalloffExponent);

    if (l.Type == 2)
    {
        float oang = l.ConeOuterAngle;
        float cosAng = -dot(ldir, l.Direction);

        lamt *= RageAngularFalloff(cosAng, l.ConeInnerAngle, oang);
        if (lamt <= 0) return 0;

        int pti = (int)l.ProjTexIndex;
        if (pti >= 0)
        {
            float3 toSurf = -ldir;
            float z = dot(toSurf, l.Direction);
            if (z > 0.001)
            {
                float s = 1.0 / max(tan(oang), 0.01);
                float2 uv = float2(dot(toSurf, l.TangentX), dot(toSurf, l.TangentY)) / z * s * 0.5 + 0.5;
                lcol *= SampleProjTex(pti, saturate(uv));
            }
        }
    }

    if (l.ShadowSlot >= 0)
    {
        lamt *= ShadowFactor(l, worldPos);
        if (lamt <= 0) return 0;
    }

    float cosTheta = saturate(dot(ldir, norm));
    float pclit = cosTheta * lamt;

    float3 spec = 0;
    if (specular.r > 0 && cosTheta > 0 && (l.Flags & LF_NO_SPECULAR) == 0)
    {

        float3 eyeDir = normalize(-camRel);
        float3 H = normalize(eyeDir + ldir);
        float HdotL = saturate(dot(H, eyeDir));
        float fres = specular.b;
        float specFresnel = (1.0 - fres) + fres * pow(1.0 - HdotL, 5.0);
        float HdotN = saturate(dot(H, norm));
        float e = specular.g;
        float blinnPhong = pow(HdotN, e + 1e-8);
        float specNormalisation = (2.0 + e) / 8.0;
        spec = lcol * ((specFresnel * blinnPhong) * pclit * specNormalisation * specular.r);
    }

    if (pclit <= 0) return spec;

    if (RenderMode == 6) return spec;
    return lcol * diffuse.rgb * pclit + spec;
}

static const float3 WaterDeepTint    = float3(0.012, 0.055, 0.075);
static const float3 WaterShallowTint = float3(0.055, 0.230, 0.250);
static const float  WaterF0          = 0.02;
static const float  WaterFaceOpacity = 0.42;
static const float  WaterReflectBoost = 2.4;

float3 WaterRipple(float2 p, float t, float dist)
{
    float3 r = 0;

    const float4 waves[5] =
    {
        float4( 0.860,  0.510,  1.60, 0.050),
        float4(-0.420,  0.907,  3.10, 0.040),
        float4( 0.310, -0.951,  6.30, 0.030),
        float4(-0.970, -0.243, 12.00, 0.020),
        float4( 0.640,  0.768, 24.00, 0.015),
    };
    [unroll]
    for (int i = 0; i < 5; i++)
    {
        float2 dir = waves[i].xy;
        float k = waves[i].z;
        float slope = waves[i].w;

        float fade = saturate(2.0 - dist * k * 0.0022);
        if (fade <= 0.0) continue;

        float speed = sqrt(9.81 * k) * 0.35;
        float ph = dot(p, dir) * k + t * speed;
        r.xy += dir * (cos(ph) * slope * fade);
        r.z  += sin(ph) * (slope / k) * fade;
    }
    return r;
}

float3 WaterSkyTint(float3 dir)
{
    float up = saturate(dir.z * 0.5 + 0.5);
    float3 horizon, zenith;
    if (UseTimecycle)
    {

        horizon = GFogParams0.w > 0.5 ? CalcFogData(dir * 25000.0, 0.0).rgb : FogColour;
        zenith  = LightNaturalAmbUp.rgb * 2.2 + LightDirAmbColour.rgb * 0.6;
    }
    else
    {
        horizon = AmbientColour.rgb * 3.0;
        zenith  = AmbientColour.rgb * 6.0;
    }
    float3 sky = lerp(horizon, zenith, up * up);

    if (UseTimecycle)
    {

        float sd = saturate(dot(dir, GlobalLightDir));
        sky += LightDirColour.rgb * GlobalLightHdr * (pow(sd, 900.0) * 6.0 + pow(sd, 40.0) * 0.35);
    }
    return sky;
}

float3 SampleSkyEnv(float3 dir, float gloss)
{

    float3 d = normalize(lerp(dir, float3(0, 0, 0.6), saturate(gloss) * 0.5));
    return WaterSkyTint(d);
}

static const float InteriorReflectAlbedo = 0.5;

float3 EnvReflection(float3 R, float gloss)
{
    float downR = max(0.0, (R.z + AmbientDownWrap) * OoOnePlusAmbientDownWrap);
    if (!EntityRoomGraded())
    {

        float3 sky = SampleSkyEnv(R, gloss);
        if (EyeRoomParams.x > 0.001)
        {
            float3 eyeRoom = RageNaturalAmbient(downR) * EyeNatScale()
                           + (EyeArtIntUp() * downR + EyeArtIntDown()) * EyeArtScale() * EyeRoomParams.y;
            return lerp(sky, eyeRoom * InteriorReflectAlbedo, saturate(EyeRoomParams.x));
        }
        return sky;
    }
    float3 room = RageNaturalAmbient(downR) * EntityNatScaleRaw()
                + (EntityArtIntUp() * downR + EntityArtIntDown()) * EntityArtScaleRaw() * AmbientScales.w;
    return room * InteriorReflectAlbedo;
}

float EnvReflectionMask(float4 vc0, float exteriorMask)
{
    if (!EntityRoomGraded()) return exteriorMask;
    return max(saturate(vc0.r) * EntityNatScaleRaw(), saturate(vc0.g) * EntityArtScaleRaw());
}

static const float OceanRippleScale     = 0.02;
static const float OceanRippleBumpiness = 4.5;
static const float OceanBumpIntensity   = 0.35;
static const float WaterSpecularFalloff = 220.0;
static const float WaterSpecularIntensity = 4.2;

float2 ReflectionUV(float2 screenPos, int mslot)
{
    float2 uv = screenPos * ProjParams.zw;
    float2 ndc = float2(2.0 * uv.x - 1.0, 1.0 - 2.0 * uv.y);
    float4 rect = (mslot == 1) ? ReflectRect0 : ReflectRect1;
    float2 sc = (mslot == 1) ? ReflectScale.xy : ReflectScale.zw;
    if (rect.z <= 0.0 || rect.w <= 0.0) { rect = float4(0, 0, 1, 1); }
    if (sc.x <= 0.0 || sc.y <= 0.0) sc = float2(1, 1);
    float2 r = float2(0.5 * (1.0 - (ndc.x - rect.x) / rect.z), 0.5 * (1.0 - (ndc.y - rect.y) / rect.w));
    return saturate(r) * sc;
}

float SceneViewDistance(float2 screenPos)
{
    float z;
    if (WaterParams.z > 1.5)      z = SceneDepthMS.Load(int2(screenPos), 0);
    else                          z = SceneDepthTex.Load(int3(int2(screenPos), 0));

    if (z <= 0.0) return 1e6;
    return ProjParams.y / (z + ProjParams.x);
}

float3 GameOceanBump(float2 worldXY, float t)
{
    float2 tex = worldXY * OceanRippleScale;
    float2 drift1 = float2(t * 0.011, t * 0.007);
    float2 drift2 = float2(-t * 0.006, t * 0.009);
    float4 bumpHigh;
    bumpHigh.xy = WaterBump2Tex.Sample(LinearSampler, tex + drift1).ba;
    bumpHigh.zw = WaterBumpTex.Sample(LinearSampler, tex * 3.7 + drift2).rg;
    bumpHigh = 2.0 * bumpHigh - 1.0;
    float2 bump = bumpHigh.xy + bumpHigh.zw;

    float2 lowBump = -(WaterBumpTex.Sample(LinearSampler, worldXY.yx / 448.0 + t / 10.0 * 0.1).ga * 2.0 - 1.0)
                     -(WaterBumpTex.Sample(LinearSampler, worldXY.xy / 512.0 - t / 10.0 * 0.1).ag * 2.0 - 1.0);
    bump += lowBump * OceanBumpIntensity;
    return float3(bump, OceanRippleBumpiness);
}

float Bayer8_U20(float2 pos)
{
    uint2 p = uint2(pos) & 7;
    uint x = p.x ^ p.y;
    uint v = ((x & 1) << 5) | ((p.y & 1) << 4) | ((x & 2) << 2) | ((p.y & 2) << 1) | ((x & 4) >> 1) | ((p.y & 4) >> 2);
    return (v + 0.5) / 64.0;
}

float4 PSMain(PS_Input input, bool isFrontFace : SV_IsFrontFace) : SV_TARGET
{

    if (FadeAlpha < 0.999)
    {
        const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        uint2 fpx = uint2(input.Pos.xy) & 3;
        clip(FadeAlpha - (bayer[fpx.y * 4 + fpx.x] + 0.5) / 16.0);
    }

    float furShade = 1.0;

    if (RenderMode == 3)
    {
        return isFrontFace ? float4(1, 0.2, 0.2, 1) : float4(0.2, 0.2, 1, 1);
    }
    if (RenderMode == 4)
    {
        float z = input.Pos.z;
        return float4(frac(z * 4000), frac(z * 400), z, 1);
    }
    if (RenderMode == 8)
    {

        float3 alb = HasDiffuseTex ? DiffuseTex.Sample(LinearSampler, input.UV0).rgb : MatDiffuse.rgb;
        float3 wc = lerp(float3(0.62, 0.66, 0.72), saturate(alb * 1.35), 0.55);

        wc *= lerp(1.0, 0.45, saturate((input.Pos.w - 60.0) / 540.0));
        if (IsSelectedMesh == 1)      wc = lerp(wc, float3(1.00, 0.10, 0.60), 0.75);
        else if (IsSelectedMesh == 2) wc = lerp(wc, float3(0.30, 0.95, 1.00), 0.55);
        else if (IsSelectedMesh == 3) wc = lerp(wc, float3(1.00, 0.60, 0.10), 0.35);
        else if (IsSelectedMesh == 4) wc = lerp(wc, float3(1.00, 0.78, 0.30), 0.60);

        else if (IsSelectedMesh == 5) wc = lerp(wc, float3(0.25, 0.95, 1.00), 0.55);
        else if (IsSelectedMesh == 6) wc = lerp(wc, float3(1.00, 0.16, 0.72), 0.70);
        return float4(wc, 1.0);
    }

    if (ReflectionParams.x > 0.5)
    {
        clip(dot(ClipPlane.xyz, input.WorldPos) + ClipPlane.w);
    }

    if (ReflectionParams.y > 0.5 && ReflectionParams.x < 0.5)
    {
        float2 druv = ReflectionUV(input.Pos.xy, 1);
        float4 drf = ReflectionTex0.SampleLevel(LinearSampler, druv, 0);
        return float4(lerp(float3(1, 0, 1), drf.rgb, drf.a), 1.0);
    }

    float2 uv = AnimateUVs(input.UV0);
    bool grassFur = FurParams.w > 0.0 && FurParams2.w >= 0.0;
    if (grassFur) uv = input.UV0 * FurParams2.x;

    float4 diffuse = HasDiffuseTex ? DiffuseTex.Sample(LinearSampler, uv) : MatDiffuse;

    if (HasDiffuseTex == 1) diffuse.rgb = SrgbToLinear(diffuse.rgb);

    if (FurParams.w > 0.0)
    {
        const float fbayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        uint2 fpx = uint2(input.Pos.xy) & 3;
        float dither = (fbayer[fpx.y * 4 + fpx.x] + 0.5) / 16.0;
        if (grassFur)
        {
            float cpv = saturate(input.Colour.a);
            if (FurParams4.y > 0.5) cpv *= FurMaskTex.Sample(LinearSampler, input.UV1).x;
            float3 toCam = CameraPos.xyz - input.WorldPos;
            float dist2 = dot(toCam, toCam);
            float fadeAlpha = saturate((FurParams3.y - dist2) / max(FurParams3.y - FurParams3.x, 0.001));
            if (FurParams2.z > 0.5) clip(fadeAlpha - Bayer8_U20(input.Pos.xy));
            float2 uvH = input.UV0 * FurParams2.y;
            int layer = (int)(FurParams2.w + 0.5);
            int slot = layer >> 1;
            float4 comb = slot == 0 ? FurCombo0.SampleBias(LinearSampler, uvH, 1.15)
                        : slot == 1 ? FurCombo1.SampleBias(LinearSampler, uvH, 1.15)
                        : slot == 2 ? FurCombo2.SampleBias(LinearSampler, uvH, 1.15)
                                    : FurCombo3.SampleBias(LinearSampler, uvH, 1.15);
            float height = (layer & 1) == 0 ? comb.g : comb.a;
            float clipLevel = lerp(0.01, FurParams.y, saturate(cpv * 2.0));
            clip(height * cpv - clipLevel);
            float shade = lerp(FurParams3.z, FurParams.z, fadeAlpha);
            furShade = lerp(FurParams3.z, shade, cpv);
            if (FurParams4.z > 0.5)
            {
                float3 hf = FurHfTex.Sample(LinearSampler, input.Colour1.xy * FurParams4.x).rgb;
                if (FurParams4.z < 1.5) hf = SrgbToLinear(hf);
                diffuse.rgb *= hf;
            }
            diffuse.a = 1.0;
        }
        else if (FurParams2.w < -50.0)
        {
            float2 furUV = input.UV0 * FurParams2.xy;
            float4 n = FurCombo0.Sample(LinearSampler, furUV);
            float hRgb = max(n.r, dot(n.rgb, float3(0.299, 0.587, 0.114)));
            float height = (n.a >= 0.996 ? hRgb : n.a) * diffuse.a;
            float tip = saturate(input.Tangent.w);
            float3 nBw = mul(float4(input.Colour.rgb * 2.0 - 1.0, 0.0), World).xyz;
            nBw = nBw / max(length(nBw), 0.0001);
            float3 edgeN = input.Tangent.xyz + nBw;
            float lenN = length(edgeN);
            edgeN = lenN > 0.0001 ? edgeN / lenN : input.Tangent.xyz;
            float3 toEye = normalize(CameraPos.xyz - input.WorldPos);
            float sil = saturate((0.38 - abs(dot(edgeN, toEye))) / 0.30);
            float alpha = height * sil * (1.0 - tip * tip * 0.8) * saturate(FurParams2.z);
            clip(alpha - max(dither, 0.12));
            furShade = lerp(saturate(FurParams.z), 1.0, tip);
        }
        else
        {
            float2 furUV = input.UV0 * FurParams2.xy;
            float4 n = FurCombo0.Sample(LinearSampler, furUV);
            float hRgb = max(n.r, dot(n.rgb, float3(0.299, 0.587, 0.114)));
            float height = (n.a >= 0.996 ? hRgb : n.a) * diffuse.a;
            float thr = FurParams.y + (1.0 - FurParams2.z) * 0.5;
            clip(height - thr);
            if (FurParams.y >= 0.0)
            {
                float shellT = FurParams.x <= 0.0 ? FurParams.x + 1.0 : FurParams.x;
                float alpha = saturate((height - thr) * 6.0) * (1.0 - 0.6 * shellT * shellT);
                clip(alpha - dither * 0.95);
            }
            if (FurParams.z > 0.0 && FurParams.z < 1.0) furShade = FurParams.z;
        }
    }

    float4 terrainBump = float4(0.5, 0.5, 1.0, 1.0);
    if (IsTerrain)
    {
        uint tbm = TerrainBlendMode & 3;

        float2 uvL = (TerrainBlendMode & 4) ? input.UV1 : uv;
        float3 blend = input.Colour1.rgb;
        if (tbm == 1)
        {
            blend = TerrainMaskTex.Sample(LinearSampler, input.UV1).rgb;
        }
        else if (tbm == 2)
        {
            float3 lookupP = TerrainMaskTex.Sample(LinearSampler, input.UV1).rgb;
            blend = lerp(lookupP, input.Colour1.rgb, input.Colour.a);
        }

        float3 rgb = saturate(blend);
        if (RenderMode == 14) return float4(rgb, 1.0);
        if (RenderMode == 15) return float4(input.Colour1.rgb, 1.0);
        if (RenderMode == 16) return float4(input.Colour.aaa, 1.0);
        if (RenderMode == 17) return float4(TerrainMaskTex.Sample(LinearSampler, input.UV1).rgb, 1.0);
        float3 m = 1.0 - rgb;
        float4 w = float4(m.g * m.b,
                          m.g * rgb.b,
                          rgb.g * m.b,
                          rgb.g * rgb.b);

        float3 l0 = LayerTex0.Sample(LinearSampler, uv).rgb;
        float3 l1 = LayerTex1.Sample(LinearSampler, uvL).rgb;
        float3 l2 = LayerTex2.Sample(LinearSampler, uvL).rgb;
        float3 l3 = LayerTex3.Sample(LinearSampler, uvL).rgb;
        if (IsTerrain == 1) { l0 = SrgbToLinear(l0); l1 = SrgbToLinear(l1); l2 = SrgbToLinear(l2); l3 = SrgbToLinear(l3); }
        diffuse = float4(l0 * w.x + l1 * w.y + l2 * w.z + l3 * w.w, 1.0);

        if (HasLayerBump)
        {

            float2 n = (LayerBump0.Sample(LinearSampler, uv).xy * 2.0 - 1.0) * w.x
                     + (LayerBump1.Sample(LinearSampler, uvL).xy * 2.0 - 1.0) * w.y
                     + (LayerBump2.Sample(LinearSampler, uvL).xy * 2.0 - 1.0) * w.z
                     + (LayerBump3.Sample(LinearSampler, uvL).xy * 2.0 - 1.0) * w.w;

            terrainBump = float4(n * 0.5 + 0.5, 1.0, 1.0);
        }
    }

    if ((HasTintPalette & 3) == 3)
    {

        float tx = (round(diffuse.a * 255.009995) - 32.0) * 0.007813;
        float3 pal = TintPaletteTex.Sample(PointSampler, float2(tx, TintPaletteV)).rgb;
        if ((HasTintPalette & 4) == 0) pal = SrgbToLinear(pal);
        diffuse.rgb *= pal;
        diffuse.a = 1.0;
    }
    else if ((HasTintPalette & 3) != 0) diffuse.rgb *= input.Tint;

    bool isWater = (AlphaMode == 5);
    float3 waterWave = 0;

    float alpha = diffuse.a;
    if (isWater)
    {

        diffuse = float4(WaterShallowTint, 1.0);
        alpha = 1.0;
    }
    else if (AlphaMode == 1 && HasDiffuseTex == 0 && !grassFur)
    {

        clip(-1);
    }
    else if (AlphaMode == 0 || AlphaMode == 1)
    {
        clip(alpha - 0.33);
        alpha = 1.0;
    }
    else if (AlphaMode == 2 && DecalKind != 0 && HasDiffuseTex == 0)
    {

        clip(-1);
    }
    else if (DecalKind == 2)
    {

        float4 mask = DecalMask * diffuse;
        alpha = saturate(mask.r + mask.g + mask.b + mask.a);
        diffuse.rgb = 0.0;
        clip(alpha - 0.001);
    }
    else if (DecalKind == 5)
    {

        float ao = HasDiffuseTex ? (diffuse.r + diffuse.g + diffuse.b) / 3.0 : 1.0;

        if (HasDiffuseTex && diffuse.r + diffuse.g + diffuse.b < 0.001 && diffuse.a > 0.001) ao = diffuse.a;
        float amount = saturate(input.Colour.a);
        float mul = lerp(1.0, ao, amount);

        if (RenderMode >= 9 && RenderMode <= 13) return float4(input.Colour.rgb, 1.0);
        return float4(mul, mul, mul, 1.0);
    }
    else if (DecalKind == 6 || DecalKind == 7)
    {

        alpha = diffuse.a;
        if (DecalKind == 6) alpha *= diffuse.r * input.Colour.r;
        else alpha *= input.Colour.g;
        clip(alpha - 0.004);
    }
    else if (DecalKind == 8)
    {

        alpha = HasDiffuseTex ? diffuse.a : 1.0;
        alpha *= input.Colour.a;
    }
    else if (DecalKind == 3 || DecalKind == 4)
    {

        clip(-1);
    }
    else
    {
        clip(alpha - 0.001);
        alpha *= input.Colour.a;
    }

    float4 sv = HasSpecTex ? SpecTex.Sample(LinearSampler, uv) : float4(0.1, 0.1, 0.1, 0.1);

    float3 det = float3(0.0, 0.0, 1.0);
    if (HasDetailTex)
    {

        float detailAlpha = HasSpecTex ? sv.a : 1.0;
        det = GetDetailBumpAndIntensity(uv, detailAlpha);
        diffuse.rgb *= det.z;
    }

    float3 norm = normalize(input.Normal);

    if (!isFrontFace && !isWater) norm = -norm;
    float3 tangent = normalize(input.Tangent.xyz);
    float tw = input.Tangent.w != 0 ? sign(input.Tangent.w) : 1.0;
    float3 bitangent = normalize(cross(tangent, norm)) * tw;

    bool hasBump = HasBumpTex || (IsTerrain && HasLayerBump);
    if (isWater)
    {

        if (WaterParams.x > 0.5)
        {

            float3 fb = GameOceanBump(input.WorldPos.xy, SceneTime);
            waterWave = float3(fb.xy * fb.z * 0.02, 0.0);
        }
        else
            waterWave = WaterRipple(input.WorldPos.xy, SceneTime, length(CameraPos.xyz - input.WorldPos));
        float3 g = float3(waterWave.xy, 0.0);

        g -= norm * dot(g, norm);
        norm = normalize(norm - g);
        bitangent = normalize(cross(tangent, norm)) * tw;

        diffuse.rgb = lerp(diffuse.rgb, WaterDeepTint, saturate(0.5 - waterWave.z * 6.0));
    }
    else if (hasBump || HasDetailTex)
    {

        float3 flatNorm = norm;

        float2 packed = hasBump
            ? ((IsTerrain && HasLayerBump) ? terrainBump : BumpTex.Sample(LinearSampler, grassFur ? input.UV0 * FurParams3.w : uv)).xy
            : float2(0.5, 0.5);
        packed += det.xy;

        float2 nxy = packed * 2.0 - 1.0;
        float nz = sqrt(abs(1.0 - dot(nxy, nxy)));
        float bmp = max(Bumpiness, 0.001);
        norm = normalize(tangent * (nxy.x * bmp) + bitangent * (nxy.y * bmp) + norm * nz);

        if (BumpTiltLimit > 0.0)
        {
            float ct = dot(norm, flatNorm);
            if (ct < BumpTiltLimit)
            {

                float3 t = norm - flatNorm * ct;
                float tl = length(t);
                if (tl > 1e-5)
                {
                    float s = sqrt(saturate(1.0 - BumpTiltLimit * BumpTiltLimit));
                    norm = normalize(flatNorm * BumpTiltLimit + (t / tl) * s);
                }
                else norm = flatNorm;
            }
        }

        bitangent = normalize(cross(tangent, norm)) * tw;
    }

    float4 specular = float4(
        max(dot(sv.xyz, SpecMapIntMask) * SpecIntensity * det.z, 0.0),
        RageSpecularExponent(sv.y * SpecFalloffMult),
        saturate(SpecFresnel * SpecFresnelMult),
        0);

    if (isWater) specular = float4(2.2, 420.0, 0.97, 0);

    if (RenderMode == 1)
    {
        return float4(diffuse.rgb, alpha);
    }
    if (RenderMode == 2)
    {
        return float4(norm * 0.5 + 0.5, 1);
    }

    if (RenderMode >= 9 && RenderMode <= 13)
    {
        float4 vc = input.Colour;

        if (RenderMode == 9)  return float4(vc.rgb, alpha);
        if (RenderMode == 10) return float4(vc.r, vc.r, vc.r, alpha);
        if (RenderMode == 11) return float4(vc.g, vc.g, vc.g, alpha);
        if (RenderMode == 12) return float4(vc.b, vc.b, vc.b, alpha);
        return float4(vc.a, vc.a, vc.a, 1.0);
    }

    if (RenderMode == 7)
    {
        return float4(alpha, alpha, alpha, 1);
    }

    if (RenderMode == 5)
    {
        diffuse.rgb = 0.75;
    }

    bool specOnly = (RenderMode == 6);
    bool lightOnly = L2Params.z > 0.5;

    float3 camRel = input.WorldPos - CameraPos.xyz;

    float3 c;
    if (specOnly)
    {
        c = 0;
        if (UseTimecycle && !isWater)
        {
            float sunShad = SunShadowFactor(input.WorldPos, norm) * RoomSunScale();
            c += SunSpecular(norm, camRel, sv.x * sv.x * SpecIntensity, sunShad);
        }
    }
    else if (UseTimecycle)
    {

        float sunShad = SunShadowFactor(input.WorldPos, norm) * RoomSunScale();
        float lf = saturate(dot(norm, GlobalLightDir)) * sunShad;

        float3 toEyeN = normalize(-camRel);
        float EdotN = saturate(dot(toEyeN, norm));
        float f5 = pow(1.0 - EdotN, 5.0);
        float reflectFresnel = (1.0 - specular.z) + specular.z * f5;
        float Kr = (isWater || AlphaMode == 4) ? 0.0 : saturate(specular.x) * reflectFresnel;
        float Kd = 1.0 - Kr;
        c = GlobalLighting(diffuse.rgb * Kd, norm, input.Colour, lf, camRel);

        if (!isWater) c += SunSpecular(norm, camRel, sv.x * sv.x * SpecIntensity, sunShad);
        if (Kr > 0.0005 && L2Params.x < 0.5)
        {

            float3 R = GetReflectedDir(camRel, norm);
            float gloss = 1.0 - saturate(specular.y / 1500.0);
            float3 env = EnvReflection(R, gloss);
            env *= EnvReflectionMask(input.Colour, max(input.Colour.r, 0.05));
            env *= lerp(1.0 / 3.14159, 1.0, saturate(specular.y / 563.0));
            c += env * Kr;
        }
    }
    else
    {
        c = diffuse.rgb * AmbientColour.rgb;
    }

    float3 refl = GetReflectedDir(camRel, norm);

    bool isAlphaGeom = (AlphaMode >= 2);

    float3 lightSum = 0;
    [loop]
    for (uint i = 0; i < MeshLightCount; i++)
    {
        uint li = MeshLightIndices[i >> 2][i & 3];
        Light gl = Lights[li];
        if (isAlphaGeom && (gl.Flags & LF_DONT_LIGHT_ALPHA) != 0) continue;
        lightSum += ComputeLight(gl, (int)li, input.WorldPos, camRel, norm, diffuse, specular, refl) * LightsMultiplier;
    }
    c += lightSum;
    if (lightOnly) c = lightSum;

    if (!specOnly && !lightOnly) c += diffuse.rgb * EmissiveMult;

    if (AlphaMode == 4 && !specOnly && RenderMode != 7)
    {
        float3 toEye = normalize(-camRel);
        float EdotN = saturate(dot(toEye, norm));
        float F0 = saturate(SpecFresnel);
        float reflectFresnel = (1.0 - F0) + F0 * pow(1.0 - EdotN, 5.0);
        float texA = alpha;
        alpha = max(texA, reflectFresnel);
        float ks = saturate(SpecIntensity);
        float Kd = 1.0 - ks * lerp(1.0, reflectFresnel, alpha);
        float Kr = 1.0 - Kd;
        c *= Kd;

        float3 R = reflect(-toEye, norm);
        float glossy = saturate(specular.g / 563.0);
        float3 env = EnvReflection(R, 1.0 - glossy);
        env *= EnvReflectionMask(input.Colour, max(input.Colour.r, saturate(input.Colour.g)));
        env = lerp(env / 3.14159, env, glossy);
        if (!lightOnly) c += env * Kr;

        if (UseTimecycle && !lightOnly)
        {
            float3 H = normalize(GlobalLightDir + toEye);
            float HdotL = saturate(dot(H, GlobalLightDir));
            float specFres = (1.0 - F0) + F0 * pow(1.0 - HdotL, 5.0);
            float e = max(specular.g, 1.0);
            float ndl = saturate(dot(norm, GlobalLightDir));
            float sunShadowG = SunShadowFactor(input.WorldPos, norm) * RoomSunScale();
            c += LightDirColour.rgb * (pow(saturate(dot(H, norm)), e) * specFres * ((2.0 + e) / 8.0)) * ks * ndl * sunShadowG;
        }
    }

    if (IsMirror > 0.5 && !specOnly && RenderMode != 7 && !lightOnly)
    {
        float3 toEye = normalize(-camRel);
        float EdotN = saturate(dot(toEye, norm));
        float fres = (1.0 - SpecFresnel) + SpecFresnel * pow(1.0 - EdotN, 5.0);
        float3 R = reflect(-toEye, norm);
        float3 env = EnvReflection(R, 0.0);

        if (!EntityRoomGraded()) env *= lerp(0.35, 1.0, saturate(R.z));
        float Kr = max(saturate(SpecIntensity), 0.6) * fres;
        int mslot = (int)round(IsMirror) - 1;
        if (mslot >= 1)
        {
            float2 ruv = ReflectionUV(input.Pos.xy, mslot);
            float4 rf = (mslot == 1) ? ReflectionTex0.SampleLevel(LinearSampler, ruv, 0)
                                     : ReflectionTex1.SampleLevel(LinearSampler, ruv, 0);

            env = lerp(env, rf.rgb, saturate(rf.a * 4.0));

            Kr = (DecalKind == 8) ? 1.0 : lerp(0.85, 1.0, fres);
        }

        if (DecalKind == 8) Kr = 1.0;
        c = lerp(c, env, Kr);
        if (mslot < 1 && DecalKind != 8) c += env * 0.15 * diffuse.rgb;

        if (DecalKind == 8) alpha = (mslot >= 1) ? alpha * lerp(0.55, 1.0, fres) : max(alpha, fres * 0.9);
    }

    if (AlphaMode == 3 && !specOnly)
    {
        c = diffuse.rgb * max(EmissiveMult, 1.0);
    }

    if (isWater && !specOnly && RenderMode != 7)
    {
        float3 toEye = normalize(-camRel);
        float3 V = -toEye;

        bool fromBelow = dot(norm, toEye) < 0.0;
        if (fromBelow) norm = -norm;
        float ndv = saturate(dot(norm, toEye));
        float sunShadow = UseTimecycle ? SunShadowFactor(input.WorldPos, float3(0, 0, 1)) : 1.0;

        float4 wcol;
        if (WaterParams.y > 0.5)
        {
            float2 fogtc = saturate((input.WorldPos.xy - WaterFogParams.xy) * WaterFogParams.zw);
            fogtc.y = 1.0 - fogtc.y;

            wcol = WaterFogTex.Sample(LinearSampler, fogtc);
        }
        else wcol = float4(sqrt(WaterShallowTint), 1.0);
        wcol.a = input.Colour.a * input.Colour.a;

        if (input.Colour.a > 0.99) wcol.a = 0.0104;

        if (wcol.a < 0.0001) wcol.a = 0.0104;

        float3 waterSun     = UseTimecycle ? LightDirColour.rgb : AmbientColour.rgb * 6.0;
        float3 fogLight = UseTimecycle
            ? GlobalLighting(1.0, float3(0, 0, 1), float4(1, 1, 1, 1), saturate(GlobalLightDir.z) * sunShadow, camRel)
            : AmbientColour.rgb * 6.0;
        float3 waterColor = wcol.rgb * wcol.rgb * fogLight * abs(WaterParams.w);

        float depth = 6.0;
        float sceneD = 1e6;
        if (WaterParams.z > 0.5)
        {
            sceneD = SceneViewDistance(input.Pos.xy);
            depth = max(sceneD - input.Pos.w, 0.0);
        }

        bool haveBottom = (WaterParams.z > 0.5) && (sceneD < 1e5);

        // The body-depth limit keeps an inland quad from flooding a valley that drops away
        // below it, so it may only judge water whose bottom is actually IN the depth buffer.
        // With no bottom, depth is the far plane: every such pixel failed the test and was
        // discarded - which deleted inland water wherever its bed was not drawn behind it
        // (distance, unloaded terrain, water meeting the horizon). Unknown is not too deep.
        float bodyFade = 1.0;
        if (Bumpiness > 0.5 && haveBottom)
        {
            bodyFade = saturate((Bumpiness - depth) / max(Bumpiness * 0.2, 1.0));
            if (bodyFade <= 0.002) discard;
        }

        if (!haveBottom) depth = 40.0;

        if (WaterParams.x > 0.5)
        {
            const float phspd = 4.0, phspdi = 0.25, phspdh = 2.0;
            float rt = fmod(SceneTime, 1000.0);
            float ta = rt * 2.0, tb = ta + phspdh;
            float t1 = frac(ta * phspdi) * phspd;
            float t2 = frac(tb * phspdi) * phspd;
            float s1 = ((t1 < phspdh) ? t1 : phspd - t1) * phspdi;
            float s2 = ((t2 < phspdh) ? t2 : phspd - t2) * phspdi;
            float2 ruv1 = input.WorldPos.xy * 0.02 * 2.3;
            float2 ruv2 = (input.WorldPos.xy * 0.02 + 0.5) * 2.3;
            float2 rn1 = WaterBumpTex.Sample(LinearSampler, ruv1).xy * 2.0 - 1.0;
            float2 rn2 = WaterBumpTex.Sample(LinearSampler, ruv2).xy * 2.0 - 1.0;
            float2 rnm = rn1 * s1 + rn2 * s2;

            norm = normalize(float3(rnm * 4.5 * input.Colour.r + norm.xy, norm.z));
        }

        float2 depthBlend = saturate(exp(float2(-20.0, -60.0 * abs(V.z)) * wcol.a * depth * 2.71828183));

        float bodyAlpha;
        float2 screenUV = input.Pos.xy * ProjParams.zw;
        bool haveRefr = false;
        if (HasSceneColour > 0.5 && haveBottom)
        {
            float2 ofs = -normalize(norm).xy * saturate(depth * 0.2) * 0.03;
            ofs.y = -ofs.y;
            float2 ruv = saturate(screenUV + ofs);
            float dR = SceneViewDistance(ruv / ProjParams.zw);
            if (dR < input.Pos.w || dR > 1e5) ruv = screenUV;
            float3 refraction = SceneColourTex.SampleLevel(LinearSampler, ruv, 0).rgb;

            if (UseTimecycle && WaterParams.x > 0.5 && !fromBelow)
            {
                float2 wp = input.WorldPos.xy;
                float c1 = WaterBumpTex.Sample(LinearSampler, wp / 5.5 + SceneTime * float2(0.021, 0.013)).g;
                float c2 = WaterBump2Tex.Sample(LinearSampler, wp.yx / 4.7 - SceneTime * float2(0.017, 0.011)).b;
                float caustic = saturate((c1 + c2) * 0.5 - 0.42) * 6.0;
                caustic *= caustic;
                float sunUp = saturate(GlobalLightDir.z * 3.0);
                refraction *= 1.0 + caustic * saturate(depth) * 1.2 * sunShadow * sunUp * depthBlend.x;
            }

            float3 litRefraction = lerp(2.0 * wcol.rgb * refraction, refraction, depthBlend.y);
            waterColor = lerp(waterColor, litRefraction, depthBlend.x);
            bodyAlpha = 1.0;
            haveRefr = true;
        }
        else
        {

            bodyAlpha = 1.0 - depthBlend.x;
        }

        if (UseTimecycle)
        {
            float3 rr = refract(V, normalize(lerp(norm, float3(0, 0, 1), 0.5)), 1.0 / 1.5);
            float pierce = pow(saturate(dot(rr, -GlobalLightDir)), 2.0) * saturate(depth / 10.0);
            waterColor += wcol.rgb * waterSun * pierce * pierce * 0.5 * sunShadow;
        }

        if (haveBottom)
        {
            float foamMask = saturate(1.0 - depth * 0.7);
            float foam = foamMask * (length(norm.xy) * 0.27 + 0.44);
            foam = saturate(foam - 0.45) * 2.0;
            if (foam > 0.001 && WaterParams.x > 0.5)
            {
                float sheet = WaterBumpTex.Sample(LinearSampler, input.WorldPos.xy * 0.05 + SceneTime * 0.02).r;
                foam *= sheet;
                float3 foamLight = (waterSun * saturate(dot(norm, GlobalLightDir) * 0.7 + 0.3) * sunShadow + fogLight) * 0.5;
                waterColor += foamLight * foam * 0.6;
            }
        }

        float3 reflectionNormal = normalize(lerp(norm, float3(0, 0, 1), 5.0 / 6.0));
        float fresnel = lerp(saturate(dot(-V, norm)), 1.0, 0.3);
        float reflectionFactor = saturate(pow(1.0 - fresnel, 3.0) * 0.9 + 0.02);
        float3 reflectionColor = WaterSkyTint(reflect(V, reflectionNormal));
        c = lerp(waterColor, reflectionColor, reflectionFactor);

        if (UseTimecycle && !fromBelow)
        {
            float3 specN = norm;
            if (WaterParams.x > 0.5)
            {
                float distM = length(camRel);
                float gscale = 0.02 * 3.7 / max(1.0, distM * 0.05);
                float2 g1 = WaterBumpTex.Sample(LinearSampler, input.WorldPos.xy * gscale + SceneTime * float2(0.031, -0.024)).rg * 2.0 - 1.0;
                float2 g2 = WaterBump2Tex.Sample(LinearSampler, input.WorldPos.yx * gscale * 1.31 - SceneTime * float2(0.019, 0.027)).ba * 2.0 - 1.0;
                float glit = saturate((distM - 15.0) / 60.0);
                specN = normalize(float3(specN.xy + (g1 + g2) * 0.11 * glit, specN.z));
            }
            float3 H = normalize(GlobalLightDir + toEye);

            float3 sunHue = waterSun / max(max(waterSun.r, max(waterSun.g, waterSun.b)), 1e-4);
            float sp = pow(saturate(dot(H, specN)), 220.0) * 4.2 * sunShadow;
            c += sunHue * fogLight * sp;
        }

        c = lerp(waterColor, c, saturate(depth));

        if (fromBelow)
        {
            float3 refrDir = refract(V, norm, 1.33);
            float refractionBlend = saturate(refrDir.z);
            float3 murk = wcol.rgb * wcol.rgb * fogLight * abs(WaterParams.w);
            if (haveRefr)
            {
                float2 ofs2 = norm.xy * 0.04;
                ofs2.y = -ofs2.y;
                float3 above = SceneColourTex.SampleLevel(LinearSampler, saturate(screenUV + ofs2), 0).rgb;
                above *= 1.0 + 0.6 * refractionBlend;
                c = lerp(murk, above, refractionBlend);
                alpha = 1.0;
            }
            else
            {
                c = murk;
                alpha = 1.0 - refractionBlend * 0.85;
            }
        }
        else

        alpha = haveRefr ? 1.0 : saturate(max(bodyAlpha, reflectionFactor));
        alpha *= bodyFade;

        if (WaterParams.w < 0.0)
        {

            return float4(saturate(depth / 20.0), depthBlend.x, haveRefr ? 1.0 : 0.0, 1.0);
        }
    }

    {
        float4 fd = CalcFogData(camRel, 1.0);
        c = lightOnly ? c * (1.0 - fd.a) : lerp(c, fd.rgb, fd.a);
    }

    if (!lightOnly)
    {
    if (IsSelectedMesh == 1)      c = lerp(c, float3(1.00, 0.10, 0.60), 0.60);
    else if (IsSelectedMesh == 2) c = lerp(c, float3(0.30, 0.95, 1.00), 0.35);
    else if (IsSelectedMesh == 3) c = lerp(c, float3(1.00, 0.60, 0.10), 0.12);
    else if (IsSelectedMesh == 4) c = lerp(c, float3(1.00, 0.78, 0.30), 0.32);

    else if (IsSelectedMesh == 5) c = PrecisionHighlight_S4(c, float3(0.25, 0.95, 1.00), input.Pos.xy);
    else if (IsSelectedMesh == 6) c = PrecisionHighlight_S4(c, float3(1.00, 0.16, 0.72), input.Pos.xy);

    else if (IsSelectedMesh == 7) c = PrecisionCast_U2(c, float3(0.25, 0.95, 1.00));
    else if (IsSelectedMesh == 8) c = PrecisionCast_U2(c, float3(1.00, 0.16, 0.72));
    }

    c *= furShade;

    return float4(c, alpha);
}
