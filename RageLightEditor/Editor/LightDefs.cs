using System;

namespace RageLightEditor.Editor
{
    public static partial class LightDefs
    {
        public struct FlagDef
        {
            public int Bit;
            public string Name;
            public string Tooltip;
            public FlagDef(int bit, string name, string tip) { Bit = bit; Name = name; Tooltip = tip; }
        }

        public static readonly FlagDef[] Flags =
        {
            new FlagDef(0,  "Interior Only", "Only rendered when the camera is inside an interior"),
            new FlagDef(1,  "Exterior Only", "Only rendered when NOT inside an interior"),
            new FlagDef(2,  "Don't Use In Cutscene", "Not rendered in cutscenes"),
            new FlagDef(3,  "Vehicle", "Light rendered on vehicles"),
            new FlagDef(4,  "Ignore Artificial Light State", "Keeps rendering during script blackouts (SET_ARTIFICIAL_LIGHTS_STATE)"),
            new FlagDef(5,  "Texture Projection", "Enables the projected texture (uses ProjectedTextureHash)"),
            new FlagDef(6,  "Cast Shadows", "Light casts shadows"),
            new FlagDef(7,  "Cast Static Shadows", "Static geometry casts shadows from this light"),
            new FlagDef(8,  "Cast Dynamic Shadows", "Movable objects cast shadows from this light"),
            new FlagDef(9,  "Calculate From Sun", "Colour/intensity depend on sun position / time of day"),
            new FlagDef(10, "Enable Buzzing", "Electrical hum sound 1"),
            new FlagDef(11, "Force Buzzing", "Electrical hum sound 2 (the 'electric' flag)"),
            new FlagDef(12, "Draw Volume", "Force-enable volumetric light rendering, ignoring timecycle"),
            new FlagDef(13, "No Specular", "Light does not produce specular reflections"),
            new FlagDef(14, "Both Interior And Exterior", "Rendered both inside and outside interiors"),
            new FlagDef(15, "Corona Only", "Only the corona is rendered; actual lighting is disabled"),
            new FlagDef(16, "Not In Reflection", "Not rendered in reflections / mirror portals"),
            new FlagDef(17, "Only In Reflection", "Only rendered in reflections (GIMS calls this 'Disable corona')"),
            new FlagDef(18, "Enable Culling Plane", "Activates CullingPlaneNormal/Offset (clips light on one side)"),
            new FlagDef(19, "Enable Volume Outer Colour", "Activates VolumeOuterColour/Intensity/Exponent"),
            new FlagDef(20, "Higher Res Shadows", ""),
            new FlagDef(21, "Only Low Res Shadows", ""),
            new FlagDef(22, "Far LOD Light", ""),
            new FlagDef(23, "Don't Light Alpha", "Does not affect transparent geometry such as glass"),
            new FlagDef(24, "Cast Shadows If Possible", ""),
            new FlagDef(25, "Cutscene", "Rendered in cutscenes"),
            new FlagDef(26, "Moving Light Source", ""),
            new FlagDef(27, "Use Vehicle Twin", ""),
            new FlagDef(28, "Force Medium LOD Light", ""),
            new FlagDef(29, "Corona Only LOD Light", ""),
            new FlagDef(30, "Delayed Render", "Create shadow-casting light early in the frame (avoids shadow pop-in)"),
            new FlagDef(31, "Already Tested For Occlusion", "Runtime flag"),
        };

        public const uint FlagCullingPlane = 0x40000;
        public const uint FlagVolumeOuterColour = 0x80000;
        public const uint FlagDrawVolume = 0x1000;
        public const uint FlagCoronaOnly = 0x8000;
        public const uint FlagTextureProjection = 0x20;
        public const uint FlagNoSpecular = 0x2000;

