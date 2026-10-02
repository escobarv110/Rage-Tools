using System;
using CodeWalker.GameFiles;
using RageLightEditor.Rendering;
using SharpDX;
using CodeWalker;

namespace RageLightEditor.Editor
{
    public static partial class ExtensionHelpers
    {
        public const uint ShaftFlagUseSunDirection = 1u << 0;
        public const uint ShaftFlagUseSunColour = 1u << 1;
        public const uint ShaftFlagScaleBySunIntensity = 1u << 2;
        public const uint ShaftFlagScaleBySunColour = 1u << 3;
        public static readonly bool ShaftDebug = Environment.GetEnvironmentVariable("RLE_SHAFTDBG") == "1";
        public static int ShaftDebugLeft;
        private static void Dbg(string s) { if (ShaftDebug && ShaftDebugLeft-- > 0) Console.WriteLine("SHAFTDBG " + s); }
        public static readonly bool ShaftsFlagOnly = Environment.GetEnvironmentVariable("RLE_SHAFTFLAGONLY") == "1";
        public static readonly bool ShaftPinZero = Environment.GetEnvironmentVariable("RLE_SHAFTPINZERO") == "1";

        public struct ShaftSun
        {
            public Vector3 Travel;
            public Vector3 Colour;
            public float Brightness;
            public float Elevation;
        }

        public static ShaftSun SunFor(Vector3 toSun, Vector4 dirColour, bool sunIsLightingNow)
        {
            var s = new ShaftSun();
            var d = toSun.LengthSquared() > 1e-8f ? Vector3.Normalize(toSun) : Vector3.UnitZ;
            s.Travel = -d;
            s.Elevation = MathUtil.Clamp(d.Z, 0.0f, 1.0f);
            float mx = Math.Max(Math.Max(dirColour.X, dirColour.Y), Math.Max(dirColour.Z, 1e-4f));
            s.Colour = new Vector3(dirColour.X / mx, dirColour.Y / mx, dirColour.Z / mx);
            float lum = 0.2126f * dirColour.X + 0.7152f * dirColour.Y + 0.0722f * dirColour.Z;
            s.Brightness = (d.Z > 0.0f && sunIsLightingNow) ? lum : 0.0f;
            s.Brightness *= MathUtil.SmoothStep(MathUtil.Clamp(d.Z / 0.12f, 0.0f, 1.0f));
            return s;
        }

        public const float ShaftReferenceSun = 40.0f;
        public const float ShaftRadianceScale = 0.6f;

