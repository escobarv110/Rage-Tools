cbuffer PostFxVars : register(b0)
{

    float FilmicA, FilmicB, FilmicC, FilmicD;
    float FilmicE, FilmicF, FilmicW, Exposure;

    float4 ColorCorrectHighLum;
    float4 ColorShiftLowLum;
    float Desaturate;
    float Gamma;
    uint EnableColorCorrect;

    uint Cinematic;
    float BloomStrength;
    float SsrIntensity;
    float DofStrength;

    float CinePad0;
    float4 PixelSize;

    float UseFxaa;
    float Vignette;
    float Grain;
    float GrainTime;
    float ChromAberration;
    float Sharpen;
    float Contrast;
    float Saturation;
    float Temperature;
    float TintGM;
    float Letterbox;
    float Halation;

    float4 HalationTint;

    float GrainSize;
    float GrainColour;
    float GrainShadow;
    float VignetteRoundness;

    float VignetteSoftness;
    float EdgeBlur;
    float EdgeBlurStart;
    float EdgeBlurElongation;

    float Dither;
    float Lift;
    float Gain;
    float Bleach;

    float HalationThreshold;
    float HalationSaturation;
    float HalationSoftness;

    float HdrScene;

    uint PostPass;
    uint RageTonemap;
    float LumBlend;
    float BloomAmount;
    float4 PostTexel;
    float ExposureBias;
    float AutoExposure;
    float BloomThresholdHdr;

    float Passthrough;

    float4 UnderwaterParams;

    float4 UnderwaterColour;

    float4 UnderwaterProj;

    float4 UnderwaterSun;

    float4 UnderwaterUp;

    float4 ExposureGame;

    float4 MaxBackground;
    float4 ClearColour;
}

Texture2D SceneTex : register(t0);

Texture2D AoTex : register(t1);
Texture2D BloomTex : register(t2);
Texture2D SsrTex : register(t3);
Texture2D DofTex : register(t4);

Texture2D LumSmallTex : register(t5);
Texture2D LumPrevTex : register(t6);
Texture2D LumCurTex : register(t7);
Texture2D RageBloomTex : register(t8);

Texture2D SceneDepthTex : register(t9);
SamplerState PointSampler : register(s0);
SamplerState LinearSampler : register(s1);

static const float MIDDLE_GRAY = 0.72;
static const float LUM_WHITE = 1.5;

static const float3 LumFactors = float3(0.299, 0.587, 0.114);

struct PS_Input
{
    float4 Pos : SV_POSITION;
    float2 UV : TEXCOORD0;
};

