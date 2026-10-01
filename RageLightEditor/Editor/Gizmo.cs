using System;
using CodeWalker.GameFiles;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor.Editor
{
    public enum GizmoMode
    {
        Select = 0,
        Translate = 1,
        Rotate = 2,
    }

    public class Gizmo
    {
        public GizmoMode Mode = GizmoMode.Translate;
        public GizmoMode EffectiveMode => Mode == GizmoMode.Select ? GizmoMode.Translate : Mode;
        public bool Enabled = true;
        private bool dragging;
        public bool DebugForceDragging;
        public bool Dragging { get => dragging || DebugForceDragging; private set => dragging = value; }
        public float RotateSnapDeg = 5.0f;

        public bool CloneDragActive { get; private set; }

        public bool CloneWasInstance { get; private set; }

        private int hotPart = -1;
        private int activePart = -1;
        private int hotLight = -1;

        private Scene scene;
        public Scene Scene
        {
            get => scene;
            set { if (ReferenceEquals(scene, value)) return; if (Dragging) MouseUp(); scene = value; hotPart = -1; hotLight = -1; }
        }

        private LightAttributes dragLight;
        private Vector3 startWorldPos;
        private Vector3 startWorldDir;
        private Vector3 startWorldTan;
        private readonly System.Collections.Generic.List<(LightAttributes l, Vector3 pos, Vector3 dir, Vector3 tan)>
            dragGroup = new System.Collections.Generic.List<(LightAttributes, Vector3, Vector3, Vector3)>();
        private float startAxisT;
        private Vector3 planeHitStart;
        private Vector3 rotStartVec;
        private Vector3 dragAxis;
        private float lastRotAngle;

        private readonly Vector3[] axes = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };

        public bool LocalSpace;
        public int SnapMode;
        public VertexSnapHit LastSnap;
        public string SnapNote = "";

        public static void LocalFrame_U11(Vector3 dir, Vector3 tan, Vector3[] into)
        {
            var z = dir.LengthSquared() > 1e-9f ? Vector3.Normalize(dir) : Vector3.UnitZ;
            var x = tan - z * Vector3.Dot(tan, z);
            if (x.LengthSquared() < 1e-6f) x = Perp(z).a;
            x.Normalize();
            var y = Vector3.Cross(z, x);
            y.Normalize();
            into[0] = x; into[1] = y; into[2] = z;
        }

        private void RefreshAxes_U11(LightAttributes l)
        {
            if (!LocalSpace || l == null || scene == null || !scene.Lights.Contains(l))
            {
                axes[0] = Vector3.UnitX; axes[1] = Vector3.UnitY; axes[2] = Vector3.UnitZ;
                return;
            }
            var inst = scene.GetInstance(l);
            LocalFrame_U11(inst.WorldDirection, inst.WorldTangent, axes);
        }

        public bool ResolveSnap_U11(Camera cam, Ray ray, float mx, float my, float vw, float vh, out Vector3 target, out string note)
        {
            target = default;
            note = "";
            LastSnap = default;
            if (scene == null) return false;
            var meshes = scene.AllMeshes;
            if (SnapMode == 1)
            {
                if (!MloVertexSnap.Find(meshes, cam, ray, mx, my, vw, vh, out var hit)) return false;
                LastSnap = hit;
                target = hit.Position;
                note = $"snapped to vertex  {target.X:0.00}, {target.Y:0.00}, {target.Z:0.00}";
                return true;
            }
            float best = float.MaxValue;
            bool any = false;
            foreach (var mesh in meshes)
            {
                if (mesh == null || !mesh.Visible || mesh.NeverDraw || mesh.PickVerts == null || mesh.PickIndices == null || mesh.PickIndices.Length < 3) continue;
                var b = mesh.WorldBounds;
                if (b.Maximum.X <= b.Minimum.X) continue;
                if (!ray.Intersects(ref b, out float bt) || bt > best) continue;
                if (MloVertexSnap.RayHitTriangle(mesh, ref ray, out float t, out _) && t < best) { best = t; any = true; }
            }
            if (!any) return false;
            target = ray.Position + ray.Direction * best;
            note = $"snapped to surface  {target.X:0.00}, {target.Y:0.00}, {target.Z:0.00}";
            return true;
        }

        public Gizmo(Scene scene)
        {
            this.scene = scene;
        }

        public float GetScale(Camera cam, Vector3 pos) => GizmoStyle.Scale(cam, pos);

        private Vector3 SelectionCentroid()
        {
            var sum = Vector3.Zero;
            int n = 0;
            foreach (var si in scene.SelectedIndices)
            {
                if (si < 0 || si >= scene.Lights.Count) continue;
                sum += scene.GetInstance(scene.Lights[si]).WorldPosition;
                n++;
            }
            return n > 0 ? sum / n : Vector3.Zero;
        }

        public bool MouseDown(Camera cam, float mx, float my, float vw, float vh,
            bool shiftClone = false, bool altInstance = false)
        {
            if (!Enabled) return false;

            UpdateHot(cam, mx, my, vw, vh);
            bool multi = hotLight == -2;
            if (hotPart < 0 || (!multi && (hotLight < 0 || hotLight >= scene.Lights.Count))) return false;
            var l = multi ? scene.SelectedLight : scene.Lights[hotLight];
            if (l == null) return false;

            if ((shiftClone || altInstance) && EffectiveMode == GizmoMode.Translate)
            {
                if (multi)
                {
                    var clones = scene.BeginCloneDragGroup(altInstance);
                    if (clones == null || clones.Count == 0) return false;
                    l = clones[clones.Count - 1];
                }
                else
                {
                    var clone = scene.BeginCloneDrag(l, altInstance);
                    if (clone == null) return false;
                    l = clone;
                }
                CloneDragActive = true;
                CloneWasInstance = altInstance;
            }
            else
            {
                scene.PushUndo();
                CloneDragActive = false;
            }

            var inst = scene.GetInstance(l);
            var ray = cam.GetPickRay(mx, my, vw, vh);

            dragLight = l;
            activePart = hotPart;
            Dragging = true;
            startWorldPos = multi ? SelectionCentroid() : inst.WorldPosition;
            startWorldDir = inst.WorldDirection;
            startWorldTan = inst.WorldTangent;
            lastRotAngle = 0;

            dragGroup.Clear();
            foreach (var si in scene.SelectedIndices)
            {
                if (si < 0 || si >= scene.Lights.Count) continue;
                var sl = scene.Lights[si];
                var sinst = scene.GetInstance(sl);
                dragGroup.Add((sl, sinst.WorldPosition, sinst.WorldDirection, sinst.WorldTangent));
            }

            if (EffectiveMode == GizmoMode.Translate)
            {
                if (activePart == 3)
                {
                    var pn = Vector3.Normalize(cam.Position - startWorldPos);
                    planeHitStart = RayPlane(ray, startWorldPos, pn);
                    dragAxis = pn;
                }
                else if (activePart >= 4)
                {
                    dragAxis = axes[activePart - 4 == 0 ? 2 : (activePart - 4 == 1 ? 0 : 1)];
                    planeHitStart = RayPlane(ray, startWorldPos, dragAxis);
                }
                else
                {
                    dragAxis = axes[activePart];
                    startAxisT = ClosestAxisT(ray, startWorldPos, dragAxis);
                }
            }
            else
            {
                dragAxis = activePart == GizmoStyle.PartView
                    ? Vector3.Normalize(cam.Position - startWorldPos) : axes[activePart];
                var hit = RayPlane(ray, startWorldPos, dragAxis);
                var v = hit - startWorldPos;
                v -= dragAxis * Vector3.Dot(v, dragAxis);
                rotStartVec = v.LengthSquared() > 1e-9f ? Vector3.Normalize(v) : Vector3.Zero;
            }
            dragDelta = Vector3.Zero;
            return true;
        }

        private Vector3 dragDelta;

        public void MouseMove(Camera cam, float mx, float my, float vw, float vh)
        {
            if (!Enabled) return;
            if (!Dragging)
            {
                UpdateHot(cam, mx, my, vw, vh);
                return;
            }

            var l = dragLight;
            if (l == null || !scene.Lights.Contains(l)) { MouseUp(); return; }

            var ray = cam.GetPickRay(mx, my, vw, vh);

            if (EffectiveMode == GizmoMode.Translate)
            {
                Vector3 delta;
                if (activePart == 3 || activePart >= 4)
                {
                    var hit = RayPlane(ray, startWorldPos, dragAxis);
                    delta = hit - planeHitStart;
                }
                else
                {
                    float t = ClosestAxisT(ray, startWorldPos, dragAxis);
                    delta = dragAxis * (t - startAxisT);
                }
                if (SnapMode != 0 && !CloneDragActive)
                {
                    if (ResolveSnap_U11(cam, ray, mx, my, vw, vh, out var target, out var note)) { delta = target - startWorldPos; SnapNote = note; }
                    else SnapNote = "nothing under the cursor to snap to";
                }
                dragDelta = delta;
                foreach (var m in dragGroup)
                {
                    m.l.Position = scene.WorldToLightSpace(m.l, m.pos + delta);
                }
                scene.Dirty = true;
            }
            else
            {
                var hit = RayPlane(ray, startWorldPos, dragAxis);
                var v = hit - startWorldPos;
                v -= dragAxis * Vector3.Dot(v, dragAxis);
                if (v.LengthSquared() < 1e-9f || rotStartVec == Vector3.Zero) return;
                v.Normalize();
                float ang = (float)Math.Atan2(Vector3.Dot(Vector3.Cross(rotStartVec, v), dragAxis), Vector3.Dot(rotStartVec, v));

                if (RotateSnapDeg > 0.01f)
                {
                    float step = RotateSnapDeg * 0.0174533f;
                    ang = (float)Math.Round(ang / step) * step;
                }
                lastRotAngle = ang;

                var q = Quaternion.RotationAxis(dragAxis, ang);
                bool group = dragGroup.Count > 1;
                foreach (var m in dragGroup)
                {
                    if (group)
                    {
                        m.l.Position = scene.WorldToLightSpace(m.l, startWorldPos + RotateVec(m.pos - startWorldPos, q));
                    }
                    m.l.Direction = scene.WorldToLightSpaceDir(m.l, Vector3.Normalize(RotateVec(m.dir, q)));
                    m.l.Tangent = scene.WorldToLightSpaceDir(m.l, Vector3.Normalize(RotateVec(m.tan, q)));
                }
                scene.Dirty = true;
            }
        }

        public bool MouseUp()
        {
            SnapNote = "";
            bool cloneDrop = Dragging && CloneDragActive;
            Dragging = false;
            activePart = -1;
            dragLight = null;
            CloneDragActive = false;
            return cloneDrop;
        }

        private static Vector3 RotateVec(Vector3 v, Quaternion q)
        {
            Vector3.Transform(ref v, ref q, out Vector3 r);
            return r;
        }

        private void UpdateHot(Camera cam, float mx, float my, float vw, float vh)
        {
            hotPart = -1;
            hotLight = -1;

            float bestAll = float.MaxValue;
            if (scene.SelectedIndices.Count > 1)
            {
                RefreshAxes_U11(scene.SelectedLight);
                HitTestAt(cam, mx, my, vw, vh, SelectionCentroid(), -2, ref bestAll);
            }
            else
            {
                foreach (var li in scene.SelectedIndices)
                {
                    if (li < 0 || li >= scene.Lights.Count) continue;
                    RefreshAxes_U11(scene.Lights[li]);
                    HitTestAt(cam, mx, my, vw, vh, scene.GetInstance(scene.Lights[li]).WorldPosition, li, ref bestAll);
                }
            }
        }

        private void HitTestAt(Camera cam, float mx, float my, float vw, float vh, Vector3 pos, int lightIndex, ref float best)
        {
            var ray = cam.GetPickRay(mx, my, vw, vh);
            float scale = GetScale(cam, pos);
            float threshold = scale * 0.14f;
            if (GizmoStyle.CW)
            {
                int cwPart = EffectiveMode == GizmoMode.Translate
                    ? GizmoStyle.HitTranslateCW(ray, pos, axes, scale)
                    : GizmoStyle.HitRotateCW(ray, cam.Position, pos, axes, scale, AllRings, true);
                if (cwPart >= 0)
                {
                    float cd = RayPointDistance(ray, pos);
                    if (cd < best) { best = cd; hotPart = cwPart; hotLight = lightIndex; }
                }
                return;
            }

            if (EffectiveMode == GizmoMode.Translate)
            {
                float dc = RayPointDistance(ray, pos);
                if (dc < scale * (GizmoStyle.CentreR + 0.04f) && dc < best) { best = dc; hotPart = 3; hotLight = lightIndex; }

                for (int i = 0; i < 3; i++)
                {
                    float t = ClosestAxisT(ray, pos, axes[i]);
                    if (t < scale * 0.18f || t > scale * 1.15f) continue;
                    var onAxis = pos + axes[i] * t;
                    float d = RayPointDistance(ray, onAxis);
                    if (d < threshold && d < best) { best = d; hotPart = i; hotLight = lightIndex; }
                }

                for (int p = 0; p < 3; p++)
                {
                    var (a, b, n) = PlaneAxes(p);
                    if (Math.Abs(Vector3.Dot(ray.Direction, n)) < 0.02f) continue;
                    var hit = RayPlane(ray, pos, n);
                    var rel = hit - pos;
                    float u = Vector3.Dot(rel, a) / scale;
                    float v = Vector3.Dot(rel, b) / scale;
                    if (u > GizmoStyle.PlaneMin - 0.03f && u < GizmoStyle.PlaneMax + 0.03f &&
                        v > GizmoStyle.PlaneMin - 0.03f && v < GizmoStyle.PlaneMax + 0.03f)
                    {
                        float d = 0;
                        if (d <= best) { best = d; hotPart = 4 + p; hotLight = lightIndex; }
                    }
                }
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    var a = axes[i];
                    if (Math.Abs(Vector3.Dot(ray.Direction, a)) < 0.02f) continue;
                    var hit = RayPlane(ray, pos, a);
                    float rdist = (hit - pos).Length();
                    float d = Math.Abs(rdist - scale);
                    if (d < threshold && d < best) { best = d; hotPart = i; hotLight = lightIndex; }
                }
                if (GizmoStyle.Modern)
                {
                    var vn = Vector3.Normalize(cam.Position - pos);
                    float rdist = (RayPlane(ray, pos, vn) - pos).Length();
                    float d = Math.Abs(rdist - scale * GizmoStyle.ViewRingR);
                    if (d < threshold * 0.8f && d < best) { best = d; hotPart = GizmoStyle.PartView; hotLight = lightIndex; }
                }
            }
        }

        private (Vector3 a, Vector3 b, Vector3 n) PlaneAxes(int p)
        {
            switch (p)
            {
                case 0: return (axes[0], axes[1], axes[2]);
                case 1: return (axes[1], axes[2], axes[0]);
                default: return (axes[0], axes[2], axes[1]);
            }
        }

        private static float ClosestAxisT(Ray ray, Vector3 p0, Vector3 axis)
        {
            var w = p0 - ray.Position;
            float a = Vector3.Dot(axis, axis);
            float b = Vector3.Dot(axis, ray.Direction);
            float c = Vector3.Dot(ray.Direction, ray.Direction);
            float d = Vector3.Dot(axis, w);
            float e = Vector3.Dot(ray.Direction, w);
            float denom = a * c - b * b;
            if (Math.Abs(denom) < 1e-8f) return 0;
            return (b * e - c * d) / denom;
        }

        private static float RayPointDistance(Ray ray, Vector3 p)
        {
            var v = p - ray.Position;
            float t = Vector3.Dot(v, ray.Direction);
            if (t < 0) t = 0;
            return (p - (ray.Position + ray.Direction * t)).Length();
        }

        private static Vector3 RayPlane(Ray ray, Vector3 planePoint, Vector3 planeNormal)
        {
            float denom = Vector3.Dot(ray.Direction, planeNormal);
            if (Math.Abs(denom) < 1e-6f) return planePoint;
            float t = Vector3.Dot(planePoint - ray.Position, planeNormal) / denom;
            return ray.Position + ray.Direction * Math.Max(t, 0);
        }

        public void Draw(LineRenderer lr, TriRenderer tr, Camera cam, float alphaMul = 1.0f)
        {
            if (!Enabled) return;
            this.alphaMul = alphaMul;

            if (scene.SelectedIndices.Count > 1)
            {
                bool isHot = hotLight == -2 || Dragging;
                var centre = Dragging ? DraggedCentroid() : SelectionCentroid();
                var primary = scene.SelectedLight;
                if (!Dragging) RefreshAxes_U11(primary);
                DrawAt(lr, tr, cam, centre,
                    primary != null ? scene.GetInstance(primary).WorldDirection : Vector3.UnitZ, isHot);
            }
            else
            {
                foreach (var li in scene.SelectedIndices)
                {
                    if (li < 0 || li >= scene.Lights.Count) continue;
                    bool isHot = li == hotLight || (Dragging && scene.Lights[li] == dragLight);
                    var l = scene.Lights[li];
                    var inst = scene.GetInstance(l);
                    if (!Dragging) RefreshAxes_U11(l);
                    DrawAt(lr, tr, cam, inst.WorldPosition, inst.WorldDirection, isHot);
                }
            }

            if (Dragging && CloneDragActive && dragLight != null && scene.Lights.Contains(dragLight))
            {
                lr.AddLine(startWorldPos, scene.GetInstance(dragLight).WorldPosition,
                    GizmoStyle.Fade(new Vector4(0.4f, 1f, 0.9f, 0.7f), alphaMul));
            }
        }

        private float alphaMul = 1.0f;

        private Vector3 DraggedCentroid()
        {
            var sum = Vector3.Zero;
            int n = 0;
            foreach (var m in dragGroup)
            {
                if (!scene.Lights.Contains(m.l)) continue;
                sum += scene.GetInstance(m.l).WorldPosition;
                n++;
            }
            return n > 0 ? sum / n : startWorldPos;
        }

        private void DrawAt(LineRenderer lr, TriRenderer tr, Camera cam, Vector3 pos, Vector3 worldDir, bool isHot)
        {
            float scale = GetScale(cam, pos);
            int hp = isHot ? hotPart : -1;
            int ap = (isHot && Dragging) ? activePart : -1;
            bool drag = isHot && Dragging;

            float am = alphaMul;
            if (EffectiveMode == GizmoMode.Translate)
            {
                GizmoStyle.DrawTranslate(tr, cam, pos, axes, scale, hp, ap, drag, am);
                GizmoStyle.AxisLetters(cam, pos, axes, scale, hp, ap, am);
                if (drag && ap >= 0) GizmoStyle.Readout(GizmoStyle.FormatDelta(dragDelta), GizmoStyle.AxisColour(ap < 3 ? ap : 2, false), cam, pos, am);
            }
            else
            {
                GizmoStyle.DrawRotate(tr, cam, pos, axes, scale, hp, ap, drag, am, AllRings, true,
                    dragAxis, drag ? rotStartVec : Vector3.Zero, lastRotAngle, RotateSnapDeg);
                GizmoStyle.AxisLetters(cam, pos, axes, scale, hp, ap, am);
                if (drag && ap >= 0)
                {
                    float r = ap == GizmoStyle.PartView ? scale * GizmoStyle.ViewRingR : scale;
                    var refB = Vector3.Cross(dragAxis, rotStartVec);
                    var mid = rotStartVec * (float)Math.Cos(lastRotAngle * 0.5f) + refB * (float)Math.Sin(lastRotAngle * 0.5f);
                    GizmoStyle.WorldLabel(cam, pos + mid * (r * 1.12f), GizmoStyle.FormatAngle(lastRotAngle), GizmoStyle.Hot, am);
                    GizmoStyle.Readout(GizmoStyle.FormatAngle(lastRotAngle), GizmoStyle.Hot, cam, pos, am);
                }

                var dtip = pos + worldDir * (scale * 1.35f);
                GizmoStyle.Stroke(tr, cam, pos, pos + worldDir * (scale * 1.2f), GizmoStyle.Direction, am, GizmoStyle.StrokePx * 0.75f);
                var (da, db) = Perp(worldDir);
                GizmoStyle.ArrowHead(tr, cam, dtip, pos + worldDir * (scale * 1.2f), da, db, scale * 0.05f, GizmoStyle.Direction, am);
            }
        }

        private static readonly bool[] AllRings = { true, true, true };

        public void DebugPose(Camera cam, int part, bool drag, float amount)
        {
            if (scene.SelectedIndices.Count == 0) return;
            hotPart = part;
            hotLight = scene.SelectedIndices.Count > 1 ? -2 : scene.SelectedIndices[0];
            if (!drag) { dragging = false; activePart = -1; return; }
            var pos = scene.SelectedIndices.Count > 1 ? SelectionCentroid()
                : scene.GetInstance(scene.Lights[Math.Clamp(scene.SelectedIndices[0], 0, scene.Lights.Count - 1)]).WorldPosition;
            dragging = true;
            activePart = part;
            dragLight = scene.SelectedLight;
            startWorldPos = pos;
            if (EffectiveMode == GizmoMode.Rotate)
            {
                dragAxis = part == GizmoStyle.PartView ? Vector3.Normalize(cam.Position - pos) : axes[Math.Clamp(part, 0, 2)];
                var (a, _) = Perp(dragAxis);
                rotStartVec = a;
                lastRotAngle = MathUtil.DegreesToRadians(amount);
            }
            else
            {
                dragAxis = part < 3 ? axes[part] : Vector3.UnitX;
                dragDelta = dragAxis * amount;
            }
        }

        private static (Vector3 a, Vector3 b) Perp(Vector3 n)
        {
            var a = Vector3.Cross(n, Math.Abs(n.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitX);
            if (a.LengthSquared() < 1e-8f) a = Vector3.UnitX; else a.Normalize();
            var b = Vector3.Cross(n, a);
            return (a, b);
        }

        private static Vector4 AxisColour(int axis, bool hot) => GizmoStyle.AxisColour(axis, hot);
    }
}