        public static bool AddLightShaftVolume(LightShaftRenderer shafts, in CExtensionDefLightShaft ls, Vector3 pos, Quaternion ori,
                                               Vector3 camPos, in ShaftSun sun, float hour, float userIntensity)
        {
            Vector3 W(Vector3 v) => ori.Multiply(v) + pos;
            var a0 = W(ls.cornerA); var b0 = W(ls.cornerB); var c0 = W(ls.cornerC); var d0 = W(ls.cornerD);
            var centre = (a0 + b0 + c0 + d0) * 0.25f;
            var X = ((b0 - a0) + (c0 - d0)) * 0.25f;
            var Y = ((d0 - a0) + (c0 - b0)) * 0.25f;
            if (X.LengthSquared() < 1e-8f || Y.LengthSquared() < 1e-8f) return false;

            var dir = ls.direction;
            if (dir.LengthSquared() < 1e-8f) dir = -Vector3.UnitZ; else dir.Normalize();
            var wdir = ori.Multiply(dir);
            if (wdir.LengthSquared() > 1e-8f) wdir.Normalize(); else wdir = -Vector3.UnitZ;
            uint flags = ls.flags;
            float amount = MathUtil.Clamp(ls.directionAmount, 0.0f, 1.0f);
            bool flagDir = (flags & ShaftFlagUseSunDirection) != 0;
            bool sunDir;
            if (ShaftsFlagOnly) sunDir = flagDir;
            else
            {
                if (ShaftPinZero && flagDir && amount <= 0.0f) sunDir = false;
                else { if (amount <= 0.0f) amount = 1.0f; sunDir = true; }
            }
            bool sunScale = (flags & ShaftFlagScaleBySunIntensity) != 0 || ls.scaleBySunIntensity != 0;
            bool sunColour = (flags & ShaftFlagUseSunColour) != 0;
            bool sunTint = (flags & ShaftFlagScaleBySunColour) != 0;
            var inward = ori.Multiply(dir);
            if (sunDir)
            {
                var blended = Vector3.Lerp(wdir, sun.Travel, amount);
                if (blended.LengthSquared() > 1e-6f) wdir = Vector3.Normalize(blended);
            }
            float len = ShaftReach_S5(Math.Max(ls.length, 0.05f), inward, wdir, sunDir);
            float pane = ShaftFlux_P3(wdir, inward, Vector3.Cross(X, Y), sunDir);
            var tip = centre + wdir * len;
            Dbg($"at {centre.X:0.0},{centre.Y:0.0},{centre.Z:0.0} beam {wdir.X:0.00},{wdir.Y:0.00},{wdir.Z:0.00} stored {dir.X:0.00},{dir.Y:0.00},{dir.Z:0.00} inward {inward.X:0.00},{inward.Y:0.00},{inward.Z:0.00} sun {sun.Travel.X:0.00},{sun.Travel.Y:0.00},{sun.Travel.Z:0.00} amount {amount:0.00} flag {flagDir} follows {sunDir} flux {pane:0.000} len {ls.length:0.0}->{len:0.0} tip {tip.X:0.0},{tip.Y:0.0},{tip.Z:0.0} inten {ls.intensity:0.00} flags {flags} fade {ls.fadeInTimeStart}-{ls.fadeInTimeEnd}/{ls.fadeOutTimeStart}-{ls.fadeOutTimeEnd}");
            if (pane <= 0.0f) return false;

            float r = ((ls.color >> 16) & 255) / 255.0f, g = ((ls.color >> 8) & 255) / 255.0f, b = (ls.color & 255) / 255.0f;
            var col = new Vector3(r, g, b);
            if (sunColour) col = sun.Colour;
            else if (sunTint) col *= sun.Colour;
            float sunAmount = sunScale ? sun.Brightness : ShaftReferenceSun;
            if (sunDir && !sunScale) sunAmount *= MathUtil.SmoothStep(MathUtil.Clamp(sun.Elevation / 0.12f, 0.0f, 1.0f));
            if (sunAmount <= 0.001f) return false;
            float hourFade = 1.0f;
            if (ls.fadeInTimeStart != 0 || ls.fadeInTimeEnd != 0 || ls.fadeOutTimeStart != 0 || ls.fadeOutTimeEnd != 0)
            {
                float fi0 = ls.fadeInTimeStart, fi1 = ls.fadeInTimeEnd, fo0 = ls.fadeOutTimeStart, fo1 = ls.fadeOutTimeEnd;
                float on = fi1 > fi0 ? MathUtil.Clamp((hour - fi0) / (fi1 - fi0), 0, 1) : (hour >= fi0 ? 1 : 0);
                float off = fo1 > fo0 ? MathUtil.Clamp((hour - fo0) / (fo1 - fo0), 0, 1) : (hour >= fo0 ? 1 : 0);
                hourFade = fo0 >= fi0 ? Math.Min(on, 1.0f - off) : Math.Max(on, 1.0f - off);
            }
            float centreDist = Vector3.Distance(camPos, centre + wdir * (len * 0.5f));
            float fs = ls.fadeDistanceStart, fe = ls.fadeDistanceEnd;
            if (fe <= fs + 0.01f) { fs = DefaultShaftRange * 0.7f; fe = DefaultShaftRange; }
            float distFade = 1.0f - MathUtil.Clamp((centreDist - fs) / Math.Max(fe - fs, 0.01f), 0.0f, 1.0f);
            float inten = ShaftRadiance_T5(ls.intensity, sunAmount) * hourFade * distFade * pane * userIntensity;
            if (inten <= 0.0005f) return false;

            float soft = MathUtil.Clamp(ls.softness, 0.0f, 1.0f);
            shafts.Add(centre, X, Y, wdir * len, soft, 1.0f, col * inten, (int)ls.densityType);
            return true;
        }
    }
}

