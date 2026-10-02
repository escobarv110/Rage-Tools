using System;
using System.Numerics;
using CodeWalker;
using CodeWalker.GameFiles;
using CodeWalker.World;
using ImGuiNET;
using SDX = SharpDX;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public bool ShowPaths;
        public bool ShowNavMeshes;
        public bool ShowTrainTracks;
        public bool ShowScenarios;
        public bool ShowAudioZones;
        public bool ShowHeightmap;
        public bool ShowAudioOuterBounds;
        public float SpaceDataRange = 1000.0f;
        public float SpaceNavRange = 450.0f;
        public string SpaceDataStatus = "";

        public object SpaceEditKey;
        public SDX.Vector3 SpaceEditBefore, SpaceEditAfter;
        public string SpaceEditName;

        partial void DrawHelpersExtras_SpaceData()
        {
            ViewGroup("Map data");
            OptCheck("Paths", ref ShowPaths, "The vehicle/ped path nodes (.ynd): nodes as cubes coloured by flags, links as lines with their lanes.\nAlso drawn while the Path selection mode is active.");
            SameCol();
            OptCheck("Nav meshes", ref ShowNavMeshes, "The navigation meshes (.ynv): polygons filled by type, portals and points as cubes.\nStreamed in around the camera. Also drawn in the Nav Mesh selection mode.");
            OptCheck("Train tracks", ref ShowTrainTracks, "traintracks.xml and its node lists: nodes as cubes, the track as a line coloured by node type.");
            SameCol();
            OptCheck("Scenarios", ref ShowScenarios, "The scenario points (sp_manifest.ymt regions): points as cubes with direction lines, chains as edges, clusters.");
            OptCheck("Audio zones", ref ShowAudioZones, "The ambient zones, rules and static emitters of the .rel files: boxes and spheres (cyan = positioning zone).");
            SameCol();
            OptCheck("Heightmap", ref ShowHeightmap, "heightmap.dat as a shaded relief surface: the highest ground in each cell lit by its own slope, with the lowest under it in a cooler translucent layer.\nAbout fifty metres per cell - the shape of the world, not its geometry.");
            if (ShowAudioZones || SelectionModeEnum == WorldSelectionMode.Audio)
            {
                SameCol();
                OptCheck("Outer bounds", ref ShowAudioOuterBounds, "Also draw the audio zones' activation bounds (blue).");
            }
            if (!string.IsNullOrEmpty(SpaceDataStatus)) ImGui.TextDisabled(SpaceDataStatus);
        }

        partial void DrawHelpersExtras_SpaceDataAdvanced()
        {
            OptSlider("Map data range", ref SpaceDataRange, 100.0f, 4000.0f, "%.0f m",
                      "Paths, scenarios and audio zones further than this from the camera are not drawn.", def: 1000.0f);
            OptSlider("Nav mesh range", ref SpaceNavRange, 150.0f, 1500.0f, "%.0f m",
                      "How far the nav meshes stream in around the camera (150 m cells; each cell is a file).", def: 450.0f);
        }

        partial void DrawWorldSelectionExtras_SpaceData(ref bool handled)
        {
            var s = WorldSelection;
            if (!s.HasValue) return;
            bool mine = s.PathNode != null || s.TrainTrackNode != null || s.ScenarioNode != null ||
                        s.NavPoly != null || s.NavPoint != null || s.NavPortal != null || s.Audio != null;
            if (!mine) return;
            handled = true;
            rowSeq = 0;
            ImGui.TextWrapped(s.GetNameString("(nothing)"));
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(SelectionKeysTip_M3);

            if (s.PathNode != null) DrawPathNodePage(s.PathNode);
            else if (s.TrainTrackNode != null) DrawTrainNodePage(s.TrainTrackNode);
            else if (s.ScenarioNode != null) DrawScenarioNodePage(s.ScenarioNode);
            else if (s.NavPoly != null) DrawNavPolyPage(s.NavPoly);
            else if (s.NavPoint != null) DrawNavPointPage(s.NavPoint);
            else if (s.NavPortal != null) DrawNavPortalPage(s.NavPortal);
            else if (s.Audio != null) DrawAudioPage(s.Audio);

            ImGui.Spacing();
            DrawAddToProjectButton("sdsel");
            ImGui.TextDisabled("Edits go to the project's copy of the file (Save All writes it).");
        }

        private void SpacePositionRow(object key, string name, SDX.Vector3 pos, Action<SDX.Vector3> apply)
        {
            var n = N(pos);
            ImGui.SetNextItemWidth(-1);
            if (ImGui.DragFloat3("##sdpos", ref n, 0.05f))
            {
                var np = S(n);
                if (SpaceEditKey == null || !ReferenceEquals(SpaceEditKey, key)) { SpaceEditKey = key; SpaceEditBefore = pos; SpaceEditName = "Move " + name; }
                SpaceEditAfter = np;
                apply(np);
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Position  (X Y Z) - the gizmo moves it too");
        }

        private static string V3(SDX.Vector3 v) => $"{v.X:0.###}, {v.Y:0.###}, {v.Z:0.###}";

        private void DrawPathNodePage(YndNode n)
        {
            SelSection("PATH NODE");
            Row("Ynd", n.Ynd?.Name ?? "");
            Row("Area / Node", $"{n.AreaID} / {n.NodeID}");
            SpacePositionRow(n, "path node", n.Position, p => n.SetPosition(p));
            Row("Street", n.StreetName.ToString());
            Row("Speed", n.Speed.ToString());
            Row("Special", n.Special.ToString());
            Row("Flags 0-4", $"{n.Flags0.Value}, {n.Flags1.Value}, {n.Flags2.Value}, {n.Flags3.Value}, {n.Flags4.Value}");
            Row("Junction", n.HasJunction && n.Junction != null ? $"yes ({n.Junction.Heightmap?.CountX ?? 0} x {n.Junction.Heightmap?.CountY ?? 0})" : "no");
            Row("Density", n.Density.ToString());
            Row("Heuristic", n.HeuristicValue.ToString());
            var props = new System.Text.StringBuilder();
            if (n.OffRoad) props.Append("OffRoad ");
            if (n.NoBigVehicles) props.Append("NoBigVehicles ");
            if (n.CannotGoLeft) props.Append("CannotGoLeft ");
            if (n.SlipRoad) props.Append("SlipRoad ");
            if (n.IndicateKeepLeft) props.Append("KeepLeft ");
            if (n.IndicateKeepRight) props.Append("KeepRight ");
            if (n.NoGps) props.Append("NoGps ");
            if (n.IsJunction) props.Append("Junction ");
            if (n.Highway) props.Append("Highway ");
            if (n.IsDisabledUnk0 || n.IsDisabledUnk1) props.Append("Disabled ");
            if (n.Tunnel) props.Append("Tunnel ");
            if (n.IsPedNode) props.Append("PedNode ");
            if (n.LeftTurnsOnly) props.Append("LeftTurnsOnly ");
            Row("Properties", props.Length > 0 ? props.ToString().TrimEnd() : "(none)");
            Row("Links", $"{n.Links?.Length ?? 0} of {n.LinkCount}");
            if (n.Links != null)
            {
                for (int i = 0; i < n.Links.Length && i < 16; i++)
                {
                    var l = n.Links[i];
                    if (l == null) continue;
                    var to = l.Node2;
                    Row($"  link {i}", $"-> {(to != null ? to.AreaID + "." + to.NodeID : "?")}  lanes {l.LaneCountForward}/{l.LaneCountBackward}  len {l.LinkLength.Value}  offset {l.LaneOffset:0.##}" +
                                       (l.Shortcut ? " shortcut" : "") + (l.NarrowRoad ? " narrow" : "") + (l.DontUseForNavigation ? " no-nav" : "") + (l.GpsBothWays ? " gps-both" : ""));
                }
            }
        }

        private void DrawTrainNodePage(TrainTrackNode n)
        {
            SelSection("TRAIN TRACK NODE");
            Row("Track", n.Track?.Name ?? "");
            Row("Config", n.Track?.trainConfigName ?? "");
            Row("Index", $"{n.Index} of {n.Track?.Nodes?.Count ?? 0}");
            SpacePositionRow(n, "train node", n.Position, p => n.SetPosition(p));
            Row("Node type", n.NodeType.ToString() + (n.NodeType == 1 || n.NodeType == 2 || n.NodeType == 5 ? " (station)" : ""));
            Row("Prev / Next", $"{(n.Links[0] != null ? n.Links[0].Index.ToString() : "-")} / {(n.Links[1] != null ? n.Links[1].Index.ToString() : "-")}");
            if (n.Track != null)
            {
                Row("Track speed", $"{n.Track.speed:0.##}");
                Row("Braking dist", $"{n.Track.brakingDist:0.##}");
                Row("Ping-pong", n.Track.isPingPongTrack.ToString());
                Row("Stops at stations", $"{n.Track.stopsAtStations} (MP {n.Track.MPstopsAtStations})");
                Row("Stations", n.Track.StationCount.ToString());
            }
        }

        private void DrawScenarioNodePage(ScenarioNode n)
        {
            SelSection("SCENARIO " + n.MedTypeName.ToUpperInvariant());
            Row("Region", n.Ymt?.Name ?? "");
            SpacePositionRow(n, "scenario point", n.Position, p => n.SetPosition(p));
            var e = WorldEditor.ToEulerDegrees(n.Orientation);
            Row("Heading", $"{e.Z:0.#} deg");
            var pt = n.MyPoint ?? n.ClusterMyPoint;
            if (pt != null)
            {
                Row("Type", pt.Type?.Name ?? pt.TypeId.ToString());
                Row("Model set", pt.ModelSet?.Name ?? pt.ModelSetId.ToString());
                Row("Interior", pt.InteriorName.ToString());
                Row("Group", pt.GroupName.ToString());
                Row("Required imap", pt.IMapName.ToString());
                Row("Time", pt.TimeRange);
                Row("Probability", pt.Probability.ToString());
                Row("Radius", pt.Radius.ToString());
                Row("Wait time", pt.WaitTime.ToString());
                Row("MP / SP", pt.AvailableMpSp.ToString());
                Row("Flags", pt.Flags.ToString());
            }
            var sp = n.LoadSavePoint ?? n.ClusterLoadSavePoint ?? n.EntityPoint;
            if (sp != null)
            {
                Row("Spawn type", sp.SpawnType.ToString());
                Row("Ped type", sp.PedType.ToString());
                Row("Group", sp.Group.ToString());
                Row("Interior", sp.Interior.ToString());
                Row("Time", $"{sp.StartTime:00}:00 - {sp.EndTime:00}:00");
                Row("Probability", $"{sp.Probability:0.##}");
                Row("Radius", $"{sp.Radius:0.##}");
                Row("Flags", sp.Flags.ToString());
            }
            if (n.Cluster != null)
            {
                Row("Cluster", $"{n.Cluster.Points?.MyPoints?.Length ?? 0} points, radius {n.Cluster.Radius:0.##}");
                Row("Cluster pos", V3(n.Cluster.Position));
                Row("Spawn delay", $"{n.Cluster.NextSpawnAttemptDelay:0.##}");
                Row("All required", n.Cluster.AllPointsRequiredForSpawn.ToString());
            }
            if (n.Entity != null)
            {
                Row("Entity override", n.Entity.ToString());
            }
            if (n.ChainingNode != null)
            {
                var cn = n.ChainingNode;
                Row("Chain node", $"#{cn.NodeIndex}  type {cn.Type?.Name ?? cn.TypeHash.ToString()}");
                Row("Chain edges", $"in {cn.HasIncomingEdges}, out {cn.HasOutgoingEdges}");
                if (cn.Chain != null)
                {
                    Row("Chain", $"#{cn.Chain.ChainIndex}, {cn.Chain.Edges?.Length ?? 0} edges");
                    if (cn.Chain.Edges != null)
                    {
                        for (int i = 0; i < cn.Chain.Edges.Length && i < 12; i++)
                        {
                            var ed = cn.Chain.Edges[i];
                            if (ed == null) continue;
                            Row($"  edge {i}", $"{ed.NodeIndexFrom} -> {ed.NodeIndexTo}  {ed.Action}  {ed.NavMode}  {ed.NavSpeed}");
                        }
                    }
                }
            }
        }

        private void DrawNavPolyPage(YnvPoly p)
        {
            SelSection("NAV MESH POLYGON");
            Row("Ynv", p.Ynv?.Name ?? "");
            Row("Index", p.Index.ToString());
            Row("Area / Part", $"{p.AreaID} / {p.PartID}");
            Row("Center", V3(p.Position));
            Row("Vertices", (p.Vertices?.Length ?? 0).ToString());
            Row("Edges", (p.Edges?.Length ?? 0).ToString());
            Row("Portal links", $"{p.PortalLinkCount} (id {p.PortalLinkID})");
            Row("Flags 1-5", $"{p.Flags1}, {p.Flags2}, {p.Flags3}, {p.Flags4}, {p.Flags5}");
            Row("Poly flags", $"{p._RawData.PolyFlags0}, {p._RawData.PolyFlags1}, {p._RawData.PolyFlags2}");
            var props = new System.Text.StringBuilder();
            if (p.B02_IsFootpath) props.Append("Footpath ");
            if (p.B03_IsUnderground) props.Append("Underground ");
            if (p.B06_SteepSlope) props.Append("SteepSlope ");
            if (p.B07_IsWater) props.Append("Water ");
            if (p.B13_HasPathNode) props.Append("HasPathNode ");
            if (p.B14_IsInterior) props.Append("Interior ");
            if (p.B17_IsFlatGround) props.Append("FlatGround ");
            if (p.B18_IsRoad) props.Append("Road ");
            if (p.B19_IsCellEdge) props.Append("CellEdge ");
            if (p.B20_IsTrainTrack) props.Append("TrainTrack ");
            if (p.B21_IsShallowWater) props.Append("ShallowWater ");
            if (p.B24_FootpathMall) props.Append("Mall ");
            Row("Properties", props.Length > 0 ? props.ToString().TrimEnd() : "(none)");
            var cb = p._RawData.CellAABB;
            Row("Cell AABB", $"({cb.Min.X:0.#}, {cb.Min.Y:0.#}, {cb.Min.Z:0.#}) - ({cb.Max.X:0.#}, {cb.Max.Y:0.#}, {cb.Max.Z:0.#})");
            if (p.Vertices != null)
            {
                for (int i = 0; i < p.Vertices.Length && i < 12; i++) Row($"  v{i}", V3(p.Vertices[i]));
            }
        }

        private void DrawNavPointPage(YnvPoint p)
        {
            SelSection("NAV MESH POINT");
            Row("Ynv", p.Ynv?.Name ?? "");
            Row("Index", p.Index.ToString());
            SpacePositionRow(p, "nav point", p.Position, np => p.SetPosition(np));
            Row("Type", p.Type.ToString());
            Row("Angle", $"{p.Angle} ({p.Direction:0.###} rad)");
        }

        private void DrawNavPortalPage(YnvPortal p)
        {
            SelSection("NAV MESH PORTAL");
            Row("Ynv", p.Ynv?.Name ?? "");
            Row("Index", p.Index.ToString());
            SpacePositionRow(p, "nav portal", p.PositionFrom, np => p.SetPosition(np));
            Row("Position to", V3(p.PositionTo));
            Row("Type", p.Type.ToString());
            Row("Angle", $"{p.Angle} ({p.Direction:0.###} rad)");
            Row("Area from / to", $"{p.AreaIDFrom} / {p.AreaIDTo}");
            Row("Poly from", $"{p.PolyIDFrom1}, {p.PolyIDFrom2}");
            Row("Poly to", $"{p.PolyIDTo1}, {p._RawData.PolyIDTo2}");
        }

        private void DrawAudioPage(AudioPlacement a)
        {
            SelSection("AUDIO " + (a.FullTypeName ?? "").ToUpperInvariant());
            Row("Rel file", a.RelFile?.Name ?? "");
            Row("Name", a.Name ?? a.NameHash.ToString());
            Row("Shape", a.Shape.ToString());
            SpacePositionRow(a, "audio " + a.ShortTypeName, a.Position, np => a.SetPosition(np));
            var e = WorldEditor.ToEulerDegrees(a.Orientation);
            Row("Heading", $"{e.Z:0.#} deg");
            if (a.Shape == Dat151ZoneShape.Sphere)
            {
                Row("Inner radius", $"{a.InnerRadius:0.##}");
                Row("Outer radius", $"{a.OuterRadius:0.##}");
                Row("Outer pos", V3(a.OuterPos));
            }
            else
            {
                Row("Inner pos", V3(a.InnerPos));
                Row("Inner size", V3(a.InnerMax - a.InnerMin));
                Row("Outer pos", V3(a.OuterPos));
                Row("Outer size", V3(a.OuterMax - a.OuterMin));
            }
            if (a.AmbientZone != null)
            {
                var z = a.AmbientZone;
                SelSection("AMBIENT ZONE");
                Row("Flags", z.Flags.ToString());
                Row("Activation centre", V3(z.ActivationZoneCentre));
                Row("Activation size", V3(z.ActivationZoneSize));
                Row("Activation angle", z.ActivationZoneRotationAngle.ToString());
                Row("Positioning centre", V3(z.PositioningZoneCentre));
                Row("Positioning size", V3(z.PositioningZoneSize));
                Row("Positioning angle", z.PositioningZoneRotationAngle.ToString());
            }
            if (a.AmbientRule != null)
            {
                var r = a.AmbientRule;
                SelSection("AMBIENT RULE");
                Row("Flags", r.Flags.ToString());
                Row("Child sound", r.ChildSound.ToString());
                Row("Category", r.Category.ToString());
                Row("Weight", $"{r.Weight:0.###}");
                Row("Min / max dist", $"{r.MinDist:0.##} / {r.MaxDist:0.##}");
                Row("Time (min)", $"{r.MinTimeMinutes} - {r.MaxTimeMinutes}");
                Row("Repeat", $"{r.MinRepeatTime} +/- {r.MinRepeatTimeVariance}");
                Row("Spawn height", r.SpawnHeight.ToString());
                Row("Explicit spawn", r.ExplicitSpawn.ToString());
            }
            if (a.StaticEmitter != null)
            {
                var em = a.StaticEmitter;
                SelSection("STATIC EMITTER");
                Row("Flags", em.Flags.ToString());
                Row("Child sound", em.ChildSound.ToString());
                Row("Radio station", em.RadioStation.ToString());
                Row("Min / max dist", $"{em.MinDistance:0.##} / {em.MaxDistance:0.##}");
                Row("Emitted volume", em.EmittedVolume.ToString());
                Row("LPF / HPF", $"{em.LPFCutoff} / {em.HPFCutoff}");
                Row("Rolloff", em.RolloffFactor.ToString());
                Row("Interior / room", $"{em.Interior} / {em.Room}");
                Row("Max leakage", $"{em.MaxLeakage:0.###} ({em.MinLeakageDistance} - {em.MaxLeakageDistance})");
                Row("Alarm", em.Alarm.ToString());
            }
        }
    }
}