PS_Input VSMain(uint id : SV_VertexID)
{
    PS_Input o;
    o.UV = float2((id << 1) & 2, id & 2);
    o.Pos = float4(o.UV * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
    return o;
}

float FilmicChannel(float x)
{
    return ((x * (FilmicA * x + FilmicC * FilmicB) + FilmicD * FilmicE)
          / (x * (FilmicA * x + FilmicB) + FilmicD * FilmicF)) - FilmicE / FilmicF;
}

float3 FilmicToneMap(float3 c)
{
    float ooWhite = 1.0 / FilmicChannel(FilmicW);
    return saturate(float3(FilmicChannel(c.r), FilmicChannel(c.g), FilmicChannel(c.b)) * ooWhite);
}

float3 ApplyColorCorrection(float3 color)
{
    float lum = dot(color, LumFactors);

    color = lerp(lum.xxx, color, Desaturate);

    float3 result = color * lerp(ColorShiftLowLum.rgb, ColorCorrectHighLum.rgb,
                                 saturate(lum / max(ColorShiftLowLum.a, 1e-5)));

    float correctedHighLum = 1.0 - ColorCorrectHighLum.a;
    result = lerp(result, color, saturate((lum - correctedHighLum) / max(0.01, 1.0 - correctedHighLum)));

    return saturate(result);
}

float Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

float3 Fxaa(float2 uv)
{
    float2 px = PixelSize.xy;

    float3 rgbM = SceneTex.SampleLevel(LinearSampler, uv, 0).rgb;
    float lM = Luma(rgbM);
    float lNW = Luma(SceneTex.SampleLevel(LinearSampler, uv + float2(-px.x, -px.y), 0).rgb);
    float lNE = Luma(SceneTex.SampleLevel(LinearSampler, uv + float2( px.x, -px.y), 0).rgb);
    float lSW = Luma(SceneTex.SampleLevel(LinearSampler, uv + float2(-px.x,  px.y), 0).rgb);
    float lSE = Luma(SceneTex.SampleLevel(LinearSampler, uv + float2( px.x,  px.y), 0).rgb);

    float lMin = min(lM, min(min(lNW, lNE), min(lSW, lSE)));
    float lMax = max(lM, max(max(lNW, lNE), max(lSW, lSE)));

    float range = lMax - lMin;
    if (range < max(0.0312, lMax * 0.125)) return rgbM;

    float2 dir = float2(-((lNW + lNE) - (lSW + lSE)), ((lNW + lSW) - (lNE + lSE)));
    float reduce = max((lNW + lNE + lSW + lSE) * 0.03125, 0.0078125);
    float rcpDir = 1.0 / (min(abs(dir.x), abs(dir.y)) + reduce);
    dir = clamp(dir * rcpDir, -8.0, 8.0) * px;

    float3 rgbA = 0.5 * (SceneTex.SampleLevel(LinearSampler, uv + dir * (1.0 / 3.0 - 0.5), 0).rgb
                       + SceneTex.SampleLevel(LinearSampler, uv + dir * (2.0 / 3.0 - 0.5), 0).rgb);
    float3 rgbB = rgbA * 0.5 + 0.25 * (SceneTex.SampleLevel(LinearSampler, uv + dir * -0.5, 0).rgb
                                     + SceneTex.SampleLevel(LinearSampler, uv + dir *  0.5, 0).rgb);

    float lB = Luma(rgbB);
    return (lB < lMin || lB > lMax) ? rgbA : rgbB;
}

float3 ToDisplay(float3 v)
{
    float3 c = FilmicToneMap(max(v, 0.0) * Exposure);
    if (EnableColorCorrect) c = ApplyColorCorrection(c);
    return pow(max(c, 0.0), Gamma);
}

float3 FetchScene(float2 uv, out float3 sharpenDelta)
{
    sharpenDelta = 0;

    float2 fromCentre = uv - 0.5;
    float radius = length(fromCentre * float2(PixelSize.z / max(PixelSize.w, 1.0), 1.0)) * 1.4142;
    float edge = saturate((radius - EdgeBlurStart) / max(1.0 - EdgeBlurStart, 1e-3));

    float3 c = UseFxaa > 0.5 ? Fxaa(uv) : SceneTex.SampleLevel(PointSampler, uv, 0).rgb;

    if (EdgeBlur > 0.0001 && edge > 0.001)
    {

        float2 dir = normalize(fromCentre + 1e-6);
        dir = normalize(lerp(dir, float2(sign(dir.x + 1e-6), 0.0), EdgeBlurElongation));
        float2 step = dir * PixelSize.xy * (EdgeBlur * edge * edge * 22.0);

        float3 sum = c;
        [unroll]
        for (int i = 1; i <= 5; i++)
        {
            float t = (float)i / 5.0;
            sum += SceneTex.SampleLevel(LinearSampler, saturate(uv + step * t), 0).rgb;
            sum += SceneTex.SampleLevel(LinearSampler, saturate(uv - step * t), 0).rgb;
        }
        c = sum / 11.0;
    }

    if (ChromAberration > 0.0001)
    {
        float2 o = fromCentre * ChromAberration * 0.01;
        c.r = SceneTex.SampleLevel(LinearSampler, uv + o, 0).r;
        c.b = SceneTex.SampleLevel(LinearSampler, uv - o, 0).b;
    }

    if (Sharpen > 0.0001)
    {
        float2 px = PixelSize.xy;
        float3 blur = (SceneTex.SampleLevel(LinearSampler, uv + float2( px.x, 0), 0).rgb
                     + SceneTex.SampleLevel(LinearSampler, uv + float2(-px.x, 0), 0).rgb
                     + SceneTex.SampleLevel(LinearSampler, uv + float2(0,  px.y), 0).rgb
                     + SceneTex.SampleLevel(LinearSampler, uv + float2(0, -px.y), 0).rgb) * 0.25;

        sharpenDelta = (ToDisplay(c) - ToDisplay(blur)) * Sharpen * (1.0 - edge * saturate(EdgeBlur));
    }
    return max(c, 0.0);
}

float3 WhiteBalance(float3 c)
{
    if (abs(Temperature) < 0.0001 && abs(TintGM) < 0.0001) return c;
    float3 g = float3(1.0 + Temperature * 0.35,
                      1.0 - abs(Temperature) * 0.04 - TintGM * 0.18,
                      1.0 - Temperature * 0.35 + TintGM * 0.10);

    return c * g / max(dot(g, LumFactors), 1e-4);
}

float GrainNoise(float2 px, float t)
{
    uint3 v = uint3((uint)(px.x + 4096.0), (uint)(px.y + 4096.0), (uint)(t * 60.0));
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
    v ^= v >> 16u;
    v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
    return (float)(v.x & 0x00FFFFFFu) / 16777216.0;
}

static const float ExposureCurveA = -106.68907449987120;
static const float ExposureCurveB = 0.0041897015173052825;
static const float ExposureCurveOffset = 104.60364172443734;
static const float LumToGame = 0.0667;
float GameExposureStops(float lum)
{
    float e = ExposureCurveA * pow(max(lum * LumToGame, 1e-6), ExposureCurveB) + ExposureCurveOffset;
    e += ExposureGame.y;
    return clamp(e, min(ExposureGame.z, ExposureGame.w), ExposureGame.w);
}
float AdaptedLum()
{
    float l = LumCurTex.Load(int3(0, 0, 0)).r;
    if (AutoExposure < 0.5) l = 1.0;
    if (ExposureGame.x > 0.5 && AutoExposure >= 0.5)
    {

        float stops = min(GameExposureStops(max(l, 0.0)), log2(MIDDLE_GRAY / clamp(l, 0.2, 10.0)));
        return MIDDLE_GRAY / exp2(stops) * ExposureBias;
    }
    return clamp(l * ExposureBias, 0.2, 10.0);
}

float3 CodeWalkerToneMap(float3 c, float fLum)
{
    c *= MIDDLE_GRAY / (fLum + 0.001);
    c *= (1.0 + c / LUM_WHITE);
    c /= (1.0 + c);
    return c;
}

float4 PostPassMain(float2 uv)
{
    if (PostPass == 1)
    {

        float3 c = SceneTex.SampleLevel(LinearSampler, uv, 0).rgb;
        return float4(dot(c, LumFactors), 0, 0, 1);
    }
    if (PostPass == 2)
    {

        float t = max(LumSmallTex.SampleLevel(PointSampler, float2(0.5, 0.5), 7).r, 0.0);
        float c = max(LumPrevTex.Load(int3(0, 0, 0)).r, 0.0);
        if (LumBlend >= 0.999) return float4(t, 0, 0, 1);
        return float4(c + (t - c) * LumBlend, 0, 0, 1);
    }
    if (PostPass == 3)
    {

        float3 c = SceneTex.SampleLevel(LinearSampler, uv, 0).rgb;
        float fLum = max(LumCurTex.Load(int3(0, 0, 0)).r * ExposureBias, 0.75);
        if (AutoExposure < 0.5) fLum = max(ExposureBias, 0.75);
        if (ExposureGame.x > 0.5 && AutoExposure >= 0.5) fLum = max(AdaptedLum(), 0.75);
        c = max(0.0, c - BloomThresholdHdr);
        c *= MIDDLE_GRAY / (fLum + 0.001);
        c *= (1.0 + c / LUM_WHITE);
        c /= (1.0 + c);
        return float4(c, 1);
    }

    {
        static const float w[8] = { 0.1974, 0.1747, 0.1210, 0.0656, 0.0278, 0.0092, 0.0024, 0.0005 };
        float2 dir = (PostPass == 4) ? float2(PostTexel.x, 0) : float2(0, PostTexel.y);
        float3 acc = SceneTex.SampleLevel(LinearSampler, uv, 0).rgb * w[0];
        [unroll]
        for (int i = 1; i < 8; i++)
        {
            acc += SceneTex.SampleLevel(LinearSampler, saturate(uv + dir * i), 0).rgb * w[i];
            acc += SceneTex.SampleLevel(LinearSampler, saturate(uv - dir * i), 0).rgb * w[i];
        }
        return float4(acc, 1);
    }
}

float4 PSMain(PS_Input input) : SV_TARGET
{
    float2 uv = input.UV;
    if (PostPass != 0) return PostPassMain(uv);
    float3 sharpenDelta = 0;
    float3 c = Cinematic ? FetchScene(uv, sharpenDelta)
                         : SceneTex.SampleLevel(PointSampler, uv, 0).rgb;

    if (UnderwaterParams.x > 0.5)
    {
        float z = SceneDepthTex.SampleLevel(PointSampler, uv, 0).r;
        float viewD = z <= 0.0 ? 1e5 : UnderwaterProj.y / (z + UnderwaterProj.x);
        float2 ndc = uv * 2.0 - 1.0;
        float3 ray = float3(ndc.x * UnderwaterProj.z, -ndc.y * UnderwaterProj.w, 1.0);
        float dist = viewD * length(ray);

        float3 rd = normalize(ray);
        float upDot = dot(rd, UnderwaterUp.xyz);
        if (upDot > 1e-4) dist = min(dist, UnderwaterParams.z / upDot);
        float depthBlend = saturate(exp(-20.0 * UnderwaterParams.y * dist * 2.71828183));
        float3 fog = UnderwaterColour.rgb;

        {
            float t = UnderwaterParams.w;
            float sunSide = saturate(0.5 + 0.5 * dot(normalize(ray), UnderwaterSun.xyz));
            float ray1 = sin(uv.x * 23.0 + t * 0.35 + uv.y * 3.0) * sin(uv.x * 11.0 - t * 0.21);
            float ray2 = sin(uv.x * 37.0 - t * 0.27 + uv.y * 5.0);
            float rays = saturate(ray1 * 0.5 + ray2 * 0.35 + 0.15) * saturate(1.0 - uv.y * 1.2);
            fog *= 1.0 + rays * 0.9 * UnderwaterSun.w * sunSide;
        }
        c = lerp(fog, c, depthBlend);
    }

    if (Cinematic)
    {

        if (SsrIntensity > 0.001)
        {
            float4 r = SsrTex.SampleLevel(LinearSampler, uv, 0);
            c = lerp(c, r.rgb, saturate(r.a * SsrIntensity));
        }

        c *= AoTex.SampleLevel(LinearSampler, uv, 0).r;

        if (DofStrength > 0.001)
        {
            float4 d = DofTex.SampleLevel(LinearSampler, uv, 0);
            float blend = saturate(d.a * DofStrength);
            c = lerp(c, d.rgb, blend);

            sharpenDelta *= 1.0 - blend;
        }

        if (BloomStrength > 0.001 || Halation > 0.001)
        {
            float3 b = BloomTex.SampleLevel(LinearSampler, uv, 0).rgb;
            c += b * BloomStrength;

            if (Halation > 0.001)
            {
                float3 wide = b;
                float2 s = PixelSize.xy * (4.0 + HalationTint.w * 26.0);
                wide += BloomTex.SampleLevel(LinearSampler, saturate(uv + float2( s.x, 0)), 0).rgb;
                wide += BloomTex.SampleLevel(LinearSampler, saturate(uv + float2(-s.x, 0)), 0).rgb;
                wide += BloomTex.SampleLevel(LinearSampler, saturate(uv + float2(0,  s.y)), 0).rgb;
                wide += BloomTex.SampleLevel(LinearSampler, saturate(uv + float2(0, -s.y)), 0).rgb;
                wide += BloomTex.SampleLevel(LinearSampler, saturate(uv + s * 0.707), 0).rgb;
                wide += BloomTex.SampleLevel(LinearSampler, saturate(uv - s * 0.707), 0).rgb;
                wide /= 7.0;

                float hl = max(max(wide.r, wide.g), wide.b);
                float gate = smoothstep(HalationThreshold,
                                        HalationThreshold + max(HalationSoftness, 0.01), hl);

                float3 tint = lerp(1.0.xxx, HalationTint.rgb, HalationSaturation);
                c += wide * tint * (Halation * gate);
            }
        }
    }

    if (Passthrough > 0.5)
    {

        c = pow(saturate(c), Gamma);
    }
    else if (!Cinematic && RageTonemap)
    {

        float fLum = AdaptedLum();
        c = CodeWalkerToneMap(c, fLum);
        c += 0.6 * RageBloomTex.SampleLevel(LinearSampler, uv, 0).rgb * BloomAmount;

        c = pow(saturate(c), Gamma);
    }
    else
    {

        if (HdrScene > 0.5) c *= 0.72 / (AdaptedLum() + 0.001);
        c = FilmicToneMap(c * Exposure);

        if (EnableColorCorrect) c = ApplyColorCorrection(c);

        c = pow(max(c, 0.0), Gamma);
    }

    if (Cinematic)
    {

        c = saturate(c + sharpenDelta);

        c = WhiteBalance(c);

        if (abs(Lift) > 0.0001 || abs(Gain - 1.0) > 0.0001)
        {
            float l = dot(c, LumFactors);
            c = saturate(c + Lift * (1.0 - l) * (1.0 - l));
            c = saturate(c * lerp(1.0, Gain, l * l));
        }

        if (abs(Contrast - 1.0) > 0.0001) c = saturate((c - 0.5) * Contrast + 0.5);

        if (abs(Saturation - 1.0) > 0.0001)
            c = saturate(lerp(dot(c, LumFactors).xxx, c, Saturation));

        if (Bleach > 0.001)
        {
            float l = dot(c, LumFactors);
            float3 overlay = l < 0.5 ? (2.0 * c * l) : (1.0 - 2.0 * (1.0 - c) * (1.0 - l));
            c = saturate(lerp(c, overlay, Bleach));
        }

        if (Vignette > 0.001)
        {
            float aspect = PixelSize.z / max(PixelSize.w, 1.0);
            float2 v = (uv - 0.5) * 2.0;
            v.x *= lerp(1.0, aspect, VignetteRoundness);
            float r = saturate(length(v) / lerp(1.4142, max(aspect, 1.0) * 1.4142, VignetteRoundness));
            float falloff = pow(saturate(r), max(VignetteSoftness, 0.05) * 4.0);
            c *= saturate(1.0 - falloff * Vignette);
        }

        if (Grain > 0.001)
        {
            float2 cell = floor(uv * PixelSize.zw / max(GrainSize, 0.5));
            float n = GrainNoise(cell, GrainTime) - 0.5;

            float3 n3 = float3(n,
                               GrainNoise(cell + float2(311.0, 137.0), GrainTime) - 0.5,
                               GrainNoise(cell + float2(853.0, 419.0), GrainTime) - 0.5);
            n3 = lerp(n.xxx, n3, saturate(GrainColour));
            float shadowWeight = 1.0 - saturate(dot(c, LumFactors)) * saturate(GrainShadow);
            c = saturate(c + n3 * Grain * 0.15 * shadowWeight);
        }

        if (Letterbox > 0.0005)
        {
            float e = min(uv.y, 1.0 - uv.y);
            c *= smoothstep(Letterbox - PixelSize.y, Letterbox, e);
        }

        if (Dither > 0.001)
        {
            float d = GrainNoise(uv * PixelSize.zw, GrainTime * 0.37) - 0.5;
            c = saturate(c + d * Dither * (1.0 / 255.0));
        }
    }

    if (MaxBackground.w > 0.5)
    {
        float3 raw = SceneTex.SampleLevel(PointSampler, uv, 0).rgb;
        float bz = SceneDepthTex.SampleLevel(PointSampler, uv, 0).r;
        if (bz <= 0.0 && all(abs(raw - ClearColour.rgb) < 0.002)) c = MaxBackground.rgb;
    }

    return float4(c, 1.0);
}
