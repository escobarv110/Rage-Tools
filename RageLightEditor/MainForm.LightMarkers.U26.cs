using System;
using System.Collections.Generic;
using CodeWalker;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        public const float LightIconPx_U26 = 7.0f;
        public const float SpotIconLenPx_U26 = 22.0f;
        public const float CapsuleIconLenPx_U26 = 20.0f;
        private const float LightIconRange_U26 = 150.0f;
        private const int LightIconCap_U26 = 800;
        private static readonly Vector4 LightIconRim_U26 = new Vector4(0.02f, 0.02f, 0.03f, 0.9f);

        private readonly List<(Vector3 pos, Vector3 dir, LightType type, Vector4 col)> lightIcons_U26 =
            new List<(Vector3 pos, Vector3 dir, LightType type, Vector4 col)>();

        public int LightIconCount_U26 => lightIcons_U26.Count;

        public static Vector4 LightIconColour_U26(byte r, byte g, byte b)
        {
            float m = Math.Max(r, Math.Max(g, b));
            if (m < 8f) return new Vector4(1, 1, 1, 1);
            return new Vector4(r / m, g / m, b / m, 1f);
        }

        private void CollectLightIcons_U26()
        {
            lightIcons_U26.Clear();
            if (SelMode != WorldSelectionMode.Light || !panel.ShowSelectionHelpers) return;
            var vis = World.Visible;
            var L = worldRender.Lights;
            var camPos = camera.Position;
            float range2 = LightIconRange_U26 * LightIconRange_U26;
            var selLight = WorldEdit.Selection.Light;
            var hovLight = worldHoverSel.Light;
            for (int vi = 0; vi < vis.Count && lightIcons_U26.Count < LightIconCap_U26; vi++)
            {
                var e = vis[vi];
                var arch = e?.Archetype;
                if (arch == null) continue;
                float reach = e.BSRadius + 2.0f;
                if (Vector3.DistanceSquared(e.Position, camPos) > range2 + reach * reach) continue;
                if (!L.TryGetDefs(arch.Hash, out var defs) || defs == null) continue;
                var ori = e.Orientation;
                var scale = e.Scale; if (scale.X <= 0.0f) scale = Vector3.One;
                for (int i = 0; i < defs.Length; i++)
                {
                    var la = defs[i].L;
                    if (la == null || ReferenceEquals(la, selLight) || ReferenceEquals(la, hovLight)) continue;
                    var wpos = ori.Multiply(defs[i].Pos * scale) + e.Position;
                    if (Vector3.DistanceSquared(wpos, camPos) > range2) continue;
                    var dir = ori.Multiply(defs[i].Dir);
                    if (dir.LengthSquared() > 1e-8f) dir.Normalize(); else dir = -Vector3.UnitZ;
                    lightIcons_U26.Add((wpos, dir, la.Type, LightIconColour_U26(la.ColorR, la.ColorG, la.ColorB)));
                }
            }
        }

        private void AddLightIcons_U26(TriRenderer tr)
        {
            if (lightIcons_U26.Count == 0) return;
            var cp = camera.Position;
            var (sr, su, _) = GizmoStyle.ScreenBasis(camera);
            foreach (var ic in lightIcons_U26)
            {
                float wpp = camera.WorldPerPixel(ic.pos);
                float r = LightIconPx_U26 * wpp;
                float rim = 1.5f * wpp;
                switch (ic.type)
                {
                    case LightType.Spot:
                    {
                        var dir = ic.dir;
                        var a = Vector3.Cross(dir, Math.Abs(dir.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitX);
                        a.Normalize();
                        var b = Vector3.Cross(dir, a);
                        var baseC = ic.pos + dir * (SpotIconLenPx_U26 * wpp);
                        float br = r * 1.05f;
                        tr.AddCone(ic.pos - dir * rim, baseC + dir * rim, a, b, br + rim, LightIconRim_U26, LightIconRim_U26, 20);
                        tr.AddDisc(baseC + dir * rim, a, b, br + rim, LightIconRim_U26, LightIconRim_U26, 20);
                        var dark = new Vector4(ic.col.X * 0.55f, ic.col.Y * 0.55f, ic.col.Z * 0.55f, 1f);
                        tr.AddCone(ic.pos, baseC, a, b, br, ic.col, dark, 20);
                        tr.AddDisc(baseC, a, b, br, ic.col, ic.col, 20);
                        tr.AddDiscAA(ic.pos, sr, su, r * 0.45f, 0.75f * wpp, new Vector4(1, 1, 1, 1), 16);
                        break;
                    }
                    case LightType.Capsule:
                    {
                        var half = ic.dir * (CapsuleIconLenPx_U26 * 0.5f * wpp);
                        tr.AddCapsuleAA(ic.pos - half, ic.pos + half, cp, r * 0.75f + rim, 0.75f * wpp, LightIconRim_U26);
                        tr.AddCapsuleAA(ic.pos - half, ic.pos + half, cp, r * 0.75f, 0.75f * wpp, ic.col);
                        break;
                    }
                    default:
                    {
                        tr.AddDiscAA(ic.pos, sr, su, r + rim, 0.75f * wpp, LightIconRim_U26, 24);
                        tr.AddDiscAA(ic.pos, sr, su, r, 0.75f * wpp, ic.col, 24);
                        tr.AddDiscAA(ic.pos, sr, su, r * 0.4f, 0.75f * wpp, new Vector4(1, 1, 1, 1), 16);
                        break;
                    }
                }
            }
            lightIcons_U26.Clear();
        }
    }
}
