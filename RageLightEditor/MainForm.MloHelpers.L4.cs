using System;
using System.Collections.Generic;
using RageLightEditor.Editor;
using RageLightEditor.Rendering;
using SharpDX;
using SharpDX.Direct3D11;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private TriRenderer l4Tris;
        private TriRenderer L4Tris => l4Tris ??= new TriRenderer(deviceResources.Device);
        private TriRenderer l4Faces_U26;
        private TriRenderer L4Faces_U26 => l4Faces_U26 ??= new TriRenderer(deviceResources.Device);
        public static readonly Vector4 PortalFromGreen_U26 = new Vector4(0.0f, 1.0f, 0.0f, 0.35f);
        public static readonly Vector4 PortalToRed_U26 = new Vector4(1.0f, 0.0f, 0.0f, 0.35f);

        private const float L4EdgePx = 1.5f, L4SelEdgePx = 2.2f, L4TickPx = 2.4f, L4EntityPx = 1.0f;
        private const float L4GhostAlpha = 0.16f, L4GhostSelAlpha = 0.70f, L4RoomFillAlpha = 0.06f, L4PortalFillAlpha = 0.12f, L4PortalSelFillAlpha = 0.24f;

        private static readonly bool L4OldHelpers = string.Equals(Environment.GetEnvironmentVariable("RLE_MLOHELPERS"), "old", StringComparison.OrdinalIgnoreCase);

        private static Vector4 L4Alpha(Vector4 c, float a) => new Vector4(c.X, c.Y, c.Z, a);

        private const float L4PullFraction = 0.008f;
        private Vector3 L4Pull(Vector3 p) => p + (camera.Position - p) * L4PullFraction;

        private void L4FlushDepthTested()
        {
            var ctx = deviceResources.Context;
            L4Tris.Flush(ctx, camera.ViewProjMatrix, CommonStates.BlendAlpha, CommonStates.DepthReadOnly);
        }

        private void L4Edge(TriRenderer depthTested, Vector3 a, Vector3 b, Vector4 col, float px, float ghost)
        {
            var d = b - a; float len = d.Length();
            if (len < 1e-5f) return;
            int pieces = Math.Clamp((int)Math.Ceiling(len / 2.5f), 1, 12);
            var cam = camera.Position;
            var gcol = L4Alpha(col, col.W * ghost);
            for (int i = 0; i < pieces; i++)
            {
                var p0 = a + d * ((float)i / pieces); var p1 = a + d * ((float)(i + 1) / pieces);
                float wpp = camera.WorldPerPixel((p0 + p1) * 0.5f);
                depthTested.AddThickLineAA(L4Pull(p0), L4Pull(p1), cam, px * 0.5f * wpp, 1.2f * wpp, col);
                if (ghost > 0.001f) triRenderer.AddThickLineAA(p0, p1, cam, px * 0.5f * wpp, 1.2f * wpp, gcol);
            }
        }

        private static void L4BoxCorners(Vector3 mn, Vector3 mx, Func<Vector3, Vector3> toWorld, Vector3[] c)
        {
            for (int i = 0; i < 8; i++)
                c[i] = toWorld(new Vector3((i & 1) != 0 ? mx.X : mn.X, (i & 2) != 0 ? mx.Y : mn.Y, (i & 4) != 0 ? mx.Z : mn.Z));
        }

        private static readonly (int a, int b)[] L4BoxEdges =
        {
            (0, 1), (2, 3), (4, 5), (6, 7),
            (0, 2), (1, 3), (4, 6), (5, 7),
            (0, 4), (1, 5), (2, 6), (3, 7),
        };

        private void L4EdgeBox(Vector3[] c, Vector4 col, float px, float ghost, bool ticks, float fillAlpha, bool camInside)
        {
            var dt = L4Tris;
            foreach (var (a, b) in L4BoxEdges) L4Edge(dt, c[a], c[b], col, px, ghost);
            if (ticks)
            {
                var tcol = new Vector4(Math.Min(col.X * 1.25f, 3.0f), Math.Min(col.Y * 1.25f, 3.0f), Math.Min(col.Z * 1.25f, 3.0f), Math.Min(col.W * 1.15f, 1.0f));
                foreach (var (a, b) in L4BoxEdges)
                {
                    var d = c[b] - c[a]; float len = d.Length();
                    if (len < 1e-4f) continue;
                    float t = Math.Min(0.28f, len * 0.16f);
                    var dn = d / len;
                    L4Edge(dt, c[a], c[a] + dn * t, tcol, L4TickPx, ghost * 1.4f);
                    L4Edge(dt, c[b], c[b] - dn * t, tcol, L4TickPx, ghost * 1.4f);
                }
            }
            if (fillAlpha > 0.001f && !camInside)
            {
                var fc = new Vector4(col.X * (FillHdr / HelperHdr), col.Y * (FillHdr / HelperHdr), col.Z * (FillHdr / HelperHdr), fillAlpha);
                var q = new Vector3[8];
                for (int i = 0; i < 8; i++) q[i] = L4Pull(c[i]);
                dt.AddQuad(q[0], q[2], q[3], q[1], fc); dt.AddQuad(q[4], q[5], q[7], q[6], fc);
                dt.AddQuad(q[0], q[1], q[5], q[4], fc); dt.AddQuad(q[2], q[6], q[7], q[3], fc);
                dt.AddQuad(q[0], q[4], q[6], q[2], fc); dt.AddQuad(q[1], q[3], q[7], q[5], fc);
            }
        }

        private void L4Portal(Vector3[] wc, Vector4 col, Vector4 windingCol, bool markWinding, float px, float ghost, float fillAlpha, bool arrow, bool oneWay, float arrowLen = 0.45f, float fromSign = 0f)
        {
            int n = wc.Length;
            if (n < 3) return;
            var dt = L4Tris;
            for (int i = 0; i < n; i++)
                L4Edge(dt, wc[i], wc[(i + 1) % n], (i == 0 && markWinding) ? windingCol : col, px, ghost);
            var fc = new Vector4(col.X * (FillHdr / HelperHdr), col.Y * (FillHdr / HelperHdr), col.Z * (FillHdr / HelperHdr), fillAlpha);
            var gfc = L4Alpha(fc, fillAlpha * 0.25f);
            var pc = new Vector3[n];
            for (int i = 0; i < n; i++) pc[i] = L4Pull(wc[i]);
            if (fromSign != 0f)
            {
                float k = Math.Clamp(fillAlpha / L4PortalFillAlpha, 0.25f, 2.0f);
                L4PortalFaces_U26(pc, fromSign, L4Alpha(PortalFromGreen_U26, PortalFromGreen_U26.W * k), L4Alpha(PortalToRed_U26, PortalToRed_U26.W * k));
            }
            else if (n == 4) { dt.AddQuad(pc[0], pc[1], pc[2], pc[3], fc); if (ghost > 0.001f) triRenderer.AddQuad(wc[0], wc[1], wc[2], wc[3], gfc); }
            else for (int i = 1; i + 1 < n; i++) { dt.AddTri(pc[0], pc[i], pc[i + 1], fc); if (ghost > 0.001f) triRenderer.AddTri(wc[0], wc[i], wc[i + 1], gfc); }
            if (!arrow) return;
            var c = Vector3.Zero; foreach (var v in wc) c += v; c /= n;
            var nrm = Vector3.Cross(wc[1] - wc[0], wc[2] - wc[0]);
            if (nrm.LengthSquared() > 1e-10f) nrm.Normalize(); else nrm = Vector3.UnitY;
            var side = Vector3.Cross(nrm, Vector3.UnitZ); if (side.LengthSquared() < 1e-6f) side = Vector3.UnitX; side.Normalize();
            var up = Vector3.Cross(side, nrm);
            void Arrow(Vector3 dir, float l)
            {
                var tip = c + dir * l;
                L4Edge(dt, c, tip, col, px, ghost);
                var coneFill = new Vector4(fc.X, fc.Y, fc.Z, 0.55f);
                dt.AddCone(L4Pull(tip), L4Pull(tip - dir * (l * 0.35f)), side, up, l * 0.12f, col, coneFill, 12);
                if (ghost > 0.001f) triRenderer.AddCone(tip, tip - dir * (l * 0.35f), side, up, l * 0.12f, L4Alpha(col, col.W * ghost), L4Alpha(coneFill, 0.55f * ghost), 12);
            }
            Arrow(nrm, arrowLen);
            if (!oneWay) Arrow(-nrm, arrowLen * 0.6f);
        }

        private void L4PortalFaces_U26(Vector3[] pc, float fromSign, Vector4 fromCol, Vector4 toCol)
        {
            int n = pc.Length;
            var f = L4Faces_U26;
            for (int i = 1; i + 1 < n; i++)
            {
                var a = pc[0]; var b = pc[i]; var c = pc[i + 1];
                bool aligned = Vector3.Dot(Vector3.Cross(b - a, c - a), PortalNormal_U26(pc)) * fromSign > 0f;
                if (aligned) { f.AddTri(a, b, c, fromCol); f.AddTri(a, c, b, toCol); }
                else { f.AddTri(a, c, b, fromCol); f.AddTri(a, b, c, toCol); }
            }
        }

        private static Vector3 PortalNormal_U26(Vector3[] p)
        {
            var nrm = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
            return nrm.LengthSquared() > 1e-12f ? Vector3.Normalize(nrm) : Vector3.UnitY;
        }

        private bool DrawMloCreatorHelpers_L4(MloCreatorPanel ui, MloCreatorSession s)
        {
            if (L4OldHelpers || s == null) return false;
            var camPos = camera.Position;
            var corners = new Vector3[8];
            Vector3 Same(Vector3 v) => v;

            for (int i = 0; i < s.Rooms.Count; i++)
            {
                var r = s.Rooms[i];
                if (!r.IsValid) continue;
                bool sel = ui.SelectedRoom == i && ui.SelectedPortal < 0;
                if (i == 0 && !sel && !ui.ShowAllRooms) continue;
                var col = sel ? CrSel : (i == 0 ? CrLimbo : CrRoom);
                L4BoxCorners(r.Min, r.Max, Same, corners);
                bool inside = r.Contains(camPos);
                L4EdgeBox(corners, col, sel ? L4SelEdgePx : L4EdgePx, sel ? L4GhostSelAlpha : (i == 0 ? 0.0f : L4GhostAlpha),
                          ticks: i > 0 || sel, fillAlpha: sel ? L4RoomFillAlpha : 0.0f, camInside: inside);
                if (ui.LabelFor_V19(sel))
                {
                    int n = s.CountInRoom(i);
                    DrawWorldLabel(new Vector3(r.Centre.X, r.Centre.Y, r.Max.Z),
                        $"{i}: {r.Name}  ({n})", sel ? new Vector4(1.0f, 0.85f, 0.45f, 1.0f) : new Vector4(1, 1, 1, 0.92f));
                }
            }

            var winding = C(1.0f, 0.45f, 0.35f, 1.0f);
            for (int i = 0; i < s.Portals.Count; i++)
            {
                var p = s.Portals[i];
                if (p.Corners == null || p.Corners.Length < 3) continue;
                bool sel = ui.SelectedPortal == i;
                uint pf = p.Flags;
                Vector4 pcol = T(UiTheme.Accent, 1.0f);
                if ((pf & 2048u) != 0) pcol = C(0.30f, 0.85f, 1.0f, 1.0f);
                if ((pf & (4u | 16u | 128u | 256u | 512u | 1024u)) != 0) pcol = C(0.80f, 0.55f, 1.0f, 1.0f);
                if ((pf & 2u) != 0) pcol = C(0.45f, 0.85f, 0.55f, 1.0f);
                if (sel) pcol = CrSel;
                L4Portal(p.Corners, pcol, winding, markWinding: true, px: sel ? L4SelEdgePx : L4EdgePx,
                         ghost: sel ? L4GhostSelAlpha : L4GhostAlpha * 1.5f, fillAlpha: sel ? L4PortalSelFillAlpha : L4PortalFillAlpha,
                         arrow: true, oneWay: (pf & 1u) != 0);
                if (ui.LabelFor_V19(sel))
                {
                    string from = p.RoomFrom >= 0 && p.RoomFrom < s.Rooms.Count ? s.Rooms[p.RoomFrom].Name : "?";
                    string to = p.RoomTo >= 0 && p.RoomTo < s.Rooms.Count ? s.Rooms[p.RoomTo].Name : "?";
                    DrawWorldLabel(p.Centre + new Vector3(0, 0, 0.15f), $"P{i}: {from} -> {to}",
                        sel ? new Vector4(1.0f, 0.85f, 0.45f, 1.0f) : new Vector4(UiTheme.AccentBright.X, UiTheme.AccentBright.Y, UiTheme.AccentBright.Z, 0.92f));
                }
            }

            for (int i = 0; i < s.Entities.Count; i++)
            {
                var e = s.Entities[i];
                if (!e.Include) continue;
                bool sel = ui.SelectedEntity == i;
                if (!sel && ui.LabelMode_V19 != MloCreatorPanel.MloLabels_V19.All) continue;
                var col = sel ? CrSel : L4Alpha(e.Room == 0 ? CrLimbo : CrRoom, 0.55f);
                if (MloEntityBox_S1(e, out var lb, out var lw))
                {
                    L4BoxCorners(lb.Minimum, lb.Maximum, p => Vector3.TransformCoordinate(p, lw), corners);
                    L4EdgeBox(corners, col, sel ? L4SelEdgePx : L4EntityPx, sel ? 0.45f : 0.0f, ticks: sel, fillAlpha: 0.0f, camInside: true);
                }
                else
                {
                    float h = sel ? 0.25f : 0.12f;
                    L4Edge(L4Tris, e.Position - Vector3.UnitX * h, e.Position + Vector3.UnitX * h, col, L4EntityPx, sel ? 0.45f : 0f);
                    L4Edge(L4Tris, e.Position - Vector3.UnitY * h, e.Position + Vector3.UnitY * h, col, L4EntityPx, sel ? 0.45f : 0f);
                    L4Edge(L4Tris, e.Position - Vector3.UnitZ * h, e.Position + Vector3.UnitZ * h, col, L4EntityPx, sel ? 0.45f : 0f);
                }
                if (sel && ui.LabelFor_V19(true)) DrawWorldLabel(e.Position + new Vector3(0, 0, 0.4f), $"{e.Label}  (room {e.Room})", new Vector4(1.0f, 0.85f, 0.45f, 1.0f));
            }

            L4FlushDepthTested();
            return true;
        }
    }
}