        public static readonly string[] FlashinessNames =
        {
            "0 Constant",
            "1 Random",
            "2 Random (override if wet)",
            "3 Once per second",
            "4 Twice per second",
            "5 Five times per second",
            "6 Random flashiness",
            "7 Off",
            "8 Unused",
            "9 Alarm",
            "10 On when raining",
            "11 Cycle 1",
            "12 Cycle 2",
            "13 Cycle 3",
            "14 Disco",
            "15 Candle",
            "16 Plane",
            "17 Fire",
            "18 Threshold",
            "19 Electric",
            "20 Strobe",
        };

        public static readonly string[] TypeNames = { "Point", "Spot", "Capsule" };

        public static byte TypeToIndex(byte type)
        {
            switch (type)
            {
                case 1: return 0;
                case 2: return 1;
                case 4: return 2;
                default: return 0;
            }
        }

        public static byte IndexToType(int index)
        {
            switch (index)
            {
                case 0: return 1;
                case 1: return 2;
                case 2: return 4;
                default: return 1;
            }
        }

        public static string HourLabel(int bit)
        {
            int h = bit % 24;
            int h12 = h % 12; if (h12 == 0) h12 = 12;
            string ampm = h < 12 ? "AM" : "PM";
            return $"{h12}{ampm}";
        }

        public static bool IsActiveAtHour(uint timeFlags, int hour)
        {
            if (timeFlags == 0) return true;
            return (timeFlags & (1u << (hour % 24))) != 0;
        }

        public static float GetFlashMultiplier(byte flashiness, float time, int lightSeed)
        {
            float seed = (lightSeed * 0.6180339887f) % 1.0f;
            switch (flashiness)
            {
                case 0: return 1.0f;
                case 1:
                case 2:
                case 6:
                    return Hash01((float)Math.Floor(time * 9.0f) + seed * 100.0f) > 0.5f ? 1.0f : 0.35f;
                case 3: return ((time + seed) % 1.0f) < 0.5f ? 1.0f : 0.0f;
                case 4: return ((time + seed) % 0.5f) < 0.25f ? 1.0f : 0.0f;
                case 5: return ((time + seed) % 0.2f) < 0.1f ? 1.0f : 0.0f;
                case 7: return 0.0f;
                case 9:
                    return 0.5f + 0.5f * (float)Math.Sin((time + seed) * Math.PI * 2.0 / 1.5);
                case 10: return 1.0f;
                case 11: return ((time / 3.0f + 0.000f) % 1.0f) < 0.333f ? 1.0f : 0.0f;
                case 12: return ((time / 3.0f + 0.333f) % 1.0f) < 0.333f ? 1.0f : 0.0f;
                case 13: return ((time / 3.0f + 0.666f) % 1.0f) < 0.333f ? 1.0f : 0.0f;
                case 14:
                    return Hash01((float)Math.Floor(time * 4.0f) + seed) > 0.4f ? 1.0f : 0.1f;
                case 15:
                    return 0.75f + 0.25f * Noise(time * 3.0f + seed * 10.0f);
                case 16:
                    return ((time + seed) % 1.2f) < 0.1f ? 1.0f : 0.05f;
                case 17:
                    return 0.65f + 0.35f * Noise(time * 6.0f + seed * 10.0f);
                case 18: return 1.0f;
                case 19:
                    return Hash01((float)Math.Floor(time * 14.0f) + seed) > 0.12f ? 1.0f : 0.2f;
                case 20: return ((time + seed) % 0.15f) < 0.05f ? 1.0f : 0.0f;
                default: return 1.0f;
            }
        }

        private static float Hash01(float x)
        {
            double s = Math.Sin(x * 127.1) * 43758.5453;
            return (float)(s - Math.Floor(s));
        }

        private static float Noise(float t)
        {
            float f = (float)Math.Floor(t);
            float fr = t - f;
            float a = Hash01(f);
            float b = Hash01(f + 1);
            float u = fr * fr * (3 - 2 * fr);
            return a + (b - a) * u;
        }
    }
}

