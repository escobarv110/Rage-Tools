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
        public WorldSelection WorldSelection = WorldSelection.Empty;

        public WorldSelectionMode SelectionModeEnum => ModeFromName(SelectionModeName);

        public static WorldSelectionMode ModeFromName(string name)
        {
            switch (name)
            {
                case "Entity": return WorldSelectionMode.Entity;
                case "Entity Precision": return WorldSelectionMode.EntityPrecision;
                case "Entity Extension": return WorldSelectionMode.EntityExtension;
                case "Archetype Extension": return WorldSelectionMode.ArchetypeExtension;
                case "Time Cycle Modifier": return WorldSelectionMode.TimeCycleModifier;
                case "Car Generator": return WorldSelectionMode.CarGenerator;
                case "Grass": return WorldSelectionMode.Grass;
                case "Water Quad": return WorldSelectionMode.WaterQuad;
                case "Water Calming Quad": return WorldSelectionMode.CalmingQuad;
                case "Water Wave Quad": return WorldSelectionMode.WaveQuad;
                case "Collision": return WorldSelectionMode.Collision;
                case "Nav Mesh": return WorldSelectionMode.NavMesh;
                case "Path": return WorldSelectionMode.Path;
                case "Train Track": return WorldSelectionMode.TrainTrack;
                case "Lod Lights": return WorldSelectionMode.LodLights;
                case "Mlo Instance": return WorldSelectionMode.MloInstance;
                case "Scenario": return WorldSelectionMode.Scenario;
                case "Audio": return WorldSelectionMode.Audio;
                case "Occlusion": return WorldSelectionMode.Occlusion;
                case "Light": return WorldSelectionMode.Light;
            }
            return WorldSelectionMode.None;
        }

        public static int IndexOfMode(WorldSelectionMode m)
        {
            for (int i = 0; i < SelectionModeNames.Length; i++)
                if (ModeFromName(SelectionModeNames[i]) == m) return i;
            return -1;
        }

        public static bool TryParseMode(string s, out WorldSelectionMode mode)
        {
            mode = WorldSelectionMode.None;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim();
            if (int.TryParse(s, out int n)) { mode = (WorldSelectionMode)n; return true; }
            if (Enum.TryParse(s.Replace(" ", ""), true, out WorldSelectionMode em)) { mode = em; return true; }
            var m2 = ModeFromName(s);
            if (m2 != WorldSelectionMode.None) { mode = m2; return true; }
            var k = s.Replace(" ", "").ToLowerInvariant();
            if (k == "cargen" || k == "cargens") { mode = WorldSelectionMode.CarGenerator; return true; }
            if (k == "tcm" || k == "timecycle") { mode = WorldSelectionMode.TimeCycleModifier; return true; }
            if (k == "lodlight" || k == "lodlights") { mode = WorldSelectionMode.LodLights; return true; }
            if (k == "mlo" || k == "interior") { mode = WorldSelectionMode.MloInstance; return true; }
            if (k == "water") { mode = WorldSelectionMode.WaterQuad; return true; }
            if (k == "occluder" || k == "occluders") { mode = WorldSelectionMode.Occlusion; return true; }
            if (k == "precision" || k == "precise") { mode = WorldSelectionMode.EntityPrecision; return true; }
            return false;
        }

        public bool WorldSelEdited;
        public string WorldSelEditName;
        public object WorldSelEditKey, WorldSelEditBefore, WorldSelEditAfter;
        public bool RequestWorldSelectionDelete;
        public bool RequestWorldSelectionFrame;
        public bool RequestAddSelectionToProject;

        private void DrawAddToProjectButton(string id)
        {
            if (!WorldSelection.HasValue) return;
            if (ImGui.Button("Add to project##" + id, new Vector2(-1, 0))) RequestAddSelectionToProject = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The file this selection lives in joins the project - an entity's ymap, an\n" +
                                 "interior's ytyp, a scenario point's region, a path node's ynd, a nav poly's\n" +
                                 "ynv, a train node's track, an audio zone's rel - and the Project window\n" +
                                 "opens on it. Editing the item does the same on its own.");
        }
        public bool ShowSelectionHelpers = true;
        public bool ShowCarGenModels;
        public bool ShowPickDebug;
        partial void DrawHelpersExtras_Selection()
        {
            OptCheck("Selection helpers", ref ShowSelectionHelpers,
                     "Blue boxes for everything the current selection mode could pick -\n" +
                     "car generators, time cycle modifiers, LOD light files, interiors...");
            SameCol();
            OptCheck("Car gen models", ref ShowCarGenModels,
                     "The vehicle each car generator spawns, drawn where it spawns, for every\n" +
                     "generator within 100 m. The selected generator's car is always drawn.");
            DrawHelpersExtras_Occluders_J1();
        }

        partial void DrawHelpersExtras_SelectionAdvanced()
        {
        }

        public WorldSelection CollisionUnderCursor = WorldSelection.Empty;
        public bool RequestSelectCollisionUnderCursor;

        partial void DrawCollisionUnderCursor_Selection()
        {
            if (!WorldShowCollision) return;
            var c = CollisionUnderCursor;
            if (!c.HasValue || c.CollisionBounds == null) return;
            var s = WorldSelection;
            if (s.CollisionBounds != null && ReferenceEquals(s.CollisionBounds, c.CollisionBounds) &&
                ReferenceEquals(s.CollisionPoly, c.CollisionPoly)) return;

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled("COLLISION UNDER CURSOR");
            rowSeq = 0;
            var b = c.CollisionBounds;
            var ybn = b.GetRootYbn();
            string file = ybn?.Name ?? ybn?.RpfFileEntry?.Name ?? b.GetName() ?? "";
            if (!string.IsNullOrEmpty(file) && !file.EndsWith(".ybn", StringComparison.OrdinalIgnoreCase) && ybn != null) file += ".ybn";
            Row("File", string.IsNullOrEmpty(file) ? "(embedded bounds)" : file);
            if (c.EntityDef != null) Row("Entity", c.EntityDef.Archetype?.Name ?? c.EntityDef.Name ?? "");
            Row("Bounds", b.Type.ToString() + (b.Parent != null ? "  (in " + b.Parent.Type + ")" : ""));
            if (b is BoundGeometry bg) Row("Polygons", (bg.Polygons?.Length ?? 0).ToString());
            var poly = c.CollisionPoly;
            if (poly != null)
            {
                Row("Polygon", $"#{poly.Index}  {poly.Type}");
                var mat = poly.Material;
                var mname = CollisionView.MaterialsReady ? BoundsMaterialTypes.GetMaterialName(mat.Type) : ("material " + mat.Type.Index);
                Row("Material", mname + "   # " + mat.Type.Index);
                if (mat.Flags != 0) Row("Mat flags", mat.Flags.ToString());
                Row("Room / proc", $"{mat.RoomId} / {mat.ProceduralId}");
            }
            else if (b.MaterialIndex != 0 || !(b is BoundGeometry))
            {
                var mname = CollisionView.MaterialsReady ? BoundsMaterialTypes.GetMaterialName(b.MaterialIndex) : ("material " + b.MaterialIndex);
                Row("Material", mname + "   # " + b.MaterialIndex);
            }
            if (c.CollisionVertex != null) Row("Vertex", c.CollisionVertex.Index.ToString());
            Row("Hit distance", $"{c.HitDist:0.##} m");
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(UiTheme.Accent.X, UiTheme.Accent.Y, UiTheme.Accent.Z, 0.35f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(UiTheme.AccentBright.X, UiTheme.AccentBright.Y, UiTheme.AccentBright.Z, 0.55f));
            if (ImGui.SmallButton("Selection mode: Collision##colcursor")) RequestSelectCollisionUnderCursor = true;
            ImGui.PopStyleColor(2);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Switch to the Collision selection mode with this polygon selected\n" +
                                 "(the gizmo then moves the bound, the page shows every field).");
        }

        private object selPageBefore;
        private bool selPageChanged;

        private void SelChanged(bool changed) { if (changed) selPageChanged = true; }

        partial void DrawWorldSelectionExtras_Selection(ref bool handled)
        {
            var s = WorldSelection;
            if (!s.HasValue) return;
            if (s.Light != null) { handled = true; DrawWorldLightPage(s); return; }

            if (s.MloEntityDef != null && s.CollisionBounds == null)
            {
                DrawMloInstanceHeader(s.MloEntityDef);
                return;
            }
            if (s.EntityDef != null && s.CollisionBounds == null && s.CollisionPoly == null && s.CollisionVertex == null) return;

            handled = true;
            rowSeq = 0;
            ImGui.TextWrapped(s.GetNameString("(nothing)"));
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(SelectionKeysTip_M3);
            var ymapName = s.OwnerYmap?.Name;
            if (!string.IsNullOrEmpty(ymapName)) ImGui.TextDisabled("in " + ymapName);

            selPageChanged = false;
            selPageBefore = SelCaptureState(s);
            var key = s.GetProjectObject();
            string editName = "Edit " + s.TypeName;

            if (s.CarGenerator != null) DrawCarGenPage(s.CarGenerator);
            else if (s.LodLight != null) DrawLodLightPage(s.LodLight);
            else if (s.TimeCycleModifier != null) DrawTcmPage(s.TimeCycleModifier);
            else if (s.GrassBatch != null) DrawGrassPage(s.GrassBatch);
            else if (s.BoxOccluder != null) DrawBoxOccluderPage(s.BoxOccluder);
            else if (s.OccludeModelTri != null) DrawOccludeTriPage(s.OccludeModelTri);
            else if (s.WaterQuad != null || s.CalmingQuad != null || s.WaveQuad != null) DrawWaterQuadPage(s);
            else if (s.CollisionBounds != null || s.CollisionPoly != null || s.CollisionVertex != null) DrawCollisionPage(s);
            else
            {
                Row("Type", s.TypeName);
                var wp = s.WidgetPosition;
                Row("Position", $"{wp.X:0.###}, {wp.Y:0.###}, {wp.Z:0.###}");
            }

            ImGui.Spacing();
            DrawAddToProjectButton("sel");

            if (selPageChanged && key != null)
            {
                WorldSelEdited = true;
                WorldSelEditName = editName;
                WorldSelEditKey = key;
                WorldSelEditBefore = selPageBefore;
                WorldSelEditAfter = SelCaptureState(s);
            }

            DrawCollisionUnderCursor_Selection();
            if (WorldDirtyCount > 0) DrawWorldSaveRow();
        }

        private void DrawMloInstanceHeader(YmapEntityDef mlo)
        {
            var mloa = mlo.Archetype as MloArchetype;
            ImGui.TextDisabled("MLO INSTANCE");
            rowSeq = 0;
            Row("Interior", mloa?.Name ?? mlo.Archetype?.Name ?? "?");
            Row("Rooms", (mloa?.rooms?.Length ?? 0).ToString());
            Row("Portals", (mloa?.portals?.Length ?? 0).ToString());
            Row("Entities", (mlo.MloInstance?.Entities?.Length ?? 0).ToString());
            Row("Entity sets", (mloa?.entitySets?.Length ?? 0).ToString());
            if (mloa?.rooms != null)
            {
                var names = new System.Text.StringBuilder();
                for (int i = 0; i < mloa.rooms.Length && i < 24; i++) { if (i > 0) names.Append(", "); names.Append(mloa.rooms[i].RoomName); }
                if (mloa.rooms.Length > 24) names.Append(", ...");
                Row("Room names", names.ToString());
            }
            ImGui.Spacing();
        }

        private struct CarGenState { public CCarGen Data; public SDX.Vector3 Pos; public SDX.Quaternion Ori; public SDX.Vector3 BBMin, BBMax; }
        private struct LodLightState
        {
            public SDX.Vector3 Position, Direction, TangentX, TangentY, Scale; public SDX.Quaternion Orientation;
            public SDX.Color Colour; public float Falloff, FalloffExponent; public uint TimeAndStateFlags, Hash;
            public byte ConeInnerAngle, ConeOuterAngleOrCapExt, CoronaIntensity;
        }
        private struct TcmState { public CTimeCycleModifier Data; public SDX.Vector3 BBMin, BBMax; }
        private struct BoxOccluderState { public SDX.Vector3 Position, Size; public SDX.Quaternion Orientation; }
        private struct OccludeTriState { public SDX.Vector3 C1, C2, C3; public uint Flags; }

        public static object SelCaptureState(in WorldSelection s)
        {
            if (s.CarGenerator != null)
            {
                var cg = s.CarGenerator;
                return new CarGenState { Data = cg._CCarGen, Pos = cg.Position, Ori = cg.Orientation, BBMin = cg.BBMin, BBMax = cg.BBMax };
            }
            if (s.LodLight != null)
            {
                var l = s.LodLight;
                return new LodLightState
                {
                    Position = l.Position, Direction = l.Direction, TangentX = l.TangentX, TangentY = l.TangentY, Scale = l.Scale,
                    Orientation = l.Orientation, Colour = l.Colour, Falloff = l.Falloff, FalloffExponent = l.FalloffExponent,
                    TimeAndStateFlags = l.TimeAndStateFlags, Hash = l.Hash, ConeInnerAngle = l.ConeInnerAngle,
                    ConeOuterAngleOrCapExt = l.ConeOuterAngleOrCapExt, CoronaIntensity = l.CoronaIntensity,
                };
            }
            if (s.TimeCycleModifier != null)
            {
                var t = s.TimeCycleModifier;
                return new TcmState { Data = t.CTimeCycleModifier, BBMin = t.BBMin, BBMax = t.BBMax };
            }
            if (s.BoxOccluder != null)
            {
                var b = s.BoxOccluder;
                return new BoxOccluderState { Position = b.Position, Size = b.Size, Orientation = b.Orientation };
            }
            if (s.OccludeModelTri != null)
            {
                var t = s.OccludeModelTri;
                return new OccludeTriState { C1 = t.Corner1, C2 = t.Corner2, C3 = t.Corner3, Flags = t.Model?.Flags ?? 0u };
            }
            return null;
        }

        public static void SelRestoreState(object key, object state)
        {
            switch (key)
            {
                case YmapCarGen cg when state is CarGenState cs:
                    cg._CCarGen = cs.Data; cg.Position = cs.Pos; cg.Orientation = cs.Ori; cg.BBMin = cs.BBMin; cg.BBMax = cs.BBMax;
                    break;
                case YmapLODLight l when state is LodLightState ls:
                    l.SetPosition(ls.Position); l.SetColour(ls.Colour);
                    l.Direction = ls.Direction; l.TangentX = ls.TangentX; l.TangentY = ls.TangentY; l.Scale = ls.Scale;
                    l.Orientation = ls.Orientation; l.Falloff = ls.Falloff; l.FalloffExponent = ls.FalloffExponent;
                    l.TimeAndStateFlags = ls.TimeAndStateFlags; l.Hash = ls.Hash; l.ConeInnerAngle = ls.ConeInnerAngle;
                    l.ConeOuterAngleOrCapExt = ls.ConeOuterAngleOrCapExt; l.CoronaIntensity = ls.CoronaIntensity;
                    break;
                case YmapTimeCycleModifier t when state is TcmState ts:
                    t.CTimeCycleModifier = ts.Data; t.BBMin = ts.BBMin; t.BBMax = ts.BBMax;
                    break;
                case YmapBoxOccluder b when state is BoxOccluderState bs:
                    b.Position = bs.Position; b.SetSize(bs.Size); b.Orientation = bs.Orientation; b.UpdateBoxStruct();
                    break;
                case YmapOccludeModelTriangle ot when state is OccludeTriState os:
                    ot.Corner1 = os.C1; ot.Corner2 = os.C2; ot.Corner3 = os.C3;
                    if (ot.Model != null) { ot.Model.Flags = os.Flags; ot.Model.BuildVertices(); ot.Model.BuildData(); ot.Model.BuildBVH(); }
                    break;
            }
        }

        private static Vector3 N(SDX.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
        private static SDX.Vector3 S(Vector3 v) => new SDX.Vector3(v.X, v.Y, v.Z);

        private bool SelDrag3(string label, ref SDX.Vector3 v, float speed, string tip = null)
        {
            var n = N(v);
            ImGui.SetNextItemWidth(-1);
            bool ch = ImGui.DragFloat3(label, ref n, speed);
            if (tip != null && ImGui.IsItemHovered()) ImGui.SetTooltip(tip);
            if (ch) v = S(n);
            SelChanged(ch);
            return ch;
        }

        private bool SelDrag(string label, ref float v, float speed, float min, float max, string fmt = "%.3f", string tip = null)
        {
            ImGui.SetNextItemWidth(-110);
            bool ch = ImGui.DragFloat(label, ref v, speed, min, max, fmt);
            if (tip != null && ImGui.IsItemHovered()) ImGui.SetTooltip(tip);
            SelChanged(ch);
            return ch;
        }

        private bool SelInt(string label, ref int v, string tip = null)
        {
            ImGui.SetNextItemWidth(-110);
            bool ch = ImGui.InputInt(label, ref v);
            if (tip != null && ImGui.IsItemHovered()) ImGui.SetTooltip(tip);
            SelChanged(ch);
            return ch;
        }

        private bool SelHash(string label, ref MetaHash h, string tip = null)
        {
            string txt = h.ToString();
            ImGui.SetNextItemWidth(-110);
            bool ch = ImGui.InputText(label, ref txt, 128, ImGuiInputTextFlags.EnterReturnsTrue);
            if (tip != null && ImGui.IsItemHovered()) ImGui.SetTooltip(tip);
            if (ch)
            {
                txt = txt.Trim();
                uint val;
                if (txt.Length == 0) val = 0;
                else if (txt.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && uint.TryParse(txt.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out var hx)) val = hx;
                else if (uint.TryParse(txt, out var dec)) val = dec;
                else { val = JenkHash.GenHash(txt.ToLowerInvariant()); JenkIndex.Ensure(txt.ToLowerInvariant()); }
                h = new MetaHash(val);
            }
            SelChanged(ch);
            return ch;
        }

        private void SelSection(string title)
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextDisabled(title);
        }

        private void DrawCarGenPage(YmapCarGen cg)
        {
            SelSection("CAR GENERATOR");
            Row("Ymap", cg.Ymap?.Name ?? "");
            var d = cg._CCarGen;
            bool ch = false;
            var model = d.carModel; if (SelHash("Car model", ref model, "Vehicle model name (or hash). Empty = from the pop group.")) { d.carModel = model; ch = true; }
            var pop = d.popGroup; if (SelHash("Pop group", ref pop, "Population group used when no model is set")) { d.popGroup = pop; ch = true; }
            int flags = (int)d.flags; if (SelInt("Flags", ref flags)) { d.flags = (uint)flags; ch = true; }
            var pos = d.position; if (SelDrag3("##cgpos", ref pos, 0.05f, "Position  (X Y Z)")) { cg.SetPosition(pos); d = cg._CCarGen; }
            float ox = d.orientX, oy = d.orientY;
            if (SelDrag("Orient X", ref ox, 0.05f, -1000, 1000)) { d.orientX = ox; ch = true; }
            if (SelDrag("Orient Y", ref oy, 0.05f, -1000, 1000)) { d.orientY = oy; ch = true; }
            float len = d.perpendicularLength;
            if (SelDrag("Length", ref len, 0.05f, 0.1f, 200.0f, "%.2f m", "perpendicularLength - the car gen box and arrow")) { cg.SetLength(len); d = cg._CCarGen; }
            int c1 = d.bodyColorRemap1, c2 = d.bodyColorRemap2, c3 = d.bodyColorRemap3, c4 = d.bodyColorRemap4;
            if (SelInt("Body colour 1", ref c1)) { d.bodyColorRemap1 = c1; ch = true; }
            if (SelInt("Body colour 2", ref c2)) { d.bodyColorRemap2 = c2; ch = true; }
            if (SelInt("Body colour 3", ref c3)) { d.bodyColorRemap3 = c3; ch = true; }
            if (SelInt("Body colour 4", ref c4)) { d.bodyColorRemap4 = c4; ch = true; }
            int liv = d.livery; if (SelInt("Livery", ref liv)) { d.livery = (sbyte)Math.Clamp(liv, -128, 127); ch = true; }
            if (ch)
            {
                cg._CCarGen = d;
                cg.CalcOrientation();
            }
            var o = WorldEditor.ToEulerDegrees(cg.Orientation);
            Row("Heading", $"{o.Z:0.#} deg");
            Row("Name", cg.NameString());
            ImGui.Spacing();
            if (ImGui.Button("Delete car generator")) RequestWorldSelectionDelete = true;
        }

        private static readonly string[] LodLightTypeNames = { "Point", "Spot", "Capsule" };

        private void DrawLodLightPage(YmapLODLight l)
        {
            SelSection("LOD LIGHT");
            Row("Ymap", l.Ymap?.Name ?? "");
            Row("Index", l.Index.ToString());
            Row("Distant ymap", l.DistLodLights?.Ymap?.Name ?? "");
            var pos = l.Position;
            if (SelDrag3("##llpos", ref pos, 0.05f, "Position  (X Y Z)")) l.SetPosition(pos);
            var dir = l.Direction;
            if (SelDrag3("##lldir", ref dir, 0.01f, "Direction  (X Y Z)"))
            {
                if (dir.LengthSquared() > 1e-8f) dir.Normalize(); else dir = -SDX.Vector3.UnitZ;
                l.Direction = dir; l.UpdateTangentsAndOrientation();
            }
            int type = l.Type == LightType.Point ? 0 : l.Type == LightType.Spot ? 1 : l.Type == LightType.Capsule ? 2 : 0;
            ImGui.SetNextItemWidth(-110);
            if (ImGui.Combo("Type", ref type, LodLightTypeNames, LodLightTypeNames.Length))
            {
                l.Type = type == 0 ? LightType.Point : type == 1 ? LightType.Spot : LightType.Capsule;
                l.UpdateTangentsAndOrientation();
                selPageChanged = true;
            }
            var col = l.Colour;
            var rgb = new Vector3(col.R / 255f, col.G / 255f, col.B / 255f);
            ImGui.SetNextItemWidth(-110);
            if (ImGui.ColorEdit3("Colour", ref rgb)) { l.SetColour(new SDX.Color((byte)(rgb.X * 255), (byte)(rgb.Y * 255), (byte)(rgb.Z * 255), col.A)); selPageChanged = true; col = l.Colour; }
            int inten = col.A;
            if (SelInt("Intensity", ref inten, "The colour's alpha - the light's intensity")) l.SetColour(new SDX.Color(col.R, col.G, col.B, (byte)Math.Clamp(inten, 0, 255)));
            float fo = l.Falloff; if (SelDrag("Falloff", ref fo, 0.1f, 0, 10000, "%.2f m")) { l.Falloff = fo; l.Scale = new SDX.Vector3(fo); }
            float fe = l.FalloffExponent; if (SelDrag("Falloff exp", ref fe, 0.05f, 0, 1000)) l.FalloffExponent = fe;
            int hash = (int)l.Hash; if (SelInt("Hash", ref hash, "The entity light this one stands in for")) l.Hash = (uint)hash;
            int inner = l.ConeInnerAngle; if (SelInt("Cone inner", ref inner, "0-255, pi/255 each")) l.ConeInnerAngle = (byte)Math.Clamp(inner, 0, 255);
            int outer = l.ConeOuterAngleOrCapExt; if (SelInt("Cone outer / cap", ref outer, "Spot: outer angle (0-255). Capsule: extent")) l.ConeOuterAngleOrCapExt = (byte)Math.Clamp(outer, 0, 255);
            int cor = l.CoronaIntensity; if (SelInt("Corona", ref cor)) l.CoronaIntensity = (byte)Math.Clamp(cor, 0, 255);
            int flagsAll = (int)l.TimeAndStateFlags;
            ImGui.SetNextItemWidth(-110);
            if (ImGui.InputInt("Time+state flags", ref flagsAll)) { l.TimeAndStateFlags = (uint)flagsAll; selPageChanged = true; }
            uint tf = l.TimeFlags;
            ImGui.TextDisabled("Hours");
            for (int h = 0; h < 24; h++)
            {
                bool on = (tf & (1u << h)) != 0;
                if (h % 12 != 0) ImGui.SameLine(0, 2);
                if (ImGui.Checkbox($"##h{h}", ref on))
                {
                    tf = on ? (tf | (1u << h)) : (tf & ~(1u << h));
                    l.TimeFlags = tf; selPageChanged = true;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{h:00}:00");
            }
            int st1 = (int)l.StateFlags1, st2 = (int)l.StateFlags2;
            if (SelInt("State flags 1", ref st1)) l.StateFlags1 = (uint)Math.Clamp(st1, 0, 3);
            if (SelInt("State flags 2", ref st2)) l.StateFlags2 = (uint)Math.Clamp(st2, 0, 7);
            ImGui.Spacing();
            if (ImGui.Button("Delete LOD light")) RequestWorldSelectionDelete = true;
        }

        private void DrawTcmPage(YmapTimeCycleModifier t)
        {
            SelSection("TIME CYCLE MODIFIER");
            Row("Ymap", t.Ymap?.Name ?? "");
            var d = t.CTimeCycleModifier;
            bool ch = false;
            var name = d.name; if (SelHash("Name", ref name, "The timecycle modifier this box applies")) { d.name = name; ch = true; }
            var mn = d.minExtents; if (SelDrag3("##tcmmin", ref mn, 0.1f, "Min extents")) { d.minExtents = mn; ch = true; }
            var mx = d.maxExtents; if (SelDrag3("##tcmmax", ref mx, 0.1f, "Max extents")) { d.maxExtents = mx; ch = true; }
            float pc = d.percentage; if (SelDrag("Percentage", ref pc, 0.5f, 0, 100, "%.1f")) { d.percentage = pc; ch = true; }
            float rg = d.range; if (SelDrag("Range", ref rg, 0.1f, 0, 1000, "%.2f")) { d.range = rg; ch = true; }
            int sh = (int)d.startHour, eh = (int)d.endHour;
            if (SelInt("Start hour", ref sh)) { d.startHour = (uint)Math.Clamp(sh, 0, 24); ch = true; }
            if (SelInt("End hour", ref eh)) { d.endHour = (uint)Math.Clamp(eh, 0, 24); ch = true; }
            if (ch)
            {
                t.CTimeCycleModifier = d;
                t.BBMin = d.minExtents; t.BBMax = d.maxExtents;
            }
            if (t.TimeCycleModData != null) Row("Modifier", (t.TimeCycleModData.name ?? "") + $"   ({t.TimeCycleModData.numMods} values)");
        }

        private void DrawGrassPage(YmapGrassInstanceBatch gb)
        {
            SelSection("GRASS BATCH");
            Row("Ymap", gb.Ymap?.Name ?? "");
            var b = gb.Batch;
            Row("Archetype", (gb.Archetype?.Name ?? b.archetypeName.ToString()));
            Row("Position", $"{gb.Position.X:0.##}, {gb.Position.Y:0.##}, {gb.Position.Z:0.##}");
            Row("Instances", (gb.Instances?.Length ?? 0).ToString());
            Row("Lod dist", b.lodDist.ToString());
            Row("Lod fade start", $"{b.LodFadeStartDist:0.##}");
            Row("Lod inst fade", $"{b.LodInstFadeRange:0.##}");
            Row("Scale range", $"{b.ScaleRange.X:0.###}, {b.ScaleRange.Y:0.###}, {b.ScaleRange.Z:0.###}");
            Row("Orient to terrain", $"{b.OrientToTerrain:0.###}");
            Row("Extents min", $"{gb.AABBMin.X:0.##}, {gb.AABBMin.Y:0.##}, {gb.AABBMin.Z:0.##}");
            Row("Extents max", $"{gb.AABBMax.X:0.##}, {gb.AABBMax.Y:0.##}, {gb.AABBMax.Z:0.##}");
            Row("Radius", $"{gb.Radius:0.##}");
        }

        private void DrawBoxOccluderPage(YmapBoxOccluder b)
        {
            SelSection("BOX OCCLUDER");
            Row("Ymap", b.Ymap?.Name ?? "");
            Row("Index", b.Index.ToString());
            var pos = b.Position; if (SelDrag3("##bopos", ref pos, 0.05f, "Center  (X Y Z)")) { b.Position = pos; b.UpdateBoxStruct(); }
            var size = b.Size; if (SelDrag3("##bosize", ref size, 0.05f, "Size  (length width height)")) { b.SetSize(SDX.Vector3.Max(size, new SDX.Vector3(0.25f))); b.UpdateBoxStruct(); }
            var dir = b.Orientation.Multiply(SDX.Vector3.UnitX);
            Row("Sin/Cos Z", $"{dir.X * 0.5f:0.####}, {dir.Y * 0.5f:0.####}");
            var e = WorldEditor.ToEulerDegrees(b.Orientation);
            float yaw = e.Z;
            if (SelDrag("Heading", ref yaw, 0.5f, -360, 360, "%.1f deg")) { b.Orientation = SDX.Quaternion.RotationYawPitchRoll(0, 0, SDX.MathUtil.DegreesToRadians(yaw)); b.UpdateBoxStruct(); }
            ImGui.Spacing();
            if (ImGui.Button("Delete box occluder")) RequestWorldSelectionDelete = true;
        }

        private void DrawOccludeTriPage(YmapOccludeModelTriangle t)
        {
            SelSection("OCCLUDE MODEL TRIANGLE");
            Row("Ymap", t.Ymap?.Name ?? "");
            Row("Model", (t.Model?.Index ?? 0).ToString() + "   " + (t.Model?.ToString() ?? ""));
            Row("Triangle", t.Index.ToString());
            Row("Corner 1", $"{t.Corner1.X:0.##}, {t.Corner1.Y:0.##}, {t.Corner1.Z:0.##}");
            Row("Corner 2", $"{t.Corner2.X:0.##}, {t.Corner2.Y:0.##}, {t.Corner2.Z:0.##}");
            Row("Corner 3", $"{t.Corner3.X:0.##}, {t.Corner3.Y:0.##}, {t.Corner3.Z:0.##}");
            var c = t.Center;
            Row("Center", $"{c.X:0.##}, {c.Y:0.##}, {c.Z:0.##}");
            if (t.Model != null)
            {
                int flags = (int)(uint)t.Model.Flags;
                if (SelInt("Model flags", ref flags)) t.Model.Flags = (uint)flags;
                var m = t.Model._OccludeModel;
                Row("Model bounds", $"({m.bmin.X:0.#}, {m.bmin.Y:0.#}, {m.bmin.Z:0.#}) - ({m.bmax.X:0.#}, {m.bmax.Y:0.#}, {m.bmax.Z:0.#})");
            }
            ImGui.Spacing();
            if (ImGui.Button("Delete triangle")) RequestWorldSelectionDelete = true;
        }

        private void DrawWaterQuadPage(in WorldSelection s)
        {
            BaseWaterQuad q = (BaseWaterQuad)s.WaterQuad ?? (BaseWaterQuad)s.CalmingQuad ?? s.WaveQuad;
            SelSection(s.WaterQuad != null ? "WATER QUAD" : s.CalmingQuad != null ? "WATER CALMING QUAD" : "WATER WAVE QUAD");
            Row("Index", q.xmlNodeIndex.ToString());
            Row("Min X / Y", $"{q.minX:0.##}, {q.minY:0.##}");
            Row("Max X / Y", $"{q.maxX:0.##}, {q.maxY:0.##}");
            Row("Size", $"{q.maxX - q.minX:0.#} x {q.maxY - q.minY:0.#} m");
            Row("Z", q.z.HasValue ? $"{q.z.Value:0.###}" : "(none)");
            if (s.WaterQuad != null)
            {
                var w = s.WaterQuad;
                Row("Type", w.Type.ToString());
                Row("Invisible", w.IsInvisible.ToString());
                Row("Limited depth", w.HasLimitedDepth.ToString());
                Row("a1..a4", $"{w.a1:0.##}, {w.a2:0.##}, {w.a3:0.##}, {w.a4:0.##}");
                Row("No stencil", w.NoStencil.ToString());
            }
            else if (s.CalmingQuad != null)
            {
                Row("Dampening", $"{s.CalmingQuad.fDampening:0.###}");
            }
            else if (s.WaveQuad != null)
            {
                Row("Amplitude", $"{s.WaveQuad.Amplitude:0.###}");
                Row("Direction", $"{s.WaveQuad.XDirection:0.###}, {s.WaveQuad.YDirection:0.###}");
            }
            ImGui.TextDisabled("Read only - water.xml is not written by this build.");
        }

        private void DrawCollisionPage(in WorldSelection s)
        {
            SelSection("COLLISION");
            var b = s.CollisionBounds;
            if (s.EntityDef != null) Row("Entity", s.EntityDef.Archetype?.Name ?? s.EntityDef.Name ?? "");
            if (b != null)
            {
                Row("Bounds", b.GetName());
                Row("Type", b.Type.ToString());
                var p = b.Position; Row("Position", $"{p.X:0.###}, {p.Y:0.###}, {p.Z:0.###}");
                var e = WorldEditor.ToEulerDegrees(b.Orientation); Row("Rotation", $"{e.X:0.#}, {e.Y:0.#}, {e.Z:0.#} deg");
                var sc = b.Scale; Row("Scale", $"{sc.X:0.###}, {sc.Y:0.###}, {sc.Z:0.###}");
                Row("Box min", $"{b.BoxMin.X:0.##}, {b.BoxMin.Y:0.##}, {b.BoxMin.Z:0.##}");
                Row("Box max", $"{b.BoxMax.X:0.##}, {b.BoxMax.Y:0.##}, {b.BoxMax.Z:0.##}");
                if (b is BoundGeometry bg)
                {
                    Row("Polygons", (bg.Polygons?.Length ?? 0).ToString());
                    Row("Vertices", (bg.Vertices?.Length ?? 0).ToString());
                }
                if (b.Parent != null) Row("Parent", b.Parent.Type.ToString());
            }
            if (s.CollisionPoly != null)
            {
                var poly = s.CollisionPoly;
                SelSection("POLYGON");
                Row("Index", poly.Index.ToString());
                Row("Type", poly.Type.ToString());
                var mat = poly.Material;
                Row("Material", BoundsMaterialTypes.GetMaterialName(mat.Type) + "   # " + mat.Type.Index);
                Row("Mat flags", mat.Flags.ToString());
                Row("Room / proc", $"{mat.RoomId} / {mat.ProceduralId}");
                var pp = poly.Position; Row("Position", $"{pp.X:0.###}, {pp.Y:0.###}, {pp.Z:0.###}");
                if (poly.VertexIndices != null) Row("Vertex indices", string.Join(", ", poly.VertexIndices));
            }
            if (s.CollisionVertex != null)
            {
                SelSection("VERTEX");
                Row("Index", s.CollisionVertex.Index.ToString());
                var vp = s.CollisionVertex.Position; Row("Position", $"{vp.X:0.###}, {vp.Y:0.###}, {vp.Z:0.###}");
            }
            Row("Hit distance", $"{s.HitDist:0.##} m");
            ImGui.TextDisabled("Read only - .ybn is not written by this build.");
        }
    }
}

