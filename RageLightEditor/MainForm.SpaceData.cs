using System;
using System.Collections.Generic;
using CodeWalker;
using CodeWalker.GameFiles;
using CodeWalker.World;
using RageLightEditor.Editor;
using RageLightEditor.Rendering;
using SharpDX;
using SharpDX.Direct3D11;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private SpaceData spaceData;
        private PathBatchRenderer pathBatch;
        private readonly List<YndFile> sdYnds = new List<YndFile>();
        private readonly List<YnvFile> sdYnvs = new List<YnvFile>();
        private readonly List<BasePathData> sdBatch = new List<BasePathData>();
        private string sdLastStatus = "";
        private object sdDirtyKey;
        private ScenarioRegion sdSelEdgeRegion;

        private SpaceData SpaceDataOrNull => spaceData ??= (gameFiles != null ? new SpaceData(gameFiles) : null);

        private float SdRange => panel?.SpaceDataRange ?? 1000.0f;
        private float SdNavRange => panel?.SpaceNavRange ?? 450.0f;

        partial void OnAfterWorldDraw_SpaceData(DeviceContext context)
        {
            if (panel == null || !panel.WorldMode || !worldBuilt) return;
            var sd = SpaceDataOrNull;
            if (sd == null) return;
            pathBatch ??= new PathBatchRenderer(deviceResources.Device);

            var mode = SelMode;
            var camPos = camera.Position;
            bool paths = panel.ShowPaths || mode == WorldSelectionMode.Path;
            bool navs = (panel.ShowNavMeshes || mode == WorldSelectionMode.NavMesh) && !panel.NavMode;
            bool trains = panel.ShowTrainTracks || mode == WorldSelectionMode.TrainTrack;
            bool scenarios = panel.ShowScenarios || mode == WorldSelectionMode.Scenario;
            bool audio = panel.ShowAudioZones || mode == WorldSelectionMode.Audio;
            bool hmap = panel.ShowHeightmap || mode == WorldSelectionMode.Heightmap
                        || Environment.GetEnvironmentVariable("RLE_HMAP") == "1";

            if (sdDirtyKey != null && !worldGizmo.Dragging) { SpaceRefreshAfterEdit(sdDirtyKey); sdDirtyKey = null; }
            if (panel.SpaceEditKey != null)
            {
                var key = panel.SpaceEditKey; var before = panel.SpaceEditBefore; var after = panel.SpaceEditAfter;
                var name = panel.SpaceEditName ?? "Move";
                panel.SpaceEditKey = null;
                SpaceRefreshAfterEdit(key);
                WorldHistory.Push(new DelegateCommand(name,
                    doIt: () => { SpaceSetPosition(key, after); SpaceRefreshAfterEdit(key); },
                    undoIt: () => { SpaceSetPosition(key, before); SpaceRefreshAfterEdit(key); }));
            }

            {
                var s = WorldEdit.Selection;
                object selNode = s.ScenarioNode, selEdge = s.ScenarioEdge;
                if (!ReferenceEquals(selNode, pathBatch.ScenarioSelectedNode) || !ReferenceEquals(selEdge, pathBatch.ScenarioSelectedEdge))
                {
                    pathBatch.Invalidate((pathBatch.ScenarioSelectedNode as ScenarioNode)?.Ymt?.ScenarioRegion);
                    pathBatch.Invalidate(sdSelEdgeRegion);
                    pathBatch.ScenarioSelectedNode = selNode;
                    pathBatch.ScenarioSelectedEdge = selEdge;
                    sdSelEdgeRegion = (selEdge as MCScenarioChainingEdge)?.Region?.Ymt?.ScenarioRegion ?? s.ScenarioNode?.Ymt?.ScenarioRegion;
                    pathBatch.Invalidate((selNode as ScenarioNode)?.Ymt?.ScenarioRegion);
                    pathBatch.Invalidate(sdSelEdgeRegion);
                }
            }

            sdBatch.Clear();
            sdNavBatch_U28.Clear();
            if (paths)
            {
                sd.EnsurePaths();
                if (sd.PathsReady)
                {
                    sd.GetYndsNear(camPos, SdRange, sdYnds);
                    foreach (var y in sdYnds) sdBatch.Add(y);
                }
            }
            if (trains)
            {
                sd.EnsureTrains();
                if (sd.TrainsReady && sd.Trains?.TrainTracks != null)
                    foreach (var t in sd.Trains.TrainTracks) if (t != null) sdBatch.Add(t);
            }
            if (scenarios)
            {
                sd.EnsureScenarios();
                if (sd.ScenariosReady && sd.Scenarios?.ScenarioRegions != null)
                {
                    foreach (var ymt in sd.Scenarios.ScenarioRegions)
                    {
                        var sr = ymt?.ScenarioRegion;
                        if (sr == null) continue;
                        if (sr.BVH != null && !BoxNear(sr.BVH.Box, camPos, SdRange)) continue;
                        sdBatch.Add(sr);
                    }
                }
            }
            if (navs)
            {
                sd.EnsureNav();
                if (sd.NavReady)
                {
                    sd.GetYnvsNear(camPos, SdNavRange, sdYnvs);
                    foreach (var y in sdYnvs) sdNavBatch_U28.Add(y);
                }
            }
            if (hmap)
            {
                sd.EnsureHeightmap();
                var surf = HeightmapSurfaceOrNull_V21(sd);
                if (surf != null) sdBatch.Add(surf);
            }
            if (sdBatch.Count > 0)
                pathBatch.Draw(context, camera.ViewProjMatrix, camPos, sdBatch);
            navOverlayPending_U28 = sdNavBatch_U28.Count > 0;
            if (!navOverlayPending_U28) pathBatch.EndFrame();

            if (panel.ShowSelectionHelpers)
            {
                var s = WorldEdit.Selection;
                if (mode == WorldSelectionMode.Path && s.PathNode != null) DrawPathNodeLinkBoxes(s.PathNode, s.PathLink);
                if (mode == WorldSelectionMode.TrainTrack && s.TrainTrackNode != null) DrawTrainNodeLinkBoxes(s.TrainTrackNode);
                if (mode == WorldSelectionMode.Scenario && s.ScenarioNode != null) DrawScenarioClusterBoxes(s.ScenarioNode);
            }

            if (audio)
            {
                sd.EnsureAudio();
                if (sd.AudioReady) DrawAudioZoneHelpers(camPos);
            }

            if (sd.Busy || sd.Status != sdLastStatus)
            {
                if (sd.Status != sdLastStatus) { sdLastStatus = sd.Status; if (!string.IsNullOrEmpty(sdLastStatus)) WorldEdit.LastStatus = sdLastStatus; }
            }
            panel.SpaceDataStatus = sd.Busy ? sd.Status : "";
        }

        private static bool BoxNear(BoundingBox b, Vector3 p, float range)
        {
            float dx = Math.Max(Math.Max(b.Minimum.X - p.X, 0.0f), p.X - b.Maximum.X);
            float dy = Math.Max(Math.Max(b.Minimum.Y - p.Y, 0.0f), p.Y - b.Maximum.Y);
            return dx * dx + dy * dy <= range * range;
        }

        private void DrawPathNodeLinkBoxes(YndNode n, YndLink selLink)
        {
            if (n?.Links == null) return;
            const float linkrad = 0.25f;
            foreach (var ln in n.Links)
            {
                if (ln?.Node2 == null) continue;
                Vector3 dv = n.Position - ln.Node2.Position;
                float dl = dv.Length();
                if (dl < 1e-4f) continue;
                Vector3 dir = dv * (1.0f / dl);
                int lanestot = ln.LaneCountForward + ln.LaneCountBackward;
                float lanewidth = ln.GetLaneWidth();
                float inner = ln.LaneOffset * lanewidth;
                float outer = inner + Math.Max(lanewidth * ln.LaneCountForward, 0.5f);
                float totwidth = lanestot * lanewidth;
                float halfwidth = totwidth * 0.5f;
                if (ln.LaneCountBackward == 0) { inner -= halfwidth; outer -= halfwidth; }
                if (ln.LaneCountForward == 0) { inner += halfwidth; outer += halfwidth; }
                var ori = Quaternion.Invert(Quaternion.RotationLookAtRH(dir, Vector3.UnitZ));
                DrawOrientedBox(n.Position, ori, new Vector3(-linkrad - outer, -linkrad, 0.0f), new Vector3(linkrad - inner, linkrad, dl),
                                ReferenceEquals(ln, selLink) ? HelperCyan : HelperBlue);
            }
        }

        private void DrawTrainNodeLinkBoxes(TrainTrackNode n)
        {
            if (n?.Links == null) return;
            const float linkrad = 0.25f;
            foreach (var ln in n.Links)
            {
                if (ln == null) continue;
                Vector3 dv = n.Position - ln.Position;
                float dl = dv.Length();
                if (dl < 1e-4f) continue;
                Vector3 dir = dv * (1.0f / dl);
                var ori = Quaternion.Invert(Quaternion.RotationLookAtRH(dir, Vector3.UnitZ));
                DrawOrientedBox(n.Position, ori, new Vector3(-linkrad, -linkrad, 0.0f), new Vector3(linkrad, linkrad, dl), HelperBlue);
            }
        }

        private void DrawScenarioClusterBoxes(ScenarioNode n)
        {
            var sr = n.Ymt?.ScenarioRegion;
            if (sr?.BVH != null) DrawOrientedBox(Vector3.Zero, Quaternion.Identity, sr.BVH.Box.Minimum, sr.BVH.Box.Maximum, HelperBlue);
            var ncl = n.Cluster;
            if (ncl == null) return;
            DrawOrientedBox(ncl.Position, Quaternion.Identity, new Vector3(-0.5f), new Vector3(0.5f), HelperCyan);
            if (ncl.Points?.MyPoints != null)
            {
                foreach (var clpoint in ncl.Points.MyPoints)
                {
                    if (clpoint == null || clpoint == n.ClusterMyPoint) continue;
                    DrawOrientedBox(clpoint.Position, clpoint.Orientation, new Vector3(-0.5f), new Vector3(0.5f), HelperBlue);
                }
            }
        }

        private void DrawAudioZoneHelpers(Vector3 camPos)
        {
            var sd = spaceData;
            if (sd?.AudioPlacements == null) return;
            float range = SdRange;
            bool outer = panel.ShowAudioOuterBounds;
            int drawn = 0;
            foreach (var placement in sd.AudioPlacements)
            {
                if (placement == null) continue;
                if ((placement.Position - camPos).Length() > range) continue;
                if (drawn++ > SelMaxHelperBoxes) break;
                switch (placement.Shape)
                {
                    case Dat151ZoneShape.Box:
                    case Dat151ZoneShape.Line:
                        DrawOrientedBox(placement.InnerPos, placement.InnerOri, placement.InnerMin, placement.InnerMax, HelperCyan);
                        if (outer) DrawOrientedBox(placement.OuterPos, placement.OuterOri, placement.OuterMin, placement.OuterMax, HelperBlue);
                        break;
                    case Dat151ZoneShape.Sphere:
                        if (placement.InnerPos != Vector3.Zero && placement.OuterPos != Vector3.Zero)
                        {
                            lineRenderer.AddSphere(placement.InnerPos, placement.InnerRadius, HelperCyan, 24);
                            if (outer) lineRenderer.AddSphere(placement.OuterPos, placement.OuterRadius, HelperBlue, 24);
                        }
                        break;
                }
            }
        }

        partial void DrawSelection_SpaceData(in WorldSelection s, Vector4 col, bool full, ref Vector3 pos, ref Quaternion ori,
                                              ref Vector3 bbmin, ref Vector3 bbmax, ref bool drawBox)
        {
            if (s.PathNode != null)
            {
                pos = s.PathNode.Position; ori = Quaternion.Identity; bbmin = new Vector3(-0.5f); bbmax = new Vector3(0.5f);
            }
            if (s.TrainTrackNode != null)
            {
                pos = s.TrainTrackNode.Position; ori = Quaternion.Identity; bbmin = new Vector3(-0.5f); bbmax = new Vector3(0.5f);
            }
            if (s.ScenarioNode != null)
            {
                var sn = s.ScenarioNode;
                pos = sn.Position; ori = sn.Orientation; bbmin = new Vector3(-0.5f); bbmax = new Vector3(0.5f);
                DrawArrowOutline(sn.Position, Vector3.UnitY, Vector3.UnitZ, ori, 2.0f, 0.25f, col, fill: full);
            }
            if (s.ScenarioEdge != null)
            {
                var se = s.ScenarioEdge;
                var sn1 = se.NodeFrom; var sn2 = se.NodeTo;
                if (sn1 != null && sn2 != null)
                {
                    var dirp = sn2.Position - sn1.Position;
                    float dl = dirp.Length();
                    if (dl > 1e-4f)
                    {
                        Vector3 dir = dirp * (1.0f / dl);
                        var aori = Quaternion.Invert(Quaternion.RotationLookAtRH(dir, Vector3.UnitZ));
                        float arrowrad = 0.25f;
                        float arrowlen = Math.Max(dl - arrowrad * 5.0f, 0);
                        DrawArrowOutline(sn1.Position, -Vector3.UnitZ, Vector3.UnitY, aori, arrowlen, arrowrad, full ? SelColour : PortalBlue, fill: full);
                    }
                }
            }
            if (s.NavPoly != null)
            {
                QueueNavPolyFill_U28(s.NavPoly);
                DrawNavPolyOutline(s.NavPoly, col);
                drawBox = false;
                return;
            }
            if (s.NavPoint != null)
            {
                var navp = s.NavPoint;
                pos = navp.Position; ori = navp.Orientation; bbmin = new Vector3(-0.5f); bbmax = new Vector3(0.5f);
                DrawArrowOutline(navp.Position, -Vector3.UnitY, Vector3.UnitZ, ori, 2.0f, 0.25f, col);
            }
            if (s.NavPortal != null)
            {
                var navp = s.NavPortal;
                pos = navp.Position; ori = navp.Orientation; bbmin = new Vector3(-0.5f); bbmax = new Vector3(0.5f);
                DrawArrowOutline(navp.Position, Vector3.UnitY, Vector3.UnitZ, ori, 2.0f, 0.25f, col);
                lineRenderer.AddLine(navp.PositionFrom, navp.PositionTo, col);
            }
            if (s.Audio != null)
            {
                var au = s.Audio;
                pos = au.Position; ori = au.Orientation; bbmin = au.HitboxMin; bbmax = au.HitboxMax;
                if (au.Shape == Dat151ZoneShape.Sphere)
                {
                    float r = s.BSphere.Radius > 0 ? s.BSphere.Radius : au.HitSphereRad;
                    lineRenderer.AddSphere(au.Position, r, col, 32);
                    if (full && au.OuterRadius > 0) lineRenderer.AddSphere(au.OuterPos, au.OuterRadius, SelWhite, 32);
                    drawBox = false;
                }
                else if (full)
                {
                    DrawOrientedBox(au.OuterPos, au.OuterOri, au.OuterMin, au.OuterMax, SelWhite);
                }
            }
        }

        private void DrawNavPolyOutline(YnvPoly poly, Vector4 col)
        {
            var ynv = poly?.Ynv;
            if (ynv?.Vertices == null || ynv.Indices == null) return;
            int ic = poly._RawData.IndexCount;
            int startid = poly._RawData.IndexID;
            int endid = startid + ic;
            int vc = ynv.Vertices.Count;
            if (startid >= ynv.Indices.Count || endid > ynv.Indices.Count) return;
            Vector3 first = Vector3.Zero, prev = Vector3.Zero; bool have = false;
            for (int id = startid; id < endid; id++)
            {
                int ind = ynv.Indices[id];
                if (ind >= vc) continue;
                var v = ynv.Vertices[ind];
                if (!have) { first = v; have = true; }
                else lineRenderer.AddLine(prev, v, col);
                prev = v;
            }
            if (have) lineRenderer.AddLine(prev, first, col);
        }

        private void PickPaths(ref Ray ray, Vector3 camPos, ref WorldSelection hit)
        {
            var sd = SpaceDataOrNull; if (sd == null) return;
            sd.EnsurePaths();
            if (!sd.PathsReady) { WorldEdit.LastStatus = sd.Status.Length > 0 ? sd.Status : "loading paths..."; return; }
            foreach (var ynd in sd.AllYnds.Values)
            {
                if (ynd?.BVH == null) continue;
                PickPathBvh(ynd.BVH, ref ray, camPos, ref hit);
            }
        }

        private void PickTrainTracks(ref Ray ray, Vector3 camPos, ref WorldSelection hit)
        {
            var sd = SpaceDataOrNull; if (sd == null) return;
            sd.EnsureTrains();
            if (!sd.TrainsReady) { WorldEdit.LastStatus = sd.Status.Length > 0 ? sd.Status : "loading train tracks..."; return; }
            foreach (var track in sd.Trains.TrainTracks)
            {
                if (track?.BVH == null) continue;
                PickPathBvh(track.BVH, ref ray, camPos, ref hit);
            }
        }

        private void PickScenarios(ref Ray ray, Vector3 camPos, ref WorldSelection hit)
        {
            var sd = SpaceDataOrNull; if (sd == null) return;
            sd.EnsureScenarios();
            if (!sd.ScenariosReady) { WorldEdit.LastStatus = sd.Status.Length > 0 ? sd.Status : "loading scenarios..."; return; }
            foreach (var ymt in sd.Scenarios.ScenarioRegions)
            {
                var sr = ymt?.ScenarioRegion;
                if (sr?.BVH == null) continue;
                PickPathBvh(sr.BVH, ref ray, camPos, ref hit);
            }
        }

        private void PickNavMeshes(ref Ray ray, Vector3 camPos, ref WorldSelection hit)
        {
            var sd = SpaceDataOrNull; if (sd == null) return;
            sd.EnsureNav();
            if (!sd.NavReady) { WorldEdit.LastStatus = sd.Status.Length > 0 ? sd.Status : "loading nav meshes..."; return; }
            sd.GetYnvsNear(camPos, SdNavRange, sdYnvs);
            if (sdYnvs.Count == 0) { WorldEdit.LastStatus = "nav meshes: none loaded here yet"; return; }
            foreach (var ynv in sdYnvs)
            {
                if (ynv == null) continue;
                if (ynv.BVH != null) PickPathBvh(ynv.BVH, ref ray, camPos, ref hit);
                if (ynv.Nav != null && ynv.Vertices != null && ynv.Indices != null && ynv.Polys != null)
                    PickNavSector(ynv, ynv.Nav.SectorTree, ynv.Nav.SectorTree, ref ray, camPos, ref hit);
            }
        }

        private void PickNavSector(YnvFile ynv, NavMeshSector navsector, NavMeshSector rootsec, ref Ray mray, Vector3 camPos, ref WorldSelection hit)
        {
            if (navsector == null) return;
            var bbox = new BoundingBox(navsector.AABBMin.XYZ(), navsector.AABBMax.XYZ());
            if (rootsec != null)
            {
                bbox.Minimum.Z = rootsec.AABBMin.Z;
                bbox.Maximum.Z = rootsec.AABBMax.Z;
            }
            if (!mray.Intersects(ref bbox, out float fhd)) return;
            if (navsector.SubTree1 != null) PickNavSector(ynv, navsector.SubTree1, rootsec, ref mray, camPos, ref hit);
            if (navsector.SubTree2 != null) PickNavSector(ynv, navsector.SubTree2, rootsec, ref mray, camPos, ref hit);
            if (navsector.SubTree3 != null) PickNavSector(ynv, navsector.SubTree3, rootsec, ref mray, camPos, ref hit);
            if (navsector.SubTree4 != null) PickNavSector(ynv, navsector.SubTree4, rootsec, ref mray, camPos, ref hit);
            var polyids = navsector.Data?.PolyIDs;
            if (polyids == null) return;
            var polys = ynv.Polys;
            int vc = ynv.Vertices.Count;
            for (int i = 0; i < polyids.Length; i++)
            {
                int polyid = polyids[i];
                if (polyid >= polys.Count) continue;
                var poly = polys[polyid];
                int ic = poly._RawData.IndexCount;
                int startid = poly._RawData.IndexID;
                int endid = startid + ic;
                if (startid >= ynv.Indices.Count || endid > ynv.Indices.Count) continue;
                int startind = ynv.Indices[startid];
                if (startind >= vc) continue;
                var p0 = ynv.Vertices[startind];
                int tricount = ic - 2;
                for (int t = 0; t < tricount; t++)
                {
                    int tid = startid + t;
                    int ind1 = ynv.Indices[tid + 1];
                    int ind2 = ynv.Indices[tid + 2];
                    if (ind1 >= vc || ind2 >= vc) continue;
                    var p1 = ynv.Vertices[ind1]; var p2 = ynv.Vertices[ind2];
                    if (mray.Intersects(ref p0, ref p1, ref p2, out float hitdist) && hitdist < hit.HitDist && hitdist > 0)
                    {
                        var cellaabb = poly._RawData.CellAABB;
                        hit.NavPoly = poly;
                        hit.NavPoint = null;
                        hit.NavPortal = null;
                        hit.HitDist = hitdist;
                        hit.CamRel = -camPos;
                        hit.AABB = new BoundingBox(cellaabb.Min, cellaabb.Max);
                        break;
                    }
                }
            }
        }

        private void PickAudioZones(ref Ray ray, Vector3 camPos, ref WorldSelection hit)
        {
            var sd = SpaceDataOrNull; if (sd == null) return;
            sd.EnsureAudio();
            if (!sd.AudioReady) { WorldEdit.LastStatus = sd.Status.Length > 0 ? sd.Status : "loading audio zones..."; return; }
            foreach (var placement in sd.AudioPlacements)
            {
                if (placement == null) continue;
                var camrel = placement.Position - camPos;
                if (camrel.Length() > SelMaxDist) continue;
                switch (placement.Shape)
                {
                    case Dat151ZoneShape.Box:
                    case Dat151ZoneShape.Line:
                        {
                            var mray = new Ray(placement.OrientationInv.Multiply(ray.Position - placement.Position), placement.OrientationInv.Multiply(ray.Direction));
                            var bbox = new BoundingBox(placement.HitboxMin, placement.HitboxMax);
                            if (mray.Intersects(ref bbox, out float hitdist) && hitdist < hit.HitDist && hitdist > 0)
                            {
                                hit.Audio = placement;
                                hit.HitDist = hitdist;
                                hit.CamRel = camrel;
                                hit.AABB = bbox;
                                hit.BSphere = new BoundingSphere();
                            }
                            break;
                        }
                    case Dat151ZoneShape.Sphere:
                        {
                            if (placement.InnerPos == Vector3.Zero || placement.OuterPos == Vector3.Zero) break;
                            var bsph = new BoundingSphere(placement.Position, placement.HitSphereRad);
                            if (ray.Intersects(ref bsph, out float hitdist) && hitdist < hit.HitDist && hitdist > 0)
                            {
                                hit.Audio = placement;
                                hit.HitDist = hitdist;
                                hit.CamRel = camrel;
                                hit.AABB = new BoundingBox();
                                hit.BSphere = bsph;
                            }
                            break;
                        }
                }
            }
        }

        partial void WorldTargetChanged_SpaceData(IWorldGizmoTarget t, ref bool handled)
        {
            switch (t?.Key)
            {
                case YndNode _:
                case TrainTrackNode _:
                case ScenarioNode _:
                case YnvPoint _:
                case YnvPortal _:
                case AudioPlacement _:
                    handled = true;
                    if (worldGizmo.Dragging)
                    {
                        sdDirtyKey = t.Key;
                        if (t.Key is ScenarioNode sn && sn.Ymt?.ScenarioRegion != null)
                        {
                            try { sn.Ymt.ScenarioRegion.BuildVertices(); } catch { }
                            pathBatch?.Invalidate(sn.Ymt.ScenarioRegion);
                        }
                    }
                    else SpaceRefreshAfterEdit(t.Key);
                    WorldEdit.LastStatus = "moved " + WorldEdit.Selection.TypeName;
                    break;
            }
        }

        private void SpaceRefreshAfterEdit(object key)
        {
            var sd = spaceData; if (sd == null) return;
            switch (key)
            {
                case YndNode pn: sd.PathNodeMoved(pn); pathBatch?.Invalidate(pn.Ynd); if (pn.Links != null) foreach (var l in pn.Links) if (l?.Node2?.Ynd != null) pathBatch?.Invalidate(l.Node2.Ynd); break;
                case TrainTrackNode tn: sd.TrainNodeMoved(tn); pathBatch?.Invalidate(tn.Track); break;
                case ScenarioNode sn: sd.ScenarioNodeMoved(sn); pathBatch?.Invalidate(sn.Ymt?.ScenarioRegion); break;
                case YnvPoint np: sd.NavNodeMoved(np.Ynv); pathBatch?.Invalidate(np.Ynv); break;
                case YnvPortal npo: sd.NavNodeMoved(npo.Ynv); pathBatch?.Invalidate(npo.Ynv); break;
                case AudioPlacement _: break;
            }
        }

        private static void SpaceSetPosition(object key, Vector3 p)
        {
            switch (key)
            {
                case YndNode pn: pn.SetPosition(p); break;
                case TrainTrackNode tn: tn.SetPosition(p); break;
                case ScenarioNode sn: sn.SetPosition(p); break;
                case YnvPoint np: np.SetPosition(p); break;
                case YnvPortal npo: npo.SetPosition(p); break;
                case AudioPlacement au: au.SetPosition(p); break;
            }
        }

        private void SpaceNearestCandidate(WorldSelectionMode mode, Action<Vector3, float> consider)
        {
            var sd = SpaceDataOrNull; if (sd == null) return;
            var camPos = camera.Position;
            switch (mode)
            {
                case WorldSelectionMode.Path:
                    sd.EnsurePaths(); SpaceWaitFor(() => sd.PathsReady, 60);
                    if (!sd.PathsReady) break;
                    sd.GetYndsNear(camPos, 600.0f, sdYnds);
                    foreach (var y in sdYnds) if (y.Nodes != null) foreach (var n in y.Nodes) if (n != null) consider(n.Position, 20.0f);
                    break;
                case WorldSelectionMode.TrainTrack:
                    sd.EnsureTrains(); SpaceWaitFor(() => sd.TrainsReady, 30);
                    if (!sd.TrainsReady) break;
                    foreach (var t in sd.Trains.TrainTracks) if (t?.Nodes != null) foreach (var n in t.Nodes) if (n != null) consider(n.Position, 20.0f);
                    break;
                case WorldSelectionMode.Scenario:
                    sd.EnsureScenarios(); SpaceWaitFor(() => sd.ScenariosReady, 60);
                    if (!sd.ScenariosReady) break;
                    foreach (var ymt in sd.Scenarios.ScenarioRegions) if (ymt?.ScenarioRegion?.Nodes != null) foreach (var n in ymt.ScenarioRegion.Nodes) if (n != null) consider(n.Position, 20.0f);
                    break;
                case WorldSelectionMode.NavMesh:
                    sd.EnsureNav(); SpaceWaitFor(() => sd.NavReady, 60);
                    if (!sd.NavReady) break;
                    sd.GetYnvsNear(camPos, SdNavRange, sdYnvs);
                    SpaceWaitFor(() => sd.NavPending == 0, 60);
                    sd.GetYnvsNear(camPos, SdNavRange, sdYnvs);
                    foreach (var y in sdYnvs) if (y?.Polys != null) foreach (var p in y.Polys) if (p != null) consider(p.Position, 20.0f);
                    break;
                case WorldSelectionMode.Audio:
                    sd.EnsureAudio(); SpaceWaitFor(() => sd.AudioReady, 60);
                    if (!sd.AudioReady) break;
                    foreach (var p in sd.AudioPlacements)
                    {
                        if (p == null) continue;
                        if (p.Shape == Dat151ZoneShape.Sphere) { if (p.InnerPos != Vector3.Zero && p.OuterPos != Vector3.Zero) consider(p.Position, Math.Max(p.HitSphereRad, 1.0f) + 20.0f); }
                        else consider(p.Position, Math.Abs(p.HitboxMax.Z) + 20.0f);
                    }
                    break;
            }
        }

        private static void SpaceWaitFor(Func<bool> cond, double seconds)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!cond() && sw.Elapsed.TotalSeconds < seconds) System.Threading.Thread.Sleep(20);
        }

        partial void WorldSelReport_SpaceData(in WorldSelection s, System.Text.StringBuilder sb)
        {
            if (s.PathNode != null)
            {
                var n = s.PathNode;
                sb.Append($" | PATHNODE area={n.AreaID} node={n.NodeID} pos={n.Position} links={n.Links?.Length ?? 0} flags={n.Flags0.Value},{n.Flags1.Value},{n.Flags2.Value},{n.Flags3.Value},{n.Flags4.Value} street={n.StreetName} speed={n.Speed} special={n.Special} ynd={n.Ynd?.Name}");
            }
            else if (s.TrainTrackNode != null)
            {
                var n = s.TrainTrackNode;
                sb.Append($" | TRAINNODE track={n.Track?.Name} index={n.Index} type={n.NodeType} pos={n.Position}");
            }
            else if (s.ScenarioNode != null)
            {
                var n = s.ScenarioNode;
                var pt = n.MyPoint ?? n.ClusterMyPoint;
                sb.Append($" | SCENARIO {n.MedTypeName} pos={n.Position} type={pt?.Type?.Name ?? n.ChainingNode?.Type?.Name ?? "?"} modelset={pt?.ModelSet?.Name ?? ""} region={n.Ymt?.Name} chain={(n.ChainingNode?.Chain != null ? n.ChainingNode.Chain.ChainIndex.ToString() : "-")} cluster={(n.Cluster != null ? "yes" : "no")}");
            }
            else if (s.NavPoly != null)
            {
                var p = s.NavPoly;
                sb.Append($" | NAVPOLY ynv={p.Ynv?.Name} index={p.Index} area={p.AreaID} verts={p.Vertices?.Length ?? 0} flags0={p._RawData.PolyFlags0} flags1={p._RawData.PolyFlags1} pos={p.Position}");
            }
            else if (s.NavPoint != null)
            {
                var p = s.NavPoint;
                sb.Append($" | NAVPOINT ynv={p.Ynv?.Name} index={p.Index} type={p.Type} pos={p.Position}");
            }
            else if (s.NavPortal != null)
            {
                var p = s.NavPortal;
                sb.Append($" | NAVPORTAL ynv={p.Ynv?.Name} index={p.Index} type={p.Type} from={p.PositionFrom} to={p.PositionTo}");
            }
            else if (s.Audio != null)
            {
                var a = s.Audio;
                sb.Append($" | AUDIO {a.ShortTypeName} name={a.Name} shape={a.Shape} pos={a.Position} rel={a.RelFile?.Name} hitbox={a.HitboxMin}-{a.HitboxMax} sphere={a.HitSphereRad}");
            }
        }

        partial void RunWorldTestExtras_SpaceData(Action<string, bool, string> check, Action<Vector3> settle)
        {
            Console.WriteLine("---- space data modes (paths / nav / trains / scenarios / audio) ----");
            var sd = SpaceDataOrNull;
            check("the space data loader exists", sd != null, sd == null ? "no game files" : "ok");
            if (sd == null) return;
            var spot = new Vector3(-270.0f, -960.0f, 30.0f);
            CameraSequence.ApplyToCamera(camera, spot, 1.4f, 0.35f, settings.FovDeg);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            sd.EnsurePaths(); SpaceWaitFor(() => sd.PathsReady || !sd.PathsLoading, 120);
            check("paths (.ynd) load into the node grid", sd.PathsReady, sd.PathsReady ? $"{sd.AllYnds.Count} ynds, {sd.PathNodeCount:N0} nodes in {sd.PathsLoadMs:0} ms" : sd.Status);
            if (sd.PathsReady)
            {
                YndNode best = null; float bd = float.MaxValue;
                sd.GetYndsNear(spot, 600.0f, sdYnds);
                foreach (var y in sdYnds) if (y.Nodes != null) foreach (var n in y.Nodes) { float d = (n.Position - spot).Length(); if (d < bd) { bd = d; best = n; } }
                check("a path node is near downtown", best != null, best != null ? $"{best.AreaID}.{best.NodeID} at {bd:0} m, {best.Links?.Length ?? 0} links" : "none");
                if (best != null)
                {
                    var ray = new Ray(best.Position + new Vector3(0, 0, 20.0f), -Vector3.UnitZ);
                    var hit = WorldPickHit(ray, WorldSelectionMode.Path);
                    check("Path mode picks the node under the ray (ynd BVH)", ReferenceEquals(hit.PathNode, best), hit.GetNameString("nothing"));
                    check("the picked node's links resolved (Space.BuildYndLinks)", hit.PathNode?.Links != null && hit.PathNode.Links.Length > 0 && hit.PathNode.Links[0].Node2 != null,
                          $"{hit.PathNode?.Links?.Length ?? -1} of {hit.PathNode?.LinkCount ?? -1}");
                    if (hit.PathNode != null)
                    {
                        var t = hit.GizmoTarget();
                        check("a path node has a move gizmo (no rings)", t != null && t.RotationAxes == WorldWidgetAxis.None, t?.RotationAxes.ToString() ?? "null");
                        var p0 = best.Position;
                        t?.SetPosition(p0 + new Vector3(0, 0, 1.0f));
                        check("SetPosition moves the path node", (best.Position - p0).Length() > 0.9f, best.Position.ToString());
                        t?.SetPosition(p0);
                        sd.PathNodeMoved(best);
                    }
                }
                check("a ynd has line vertices to draw (BuildYndVerts)", sdYnds.Count > 0 && sdYnds[0].LinkedVerts != null && sdYnds[0].LinkedVerts.Length > 0, $"{(sdYnds.Count > 0 ? sdYnds[0].LinkedVerts?.Length ?? 0 : 0)} verts");
            }

            sd.EnsureTrains(); SpaceWaitFor(() => sd.TrainsReady || !sd.TrainsLoading, 60);
            check("train tracks load (traintracks.xml)", sd.TrainsReady, sd.TrainsReady ? $"{sd.Trains.TrainTracks.Count} tracks in {sd.TrainsLoadMs:0} ms" : sd.Status);
            if (sd.TrainsReady)
            {
                TrainTrackNode best = null; float bd = float.MaxValue;
                foreach (var t in sd.Trains.TrainTracks) if (t?.Nodes != null) foreach (var n in t.Nodes) { float d = (n.Position - spot).Length(); if (d < bd) { bd = d; best = n; } }
                check("a train track node exists", best != null, best != null ? $"{best.Track?.Name} #{best.Index} at {bd:0} m from downtown" : "none");
                if (best != null)
                {
                    var ray = new Ray(best.Position + new Vector3(0, 0, 20.0f), -Vector3.UnitZ);
                    var hit = WorldPickHit(ray, WorldSelectionMode.TrainTrack);
                    check("TrainTrack mode picks the node under the ray (track BVH)", hit.TrainTrackNode != null && ReferenceEquals(hit.TrainTrackNode.Track, best.Track), hit.GetNameString("nothing"));
                }
            }

            sd.EnsureScenarios(); SpaceWaitFor(() => sd.ScenariosReady || !sd.ScenariosLoading, 120);
            check("scenario regions load (sp_manifest.ymt)", sd.ScenariosReady, sd.ScenariosReady ? $"{sd.Scenarios.ScenarioRegions.Count} regions in {sd.ScenariosLoadMs:0} ms" : sd.Status);
            if (sd.ScenariosReady)
            {
                ScenarioNode best = null; float bd = float.MaxValue;
                foreach (var ymt in sd.Scenarios.ScenarioRegions) if (ymt?.ScenarioRegion?.Nodes != null) foreach (var n in ymt.ScenarioRegion.Nodes) { float d = (n.Position - spot).Length(); if (d < bd) { bd = d; best = n; } }
                check("a scenario point is near downtown", best != null && bd < 300.0f, best != null ? $"{best} at {bd:0} m" : "none");
                if (best != null)
                {
                    var ray = new Ray(best.Position + new Vector3(0, 0, 20.0f), -Vector3.UnitZ);
                    var hit = WorldPickHit(ray, WorldSelectionMode.Scenario);
                    check("Scenario mode picks the point under the ray (region BVH)", hit.ScenarioNode != null, hit.GetNameString("nothing"));
                    if (hit.ScenarioNode != null)
                    {
                        var pt = hit.ScenarioNode.MyPoint ?? hit.ScenarioNode.ClusterMyPoint;
                        check("the scenario point has a resolved type (ScenarioTypes)", pt == null || pt.Type != null || hit.ScenarioNode.ChainingNode != null, pt?.Type?.Name ?? "(cluster/chain node)");
                    }
                }
            }

            sd.EnsureNav(); SpaceWaitFor(() => sd.NavReady || !sd.NavLoading, 60);
            check("nav mesh grid loads (navmesh[x][y].ynv entries)", sd.NavReady, sd.NavReady ? $"{sd.NavCellCount} cells in {sd.NavLoadMs:0} ms" : sd.Status);
            if (sd.NavReady)
            {
                sd.GetYnvsNear(spot, 200.0f, sdYnvs);
                var swn = System.Diagnostics.Stopwatch.StartNew();
                SpaceWaitFor(() => sd.NavPending == 0 && !sd.Busy, 120);
                sd.GetYnvsNear(spot, 200.0f, sdYnvs);
                check("the ynvs around downtown stream in", sdYnvs.Count > 0, $"{sdYnvs.Count} ynvs in {swn.ElapsedMilliseconds} ms ({sd.NavLoadedCount} loaded)");
                if (sdYnvs.Count > 0)
                {
                    var ray = new Ray(new Vector3(spot.X, spot.Y, 80.0f), -Vector3.UnitZ);
                    var hit = WorldPickHit(ray, WorldSelectionMode.NavMesh);
                    check("NavMesh mode picks a poly under a downward ray (sector tree)", hit.NavPoly != null, hit.NavPoly != null ? hit.GetNameString("") + $" at {hit.HitDist:0.#} m, {hit.NavPoly.Vertices?.Length ?? 0} verts" : "nothing");
                    int tv = 0; foreach (var y in sdYnvs) tv += y.TriangleVerts?.Length ?? 0;
                    check("the ynvs have triangle vertices to draw", tv > 0, $"{tv} verts");
                }
            }

            sd.EnsureAudio(); SpaceWaitFor(() => sd.AudioReady || !sd.AudioLoading, 120);
            check("audio zones load (.rel dat151)", sd.AudioReady, sd.AudioReady ? $"{sd.AudioPlacements.Count} placements in {sd.AudioLoadMs:0} ms" : sd.Status);
            if (sd.AudioReady)
            {
                AudioPlacement best = null; float bd = float.MaxValue;
                foreach (var p in sd.AudioPlacements)
                {
                    if (p == null || p.Shape != Dat151ZoneShape.Box) continue;
                    if (p.HitboxMax.X <= 0 || p.HitboxMax.Y <= 0 || p.HitboxMax.Z <= 0) continue;
                    float d = (p.Position - spot).Length(); if (d < bd) { bd = d; best = p; }
                }
                check("a box audio zone is near downtown", best != null, best != null ? $"{best.Name} at {bd:0} m, box {best.HitboxMax}" : "none");
                if (best != null)
                {
                    var ray = new Ray(best.Position + new Vector3(0, 0, best.HitboxMax.Z + 50.0f), -Vector3.UnitZ);
                    var hit = WorldPickHit(ray, WorldSelectionMode.Audio);
                    check("Audio mode picks the zone under the ray (oriented hitbox)", hit.Audio != null, hit.GetNameString("nothing"));
                }
                AudioPlacement sph = null; bd = float.MaxValue;
                foreach (var p in sd.AudioPlacements)
                {
                    if (p == null || p.Shape != Dat151ZoneShape.Sphere || p.InnerPos == Vector3.Zero || p.OuterPos == Vector3.Zero || p.HitSphereRad <= 0) continue;
                    float d = (p.Position - spot).Length(); if (d < bd) { bd = d; sph = p; }
                }
                if (sph != null)
                {
                    var ray = new Ray(sph.Position + new Vector3(0, 0, sph.HitSphereRad + 50.0f), -Vector3.UnitZ);
                    var hit = WorldPickHit(ray, WorldSelectionMode.Audio);
                    check("Audio mode picks a sphere placement (emitter/rule) under the ray", hit.Audio != null, hit.Audio != null ? hit.GetNameString("") : "nothing");
                }
            }
            Console.WriteLine($"  space data total {sw.ElapsedMilliseconds} ms");
        }
    }
}

