using System;
using System.Collections.Generic;
using CodeWalker.GameFiles;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor.Editor
{
    public enum WorldGizmoMode
    {
        Select = 0,
        Translate = 1,
        Rotate = 2,
        Scale = 3,
    }

    public enum WorldGizmoSpace
    {
        World = 0,
        Local = 1,
    }

    public class WorldGizmo
    {
        public WorldGizmoMode Mode = WorldGizmoMode.Translate;
        public WorldGizmoMode EffectiveMode => Mode == WorldGizmoMode.Select ? WorldGizmoMode.Translate : Mode;
        public WorldGizmoSpace Space = WorldGizmoSpace.World;
        public bool Enabled = true;

        public float RotateSnapDeg = 5.0f;
        public float TranslateSnap = 0.0f;
        public float ScaleSnap = 0.0f;

        private bool dragging;
        public bool Dragging { get => dragging; private set => dragging = value; }

        public event Action DragBegan;
        public event Action DragEnded;

        public Action<YmapEntityDef> EntityChanged;
        public Action<IWorldGizmoTarget> TargetChanged;

        public WorldWidgetAxis RotationAxes { get; private set; } = WorldWidgetAxis.XYZ;
        public bool ScaleLockXY { get; private set; } = true;
        public bool CanScale { get; private set; } = true;

        private void Notify(IWorldGizmoTarget t)
        {
            if (t == null) return;
            TargetChanged?.Invoke(t);
            if (t.Key is YmapEntityDef e) EntityChanged?.Invoke(e);
        }

        public bool DragUsedShift { get; private set; }
        public bool DragUsedAlt { get; private set; }

        public bool LocalBasisFromConjugate = false;

        private const int PartX = 0, PartY = 1, PartZ = 2, PartCentre = 3, PartPlane = 4, PartView = GizmoStyle.PartView;

        private Vector3 dragDelta;
        private float dragScaleF = 1f;

        private int hotPart = -1;
        private int activePart = -1;
        private WorldGizmoMode dragMode = WorldGizmoMode.Translate;

        private readonly List<IWorldGizmoTarget> sel = new List<IWorldGizmoTarget>();
        private readonly List<IWorldGizmoTarget> wrapList = new List<IWorldGizmoTarget>();
        private readonly Dictionary<YmapEntityDef, EntityGizmoTarget> wrapCache = new Dictionary<YmapEntityDef, EntityGizmoTarget>();
        private IList<IWorldGizmoTarget> Wrap(IList<YmapEntityDef> ents)
        {
            wrapList.Clear();
            if (ents == null) return wrapList;
            for (int i = 0; i < ents.Count; i++)
            {
                var e = ents[i];
                if (e == null) continue;
                if (!wrapCache.TryGetValue(e, out var t)) { t = new EntityGizmoTarget(e); wrapCache[e] = t; }
                wrapList.Add(t);
            }
            if (wrapCache.Count > 4096) wrapCache.Clear();
            return wrapList;
        }

        private Vector3 pivot;
        private readonly Vector3[] basis = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };

        private struct DragEntry
        {
            public IWorldGizmoTarget Entity;
            public Vector3 Pos;
            public Quaternion Ori;
            public float ScaleX;
            public float ScaleY;
            public float ScaleZ;
        }
        private readonly List<DragEntry> dragGroup = new List<DragEntry>();

        private Vector3 dragAxis;
        private float startAxisT;
        private Vector3 planeHitStart;
        private Vector3 rotStartVec;
        private float lastRotAngle;
        private float startRadius;
        private float dragGizmoScale;

        private Matrix invViewProj;

        private static readonly Vector3[] WorldAxes = { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };

        public float GetScale(Camera cam, Vector3 pos) => GizmoStyle.Scale(cam, pos);

        public void SetSelection(IList<YmapEntityDef> selection) => Prepare(Wrap(selection));
        public void SetSelection(IList<IWorldGizmoTarget> selection) => Prepare(selection);

        private void Prepare(IList<IWorldGizmoTarget> selection)
        {
            if (Dragging) return;

            sel.Clear();
            if (selection != null)
            {
                for (int i = 0; i < selection.Count; i++)
                {
                    var e = selection[i];
                    if (e != null) sel.Add(e);
                }
            }

            if (sel.Count == 0)
            {
                pivot = Vector3.Zero;
                basis[0] = Vector3.UnitX; basis[1] = Vector3.UnitY; basis[2] = Vector3.UnitZ;
                hotPart = -1;
                RotationAxes = WorldWidgetAxis.XYZ; ScaleLockXY = true; CanScale = true;
                return;
            }
            RotationAxes = sel[0].RotationAxes;
            ScaleLockXY = sel[0].ScaleLockXY;
            CanScale = sel[0].CanScale;

            var sum = Vector3.Zero;
            for (int i = 0; i < sel.Count; i++) sum += sel[i].Position;
            pivot = sum / sel.Count;

            SetBasisFrom(sel[0]);
        }

        private void SetBasisFrom(IWorldGizmoTarget primary)
        {
            if (Space == WorldGizmoSpace.World || primary == null)
            {
                basis[0] = Vector3.UnitX; basis[1] = Vector3.UnitY; basis[2] = Vector3.UnitZ;
                return;
            }

            var q = primary.Orientation;
            if (q.LengthSquared() < 1e-8f) q = Quaternion.Identity; else q.Normalize();
            if (LocalBasisFromConjugate) q.Conjugate();

            for (int i = 0; i < 3; i++)
            {
                var a = WorldAxes[i];
                Vector3.Transform(ref a, ref q, out Vector3 r);
                basis[i] = r.LengthSquared() > 1e-8f ? Vector3.Normalize(r) : WorldAxes[i];
            }
        }

        private Vector3 LivePivot()
        {
            if (!Dragging || dragGroup.Count == 0) return pivot;
            var sum = Vector3.Zero;
            int n = 0;
            foreach (var m in dragGroup)
            {
                if (m.Entity == null) continue;
                sum += m.Entity.Position;
                n++;
            }
            return n > 0 ? sum / n : pivot;
        }

        public bool MouseDown(Camera cam, IList<YmapEntityDef> selection, int mx, int my,
            float vw, float vh, bool shift, bool alt) =>
            MouseDown(cam, Wrap(selection), mx, my, vw, vh, shift, alt);

        public bool HitsHandle_V33(Camera cam, IList<IWorldGizmoTarget> selection, int mx, int my, float vw, float vh)
        {
            if (!Enabled || cam == null || Dragging) return false;
            Prepare(selection);
            if (sel.Count == 0) return false;
            PrimeRays(cam);
            this.cam = cam;
            var ray = MakeRay(mx, my, vw, vh);
            UpdateHot(ray, GetScale(cam, pivot));
            return hotPart >= 0;
        }

        public bool MouseDown(Camera cam, IList<IWorldGizmoTarget> selection, int mx, int my,
            float vw, float vh, bool shift, bool alt)
        {
            if (!Enabled || cam == null) return false;
            if (Dragging) return true;

            Prepare(selection);
            if (sel.Count == 0) return false;

            PrimeRays(cam);
            this.cam = cam;
            var ray = MakeRay(mx, my, vw, vh);
            float gscale = GetScale(cam, pivot);
            UpdateHot(ray, gscale);
            if (hotPart < 0) return false;

            DragUsedShift = shift;
            DragUsedAlt = alt;
            activePart = hotPart;
            dragMode = EffectiveMode;
            dragGizmoScale = gscale;
            lastRotAngle = 0;

            dragGroup.Clear();
            foreach (var e in sel)
            {
                dragGroup.Add(new DragEntry
                {
                    Entity = e,
                    Pos = e.Position,
                    Ori = e.Orientation,
                    ScaleX = e.Scale.X,
                    ScaleY = e.Scale.Y,
                    ScaleZ = e.Scale.Z,
                });
            }
            if (dragGroup.Count == 0) return false;

            switch (EffectiveMode)
            {
                case WorldGizmoMode.Translate:
                    if (activePart == PartCentre)
                    {
                        dragAxis = Vector3.Normalize(cam.Position - pivot);
                        planeHitStart = RayPlane(ray, pivot, dragAxis);
                    }
                    else if (activePart >= PartPlane)
                    {
                        dragAxis = PlaneNormal(activePart - PartPlane);
                        planeHitStart = RayPlane(ray, pivot, dragAxis);
                    }
                    else
                    {
                        dragAxis = basis[activePart];
                        startAxisT = ClosestAxisT(ray, pivot, dragAxis);
                    }
                    break;

                case WorldGizmoMode.Rotate:
                    dragAxis = activePart == PartView ? Vector3.Normalize(cam.Position - pivot) : basis[activePart];
                    rotStartVec = RingVector(ray, pivot, dragAxis);
                    break;

                case WorldGizmoMode.Scale:
                    if (activePart == PartCentre)
                    {
                        var pn = Vector3.Normalize(cam.Position - pivot);
                        dragAxis = pn;
                        startRadius = (RayPlane(ray, pivot, pn) - pivot).Length();
                    }
                    else if (activePart >= PartPlane)
                    {
                        dragAxis = PlaneNormal(activePart - PartPlane);
                        planeHitStart = RayPlane(ray, pivot, dragAxis);
                    }
                    else
                    {
                        dragAxis = basis[activePart];
                        startAxisT = ClosestAxisT(ray, pivot, dragAxis);
                    }
                    break;
            }

            dragDelta = Vector3.Zero;
            dragScaleF = 1f;
            Dragging = true;
            DragBegan?.Invoke();
            return true;
        }

        public void MouseMove(Camera cam, int mx, int my, float vw, float vh)
        {
            if (!Enabled || cam == null) return;

            PrimeRays(cam);
            this.cam = cam;

            if (!Dragging)
            {
                if (sel.Count == 0) { hotPart = -1; return; }
                UpdateHot(MakeRay(mx, my, vw, vh), GetScale(cam, pivot));
                return;
            }

            var ray = MakeRay(mx, my, vw, vh);
            switch (dragMode)
            {
                case WorldGizmoMode.Translate: DragTranslate(ray); break;
                case WorldGizmoMode.Rotate: DragRotate(ray); break;
                case WorldGizmoMode.Scale: DragScale(ray); break;
            }
        }

        public void MouseUp()
        {
            if (!Dragging)
            {
                activePart = -1;
                return;
            }
            Dragging = false;
            activePart = -1;
            dragGroup.Clear();
            DragEnded?.Invoke();
        }

        private void DragTranslate(Ray ray)
        {
            Vector3 delta;
            if (activePart == PartCentre || activePart >= PartPlane)
            {
                delta = RayPlane(ray, pivot, dragAxis) - planeHitStart;
                delta = SnapAlongBasis(delta);
            }
            else
            {
                float t = ClosestAxisT(ray, pivot, dragAxis);
                delta = dragAxis * Snap(t - startAxisT, TranslateSnap);
            }

            dragDelta = delta;
            foreach (var m in dragGroup)
            {
                if (m.Entity == null) continue;
                m.Entity.SetPosition(m.Pos + delta);
                Notify(m.Entity);
            }
        }

        private void DragRotate(Ray ray)
        {
            if (rotStartVec == Vector3.Zero) return;
            var v = RingVector(ray, pivot, dragAxis);
            if (v == Vector3.Zero) return;

            float ang = (float)Math.Atan2(Vector3.Dot(Vector3.Cross(rotStartVec, v), dragAxis),
                                          Vector3.Dot(rotStartVec, v));
            if (!DragUsedShift && RotateSnapDeg > 0.01f)
            {
                float step = MathUtil.DegreesToRadians(RotateSnapDeg);
                ang = (float)Math.Round(ang / step) * step;
            }
            lastRotAngle = ang;

            var q = Quaternion.RotationAxis(dragAxis, ang);
            bool orbit = dragGroup.Count > 1 && !DragUsedAlt;

            foreach (var m in dragGroup)
            {
                if (m.Entity == null) continue;
                if (orbit) m.Entity.SetPosition(pivot + RotateVec(m.Pos - pivot, q));

                var ori = m.Ori;
                if (ori.LengthSquared() < 1e-8f) ori = Quaternion.Identity;
                m.Entity.SetOrientation(Quaternion.Normalize(Quaternion.Multiply(q, ori)));
                Notify(m.Entity);
            }
        }

        private void DragScale(Ray ray)
        {
            float f;
            if (activePart == PartCentre)
            {
                float r = (RayPlane(ray, pivot, dragAxis) - pivot).Length();
                f = 1.0f + (r - startRadius) / Math.Max(dragGizmoScale, 1e-4f);
            }
            else if (activePart >= PartPlane)
            {
                var (pa, pb, _) = PlaneAxes(activePart - PartPlane);
                var diag = Vector3.Normalize(pa + pb);
                float d = Vector3.Dot(RayPlane(ray, pivot, dragAxis) - planeHitStart, diag);
                f = 1.0f + d / Math.Max(dragGizmoScale, 1e-4f);
            }
            else
            {
                float t = ClosestAxisT(ray, pivot, dragAxis);
                f = 1.0f + (t - startAxisT) / Math.Max(dragGizmoScale, 1e-4f);
            }
            if (f < 0.001f) f = 0.001f;

            int plane = activePart >= PartPlane ? activePart - PartPlane : -1;
            bool doX = activePart == PartX || activePart == PartCentre || (ScaleLockXY && activePart == PartY) || plane == 0 || plane == 2;
            bool doY = activePart == PartY || activePart == PartCentre || (ScaleLockXY && activePart == PartX) || plane == 0 || plane == 1;
            bool doZ = activePart == PartZ || activePart == PartCentre || plane == 1 || plane == 2;
            dragScaleF = f;

            float fx = doX ? f : 1.0f;
            float fy = doY ? f : 1.0f;
            float fz = doZ ? f : 1.0f;
            bool orbit = dragGroup.Count > 1 && !DragUsedAlt;

            foreach (var m in dragGroup)
            {
                if (m.Entity == null) continue;
                if (!m.Entity.CanScale) continue;

                float x = doX ? ClampScale(SnapScale(m.ScaleX * f)) : m.ScaleX;
                float y = doY ? ClampScale(SnapScale(m.ScaleY * f)) : m.ScaleY;
                float z = doZ ? ClampScale(SnapScale(m.ScaleZ * f)) : m.ScaleZ;
                if (ScaleLockXY) y = x;
                m.Entity.SetScale(new Vector3(x, y, z));

                if (orbit)
                {
                    var rel = m.Pos - pivot;
                    var scaled = basis[0] * (Vector3.Dot(rel, basis[0]) * fx)
                               + basis[1] * (Vector3.Dot(rel, basis[1]) * fy)
                               + basis[2] * (Vector3.Dot(rel, basis[2]) * fz);
                    m.Entity.SetPosition(pivot + scaled);
                }
                Notify(m.Entity);
            }
        }

        private static float ClampScale(float s) => s < 0.001f ? 0.001f : s;

        private float SnapScale(float s)
        {
            if (DragUsedShift || ScaleSnap <= 1e-5f) return s;
            return (float)Math.Round(s / ScaleSnap) * ScaleSnap;
        }

        private float Snap(float v, float step)
        {
            if (DragUsedShift || step <= 1e-5f) return v;
            return (float)Math.Round(v / step) * step;
        }

        private Vector3 SnapAlongBasis(Vector3 delta)
        {
            if (DragUsedShift || TranslateSnap <= 1e-5f) return delta;
            return basis[0] * Snap(Vector3.Dot(delta, basis[0]), TranslateSnap)
                 + basis[1] * Snap(Vector3.Dot(delta, basis[1]), TranslateSnap)
                 + basis[2] * Snap(Vector3.Dot(delta, basis[2]), TranslateSnap);
        }

        private static Vector3 RotateVec(Vector3 v, Quaternion q)
        {
            Vector3.Transform(ref v, ref q, out Vector3 r);
            return r;
        }

        private static Vector3 RingVector(Ray ray, Vector3 centre, Vector3 axis)
        {
            var v = RayPlane(ray, centre, axis) - centre;
            v -= axis * Vector3.Dot(v, axis);
            return v.LengthSquared() > 1e-9f ? Vector3.Normalize(v) : Vector3.Zero;
        }

        private void PrimeRays(Camera cam)
        {
            invViewProj = cam.ViewProjMatrix;
            invViewProj.Invert();
        }

        private Ray MakeRay(float sx, float sy, float vw, float vh)
        {
            float nx = (2.0f * sx / Math.Max(vw, 1)) - 1.0f;
            float ny = 1.0f - (2.0f * sy / Math.Max(vh, 1));
            var np = Vector3.TransformCoordinate(new Vector3(nx, ny, 1.0f), invViewProj);
            var fp = Vector3.TransformCoordinate(new Vector3(nx, ny, 0.0f), invViewProj);
            var d = fp - np;
            if (d.LengthSquared() < 1e-12f) d = Vector3.UnitZ;
            return new Ray(np, Vector3.Normalize(d));
        }

        private void UpdateHot(Ray ray, float scale)
        {
            hotPart = -1;
            if (sel.Count == 0) return;

            if (GizmoStyle.CW)
            {
                if (EffectiveMode == WorldGizmoMode.Rotate)
                {
                    for (int i = 0; i < 3; i++) ringMask[i] = RingEnabled(i);
                    hotPart = GizmoStyle.HitRotateCW(ray, cam?.Position ?? ray.Position, pivot, basis, scale, ringMask, RotationAxes == WorldWidgetAxis.XYZ);
                }
                else if (EffectiveMode == WorldGizmoMode.Scale) { if (CanScale) hotPart = GizmoStyle.HitScaleCW(ray, pivot, basis, scale, ScaleLockXY); }
                else hotPart = GizmoStyle.HitTranslateCW(ray, pivot, basis, scale);
                return;
            }

            float best = float.MaxValue;
            float threshold = scale * 0.14f;

            if (EffectiveMode == WorldGizmoMode.Rotate)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (!RingEnabled(i)) continue;
                    var a = basis[i];
                    if (Math.Abs(Vector3.Dot(ray.Direction, a)) < 0.02f) continue;
                    float rdist = (RayPlane(ray, pivot, a) - pivot).Length();
                    float d = Math.Abs(rdist - scale);
                    if (d < threshold && d < best) { best = d; hotPart = i; }
                }
                if (GizmoStyle.Modern && RotationAxes == WorldWidgetAxis.XYZ && cam != null)
                {
                    var vn = Vector3.Normalize(cam.Position - pivot);
                    float rdist = (RayPlane(ray, pivot, vn) - pivot).Length();
                    float d = Math.Abs(rdist - scale * GizmoStyle.ViewRingR);
                    if (d < threshold * 0.8f && d < best) { best = d; hotPart = PartView; }
                }
                return;
            }

            if (EffectiveMode == WorldGizmoMode.Scale && !CanScale) return;

            float dc = RayPointDistance(ray, pivot);
            if (dc < scale * (GizmoStyle.CentreR + 0.04f) && dc < best) { best = dc; hotPart = PartCentre; }

            for (int i = 0; i < 3; i++)
            {
                float t = ClosestAxisT(ray, pivot, basis[i]);
                if (t < scale * 0.18f || t > scale * 1.15f) continue;
                float d = RayPointDistance(ray, pivot + basis[i] * t);
                if (d < threshold && d < best) { best = d; hotPart = i; }
            }

            bool scaleMode = EffectiveMode == WorldGizmoMode.Scale;
            for (int p = 0; p < 3; p++)
            {
                if (scaleMode && ScaleLockXY && p != 0) continue;
                var (a, b, n) = PlaneAxes(p);
                if (Math.Abs(Vector3.Dot(ray.Direction, n)) < 0.02f) continue;
                var rel = RayPlane(ray, pivot, n) - pivot;
                float u = Vector3.Dot(rel, a) / scale;
                float v = Vector3.Dot(rel, b) / scale;
                if (u > GizmoStyle.PlaneMin - 0.03f && u < GizmoStyle.PlaneMax + 0.03f &&
                    v > GizmoStyle.PlaneMin - 0.03f && v < GizmoStyle.PlaneMax + 0.03f)
                {
                    best = 0;
                    hotPart = PartPlane + p;
                }
            }
        }

        private bool RingEnabled(int i) =>
            RotationAxes == WorldWidgetAxis.XYZ || (RotationAxes == WorldWidgetAxis.Z && i == 2);

        private (Vector3 a, Vector3 b, Vector3 n) PlaneAxes(int p)
        {
            switch (p)
            {
                case 0: return (basis[0], basis[1], basis[2]);
                case 1: return (basis[1], basis[2], basis[0]);
                default: return (basis[0], basis[2], basis[1]);
            }
        }

        private Vector3 PlaneNormal(int p) => p == 0 ? basis[2] : (p == 1 ? basis[0] : basis[1]);

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

        public void Draw(LineRenderer lr, TriRenderer tr, Camera cam, IList<YmapEntityDef> selection, float alphaMul = 1.0f) =>
            Draw(lr, tr, cam, Wrap(selection), alphaMul);

        public void Draw(LineRenderer lr, TriRenderer tr, Camera cam, IList<IWorldGizmoTarget> selection, float alphaMul = 1.0f)
        {
            if (!Enabled || cam == null) return;

            Prepare(selection);
            if (sel.Count == 0 && !Dragging) return;
            this.cam = cam;
            this.alphaMul = alphaMul;

            var pos = LivePivot();
            float scale = GetScale(cam, pos);
            int hp = hotPart;
            int ap = Dragging ? activePart : -1;

            switch (Dragging ? dragMode : EffectiveMode)
            {
                case WorldGizmoMode.Translate: DrawTranslate(lr, tr, pos, scale, hp, ap); break;
                case WorldGizmoMode.Rotate: DrawRotate(lr, tr, pos, scale, hp, ap); break;
                case WorldGizmoMode.Scale: if (CanScale) DrawScale(lr, tr, pos, scale, hp, ap); break;
            }

            if (sel.Count > 1)
            {
                var spoke = GizmoStyle.Fade(new Vector4(0.9f, 0.9f, 0.35f, 0.35f), alphaMul);
                foreach (var e in sel) lr.AddLine(pos, e.Position, spoke);
            }
        }

        private Camera cam;
        private float alphaMul = 1.0f;

        private void DrawTranslate(LineRenderer lr, TriRenderer tr, Vector3 pos, float scale, int hp, int ap)
        {
            float am = alphaMul;
            GizmoStyle.DrawTranslate(tr, cam, pos, basis, scale, hp, ap, Dragging, am);
            GizmoStyle.AxisLetters(cam, pos, basis, scale, hp, ap, am);
            if (Dragging && ap >= 0)
                GizmoStyle.Readout(GizmoStyle.FormatDelta(dragDelta), GizmoStyle.AxisColour(ap < 3 ? ap : 2, false), cam, pos, am);
        }

        private void DrawRotate(LineRenderer lr, TriRenderer tr, Vector3 pos, float scale, int hp, int ap)
        {
            float am = alphaMul;
            var rings = ringMask;
            for (int i = 0; i < 3; i++) rings[i] = RingEnabled(i);
            GizmoStyle.DrawRotate(tr, cam, pos, basis, scale, hp, ap, Dragging, am, rings,
                RotationAxes == WorldWidgetAxis.XYZ, dragAxis, Dragging ? rotStartVec : Vector3.Zero, lastRotAngle,
                DragUsedShift ? 0f : RotateSnapDeg);
            GizmoStyle.AxisLetters(cam, pos, basis, scale, hp, ap, am, rings);
            if (Dragging && ap >= 0 && rotStartVec != Vector3.Zero)
            {
                float r = ap == PartView ? scale * GizmoStyle.ViewRingR : scale;
                var refB = Vector3.Cross(dragAxis, rotStartVec);
                var mid = rotStartVec * (float)Math.Cos(lastRotAngle * 0.5f) + refB * (float)Math.Sin(lastRotAngle * 0.5f);
                GizmoStyle.WorldLabel(cam, pos + mid * (r * 1.12f), GizmoStyle.FormatAngle(lastRotAngle), GizmoStyle.Hot, am);
                GizmoStyle.Readout(GizmoStyle.FormatAngle(lastRotAngle), GizmoStyle.Hot, cam, pos, am);
            }

            if (GizmoStyle.CW) return;
            var front = sel.Count > 0 ? LocalForward(sel[0]) : Vector3.UnitX;
            var tipf = pos + front * (scale * 1.35f);
            var neck = pos + front * (scale * 1.2f);
            GizmoStyle.Stroke(tr, cam, pos, neck, GizmoStyle.Direction, am, GizmoStyle.StrokePx * 0.75f);
            var pa = Vector3.Cross(front, Math.Abs(front.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitX);
            if (pa.LengthSquared() < 1e-8f) pa = Vector3.UnitX; else pa.Normalize();
            var pb = Vector3.Cross(front, pa);
            GizmoStyle.ArrowHead(tr, cam, tipf, neck, pa, pb, scale * 0.05f, GizmoStyle.Direction, am);
        }
        private readonly bool[] ringMask = new bool[3];

        private void DrawScale(LineRenderer lr, TriRenderer tr, Vector3 pos, float scale, int hp, int ap)
        {
            float am = alphaMul;
            GizmoStyle.DrawScale(tr, cam, pos, basis, scale, hp, ap, Dragging, am, ScaleLockXY, true);
            GizmoStyle.AxisLetters(cam, pos, basis, scale, hp, ap, am);
            if (Dragging && ap >= 0) GizmoStyle.Readout(GizmoStyle.FormatScale(dragScaleF), GizmoStyle.Hot, cam, pos, am);
        }

        public void DebugPose(Camera cam, int part, bool drag, float amount)
        {
            if (sel.Count == 0 || cam == null) return;
            this.cam = cam;
            hotPart = part;
            if (!drag) { dragging = false; activePart = -1; return; }
            dragging = true;
            activePart = part;
            dragMode = EffectiveMode;
            dragGizmoScale = GetScale(cam, pivot);
            switch (dragMode)
            {
                case WorldGizmoMode.Rotate:
                    dragAxis = part == PartView ? Vector3.Normalize(cam.Position - pivot) : basis[Math.Clamp(part, 0, 2)];
                    {
                        var a = Vector3.Cross(dragAxis, Math.Abs(dragAxis.Z) < 0.9f ? Vector3.UnitZ : Vector3.UnitX);
                        rotStartVec = a.LengthSquared() > 1e-8f ? Vector3.Normalize(a) : Vector3.UnitX;
                    }
                    lastRotAngle = MathUtil.DegreesToRadians(amount);
                    break;
                case WorldGizmoMode.Scale:
                    dragScaleF = amount > 0 ? amount : 1f;
                    break;
                default:
                    dragAxis = part < 3 ? basis[part] : basis[0];
                    dragDelta = dragAxis * amount;
                    break;
            }
        }

        private Vector3 LocalForward(IWorldGizmoTarget e)
        {
            if (e == null) return Vector3.UnitX;
            var q = e.Orientation;
            if (q.LengthSquared() < 1e-8f) return Vector3.UnitX;
            q.Normalize();
            if (LocalBasisFromConjugate) q.Conjugate();
            var a = Vector3.UnitX;
            Vector3.Transform(ref a, ref q, out Vector3 r);
            return r.LengthSquared() > 1e-8f ? Vector3.Normalize(r) : Vector3.UnitX;
        }

        private static Vector4 AxisColour(int axis, bool hot) => GizmoStyle.AxisColour(axis, hot);
    }
}

