using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using CodeWalker.GameFiles;
using ImGuiNET;
using SDX = SharpDX;

namespace RageLightEditor.Editor
{
    public partial class ProjectWindow
    {
        public CwProject Project;
        public bool Visible;

        public YmapFile CurrentYmap;
        public YtypFile CurrentYtyp;
        public YmapEntityDef CurrentEntity;
        public Archetype CurrentArchetype;
        public MCMloRoomDef CurrentRoom;
        public MCMloPortalDef CurrentPortal;
        public MCMloEntitySet CurrentEntitySet;
        private object currentItem;

        public bool RequestNewProject, RequestOpenProject, RequestSaveProject, RequestSaveProjectAs, RequestCloseProject;
        public bool CloseViaWindow_V22;
        public bool RequestNewYmap, RequestSaveYmap, RequestSaveYmapAs, RequestRemoveYmap;
        public bool RequestNewYtyp, RequestSaveYtyp, RequestSaveYtypAs, RequestRemoveYtyp;
        public bool RequestNewEntity, RequestDeleteEntity, RequestGoToEntity, RequestAddSelectedToProject;
        public bool RequestNewArchetype, RequestDeleteArchetype, RequestArchetypesFromYdrs;
        public bool RequestSaveAll, RequestCalcAllExtents, RequestCalcAllFlags;
        public bool RequestImportMenyoo, RequestImportYmapXml;
        public bool RequestPropsPanel, RequestManifestGenerator;
        public bool RequestNewMloEntity, RequestDeleteEntitySet, RequestNewEntitySet;
        public bool RequestOpenAny;
        public bool RequestSaveManifest;
        public string ManifestText;
        private sealed class ManifestPage { }
        private readonly ManifestPage manifestPage = new ManifestPage();
        public void ShowManifest(string xml) { ManifestText = xml; currentItem = manifestPage; Visible = true; }
        public YmapEntityDef EntityChangedInPage;
        public YmapEntityDef WorldSelectionToShow;
        public string Status = "";

        public bool HideGtaMap;
        public bool RenderProjectItems = true;

        private float explorerWidth = 300.0f;
        private string ymapNameEdit, ymapParentEdit;
        private string archNameEdit, archAssetEdit, archTxdEdit, archClipEdit, archDrawDictEdit, archPhysEdit;
        private object editsFor;

        public bool Minimized;
        public bool Maximized;
        private Vector2 restorePos, restoreSize;
        private bool restoreValid;

        public void Draw(float displayW, float displayH)
        {
            if (!Visible || Minimized || Detached || ShowsDocked) return;

            ImGui.SetNextWindowSize(new Vector2(Math.Min(1180, displayW - 80), Math.Min(760, displayH - 80)), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(displayW * 0.5f, displayH * 0.5f), ImGuiCond.FirstUseEver, new Vector2(0.5f, 0.5f));
            if (Maximized)
            {
                ImGui.SetNextWindowPos(new Vector2(0, 0), ImGuiCond.Always);
                ImGui.SetNextWindowSize(new Vector2(displayW, displayH), ImGuiCond.Always);
            }
            else if (restoreValid)
            {
                ImGui.SetNextWindowPos(restorePos, ImGuiCond.Always);
                ImGui.SetNextWindowSize(restoreSize, ImGuiCond.Always);
                restoreValid = false;
            }
            string title = (Project == null ? "Project" : (Project.AnyUnsaved ? "*" : "") + Project.Name) + "###ProjectWindow";
            ImGui.SetNextWindowSizeConstraints(new Vector2(560, 360), new Vector2(displayW, displayH));
            var wflags = ImGuiWindowFlags.MenuBar | (Maximized ? ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove : ImGuiWindowFlags.None);
            bool wasVisible = Visible;
            bool open = ImGui.Begin(title, ref Visible, wflags);
            if (wasVisible && !Visible && Project != null) { Visible = true; RequestCloseProject = true; CloseViaWindow_V22 = true; }
            if (!open)
            {
                ImGui.End();
                return;
            }
            if (!Maximized) { lastPos = ImGui.GetWindowPos(); lastSize = ImGui.GetWindowSize(); }

            DrawContents();

            ImGui.End();
        }

        private void DrawContents()
        {
            HandleShortcuts();
            DrawMenuBar();
            DrawToolStrip();

            var avail = ImGui.GetContentRegionAvail();
            if (ShowExplorer)
            {
                ImGui.BeginChild("##projexplorer", new Vector2(explorerWidth, avail.Y), ImGuiChildFlags.Borders | ImGuiChildFlags.ResizeX);
                DrawExplorer();
                explorerWidth = ImGui.GetWindowSize().X;
                ImGui.EndChild();
                ImGui.SameLine();
            }
            ImGui.BeginChild("##projpage", new Vector2(0, avail.Y), ImGuiChildFlags.Borders);
            DrawPage();
            ImGui.EndChild();
        }

        private Vector2 lastPos, lastSize;

        private void DrawWindowButtons()
        {
            if (embedded) { DrawEmbeddedButtons_U22(); return; }
            if (Detached) { DrawDetachedWindowButtons(); return; }
            float right = ImGui.GetWindowWidth() - 8;
            ImGui.SameLine(Math.Max(right - 160, ImGui.GetCursorPosX() + 8));
            DrawDockButton_U22();
            ImGui.SameLine();
            if (ImGui.SmallButton("^##pwdetach")) RequestDetach = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Detach into its own window (drag it to another monitor)");
            ImGui.SameLine();
            if (ImGui.SmallButton("_##pwmin")) Minimized = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Minimise to the taskbar");
            ImGui.SameLine();
            if (ImGui.SmallButton(Maximized ? "o##pwmax" : "[]##pwmax"))
            {
                if (!Maximized) { restorePos = lastPos; restoreSize = lastSize; }
                Maximized = !Maximized;
                if (!Maximized) restoreValid = true;
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(Maximized ? "Restore" : "Maximise");
            ImGui.SameLine();
            if (ImGui.SmallButton("x##pwclose"))
            {
                if (Project != null) { RequestCloseProject = true; CloseViaWindow_V22 = true; }
                else Visible = false;
            }
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(Project != null
                ? "Close the project (asks about saving). The world goes back to the game's files."
                : "Close (Ctrl+Shift+P brings it back)");
        }

        public void DrawTaskbar(float displayW, float displayH, float bottomInset)
        {
            if (!Visible || !Minimized || ShowsDocked) return;
            const float h = 26.0f;
            ImGui.SetNextWindowPos(new Vector2(0, displayH - bottomInset - h), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(displayW, h), ImGuiCond.Always);
            var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking |
                        ImGuiWindowFlags.NoBackground;
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(6, 2));
            if (ImGui.Begin("##taskbar", flags))
            {
                string label = (Project == null ? "Project" : (Project.AnyUnsaved ? "*" : "") + Project.Name);
                if (ImGui.Button("[] " + label + "##taskbarproj")) Minimized = false;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Restore the project window");
            }
            ImGui.End();
            ImGui.PopStyleVar();
        }

        private void DrawMenuBar()
        {
            if (!ImGui.BeginMenuBar()) return;
            bool hasProj = Project != null;
            DrawFileMenu();
            DrawEditMenu();
            DrawViewMenu();
            if (ImGui.BeginMenu("Ymap", hasProj))
            {
                bool hasYmap = CurrentYmap != null;
                if (ImGui.MenuItem("New Entity", null, false, hasYmap)) RequestNewEntity = true;
                if (ImGui.MenuItem("Delete Entity", null, false, CurrentEntity != null)) RequestDeleteEntity = true;
                ImGui.Separator();
                if (ImGui.MenuItem("Go to Ymap", null, false, hasYmap)) RequestGoToSelected = true;
                ImGui.Separator();
                if (ImGui.MenuItem("Save Ymap", null, false, hasYmap)) RequestSaveYmap = true;
                if (ImGui.MenuItem("Save Ymap As...", null, false, hasYmap)) RequestSaveYmapAs = true;
                if (ImGui.MenuItem("Remove Ymap from Project", null, false, hasYmap)) RequestRemoveYmap = true;
                ImGui.Separator();
                if (ImGui.MenuItem("Calculate All Extents")) RequestCalcAllExtents = true;
                if (ImGui.MenuItem("Calculate All Flags")) RequestCalcAllFlags = true;
                ImGui.Separator();
                if (ImGui.MenuItem("Import Menyoo XML...")) RequestImportMenyoo = true;
                if (ImGui.MenuItem("Import Ymap XML...")) RequestImportYmapXml = true;
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Ytyp", hasProj))
            {
                bool hasYtyp = CurrentYtyp != null;
                if (ImGui.MenuItem("New Archetype", null, false, hasYtyp)) RequestNewArchetype = true;
                if (ImGui.MenuItem("New Archetypes from YDRs...", null, false, hasYtyp)) RequestArchetypesFromYdrs = true;
                if (ImGui.MenuItem("Delete Archetype", null, false, CurrentArchetype != null)) RequestDeleteArchetype = true;
                ImGui.Separator();
                bool isMlo = CurrentArchetype is MloArchetype;
                if (ImGui.BeginMenu("Mlo", isMlo))
                {
                    if (ImGui.MenuItem("New Entity", null, false, CurrentEntitySet != null || CurrentRoom != null || CurrentPortal != null)) RequestNewMloEntity = true;
                    if (ImGui.MenuItem("New Entity Set", null, false, isMlo)) RequestNewEntitySet = true;
                    if (ImGui.MenuItem("Delete Entity Set", null, false, CurrentEntitySet != null)) RequestDeleteEntitySet = true;
                    ImGui.EndMenu();
                }
                if (ImGui.MenuItem("Go to Interior", null, false, isMlo)) RequestGoToSelected = true;
                ImGui.Separator();
                if (ImGui.MenuItem("Save Ytyp", null, false, hasYtyp)) RequestSaveYtyp = true;
                if (ImGui.MenuItem("Save Ytyp As...", null, false, hasYtyp)) RequestSaveYtypAs = true;
                if (ImGui.MenuItem("Remove Ytyp from Project", null, false, hasYtyp)) RequestRemoveYtyp = true;
                ImGui.EndMenu();
            }
            if (ImGui.BeginMenu("Tools"))
            {
                if (ImGui.MenuItem("Props Panel...")) RequestPropsPanel = true;
                if (ImGui.MenuItem("Manifest Generator...")) RequestManifestGenerator = true;
                ImGui.Separator();
                if (ImGui.MenuItem("Import Menyoo XML...")) RequestImportMenyoo = true;
                ImGui.Separator();
                if (ImGui.MenuItem("Delete Grass...")) RequestDeleteGrass_S5 = true;
                ImGui.EndMenu();
            }
            DrawOptionsMenu();
            DrawWindowButtons();
            ImGui.EndMenuBar();
        }

        private void DrawExplorer()
        {
            ImGui.TextDisabled("PROJECT EXPLORER");
            if (Project == null)
            {
                DrawEmptyState_U22();
                return;
            }

            var rootFlags = ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth;
            if (currentItem == Project) rootFlags |= ImGuiTreeNodeFlags.Selected;
            bool open = ImGui.TreeNodeEx((Project.AnyUnsaved ? "*" : "") + Project.Name + "###projroot", rootFlags);
            if (ImGui.IsItemClicked()) Select(Project);
            if (!open) return;

            var revealEnt = revealItem as YmapEntityDef;
            var revealYmap = revealItem as YmapFile ?? revealEnt?.Ymap
                             ?? (revealItem as YmapCarGen)?.Ymap ?? (revealItem as YmapLODLight)?.Ymap
                             ?? (revealItem as YmapBoxOccluder)?.Ymap ?? (revealItem as YmapOccludeModelTriangle)?.Ymap
                             ?? (revealItem as YmapTimeCycleModifier)?.Ymap ?? (revealItem as YmapGrassInstanceBatch)?.Ymap;
            var revealYtyp = revealItem as YtypFile ?? (revealItem as Archetype)?.Ytyp
                             ?? (revealItem as MCMloRoomDef)?.OwnerMlo?.Ytyp ?? (revealItem as MCMloPortalDef)?.OwnerMlo?.Ytyp
                             ?? (revealItem as MCMloEntitySet)?.OwnerMlo?.Ytyp ?? (revealItem as MCEntityDef)?.OwnerMlo?.Ytyp
                             ?? (revealEnt?.Ymap == null ? revealEnt?.MloParent?.Archetype?.Ytyp : null);
            var revealArch = revealItem as Archetype ?? (revealItem as MCMloRoomDef)?.OwnerMlo ?? (revealItem as MCMloPortalDef)?.OwnerMlo
                             ?? (revealItem as MCMloEntitySet)?.OwnerMlo ?? (revealItem as MCEntityDef)?.OwnerMlo
                             ?? (revealEnt?.Ymap == null ? revealEnt?.MloParent?.Archetype : null);
            if (ImGui.TreeNodeEx($"Ymap Files ({Project.YmapFiles.Count})###ymaps", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth))
            {
                foreach (var y in Project.YmapFiles.ToList())
                {
                    var f = ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.OpenOnArrow;
                    if (currentItem == y) f |= ImGuiTreeNodeFlags.Selected;
                    bool revealHere = revealYmap != null && ReferenceEquals(revealYmap, y);
                    if (revealHere && revealEnt != null) ImGui.SetNextItemOpen(true);
                    bool yo = ImGui.TreeNodeEx((y.HasChanged ? "*" : "") + (y.Name ?? "ymap") + "###y" + y.GetHashCode(), f);
                    if (revealHere && revealEnt == null) { ImGui.SetScrollHereY(); revealItem = null; }
                    if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) Select(y);
                    if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) { Select(y); RequestGoToSelected = true; }
                    DrawYmapContext(y);
                    if (yo)
                    {
                        var ents = y.AllEntities;
                        int n = ents?.Length ?? 0;
                        if (revealHere && revealEnt != null) ImGui.SetNextItemOpen(true);
                        if (ImGui.TreeNodeEx($"Entities ({n})###ye{y.GetHashCode()}", ImGuiTreeNodeFlags.SpanAvailWidth))
                        {
                            for (int i = 0; i < n; i++)
                            {
                                var e = ents[i];
                                if (e == null) continue;
                                var lf = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.SpanAvailWidth;
                                if (currentItem == e || IsMultiSelected_V26(e)) lf |= ImGuiTreeNodeFlags.Selected;
                                var en = e.Archetype?.Name ?? e._CEntityDef.archetypeName.ToString();
                                var label = DisplayEntityIndexes ? $"[{i}] {en}" : en;
                                ImGui.TreeNodeEx($"{label}###e{y.GetHashCode()}_{i}", lf);
                                if (revealHere && ReferenceEquals(revealEnt, e)) { ImGui.SetScrollHereY(); revealItem = null; }
                                if (ImGui.IsItemClicked())
                                {
                                    if (!ClickEntity_V26(e, y, ents, i)) Select(e);
                                }
                                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                                { Select(e); RequestGoToEntity = true; }
                                DrawEntityContext(e, $"##ectx{y.GetHashCode()}_{i}");
                            }
                            ImGui.TreePop();
                        }
                        int hy = y.GetHashCode();
                        DrawCountNode("Car Generators", y.CarGenerators?.Length ?? 0, $"ycg{hy}");
                        DrawCountNode("LOD Lights", y.LODLights?.LodLights?.Length ?? 0, $"yll{hy}");
                        DrawCountNode("Box Occluders", y.BoxOccluders?.Length ?? 0, $"ybo{hy}");
                        DrawCountNode("Occlude Models", y.OccludeModels?.Length ?? 0, $"yom{hy}");
                        DrawCountNode("Grass Batches", y.GrassInstanceBatches?.Length ?? 0, $"ygb{hy}");
                        ImGui.TreePop();
                    }
                }
                ImGui.TreePop();
            }

            if (ImGui.TreeNodeEx($"Ytyp Files ({Project.YtypFiles.Count})###ytyps", ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.SpanAvailWidth))
            {
                foreach (var t in Project.YtypFiles.ToList())
                {
                    var f = ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.OpenOnArrow;
                    if (currentItem == t) f |= ImGuiTreeNodeFlags.Selected;
                    bool revealHere = revealYtyp != null && ReferenceEquals(revealYtyp, t);
                    if (revealHere && revealArch != null) ImGui.SetNextItemOpen(true);
                    bool to = ImGui.TreeNodeEx((t.HasChanged ? "*" : "") + (t.Name ?? "ytyp") + "###t" + t.GetHashCode(), f);
                    if (revealHere && revealArch == null) { ImGui.SetScrollHereY(); revealItem = null; }
                    if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) Select(t);
                    DrawYtypContext(t);
                    if (to)
                    {
                        var archs = t.AllArchetypes;
                        int n = archs?.Length ?? 0;
                        if (revealHere && revealArch != null) ImGui.SetNextItemOpen(true);
                        if (ImGui.TreeNodeEx($"Archetypes ({n})###ta{t.GetHashCode()}", ImGuiTreeNodeFlags.SpanAvailWidth))
                        {
                            for (int i = 0; i < n; i++)
                            {
                                var a = archs[i];
                                if (a == null) continue;
                                var mlo = a as MloArchetype;
                                var af = ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.OpenOnArrow;
                                if (mlo == null) af |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
                                if (currentItem == a) af |= ImGuiTreeNodeFlags.Selected;
                                bool revealArchHere = revealHere && ReferenceEquals(revealArch, a);
                                if (revealArchHere && mlo != null && !ReferenceEquals(revealItem, a)) ImGui.SetNextItemOpen(true);
                                bool ao = ImGui.TreeNodeEx($"{a.Name}###a{t.GetHashCode()}_{i}", af);
                                if (revealArchHere) { ImGui.SetScrollHereY(); if (ReferenceEquals(revealItem, a) || mlo == null) revealItem = null; }
                                if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) Select(a);
                                if (mlo != null && ao)
                                {
                                    DrawMloBranch(mlo, i);
                                    ImGui.TreePop();
                                }
                            }
                            ImGui.TreePop();
                        }
                        ImGui.TreePop();
                    }
                }
                ImGui.TreePop();
            }

            DrawFileFolders();
            ImGui.TreePop();
            revealItem = null;
        }

        private object revealItem;

        public void Reveal(object item) { if (item != null) revealItem = item; }

        private void DrawMloBranch(MloArchetype mlo, int idx)
        {
            var ents = mlo.entities;
            var rooms = mlo.rooms;
            bool revealRoom = revealItem is MCMloRoomDef rr && ReferenceEquals(rr.OwnerMlo, mlo);
            bool revealPortal = revealItem is MCMloPortalDef rp && ReferenceEquals(rp.OwnerMlo, mlo);
            bool revealSet = revealItem is MCMloEntitySet rs && ReferenceEquals(rs.OwnerMlo, mlo);
            var revealEntRoom = RevealRoomOf(mlo); var revealEntPortal = RevealPortalOf(mlo); var revealEntSet = RevealSetOf(mlo);
            if (revealRoom || revealEntRoom != null) ImGui.SetNextItemOpen(true);
            if (rooms != null && ImGui.TreeNodeEx($"Rooms ({rooms.Length})###mr{idx}", ImGuiTreeNodeFlags.SpanAvailWidth))
            {
                for (int i = 0; i < rooms.Length; i++)
                {
                    var r = rooms[i];
                    if (r == null) continue;
                    var att = r.AttachedObjects;
                    int n = att?.Length ?? 0;
                    var lf = ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.OpenOnArrow;
                    if (n == 0) lf |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
                    if (currentItem == r) lf |= ImGuiTreeNodeFlags.Selected;
                    if (ReferenceEquals(revealEntRoom, r)) ImGui.SetNextItemOpen(true);
                    bool ro = ImGui.TreeNodeEx($"{r.Index}: {r.RoomName}  ({n})###room{idx}_{i}", lf);
                    if (revealRoom && ReferenceEquals(revealItem, r)) { ImGui.SetScrollHereY(); revealItem = null; }
                    if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) Select(r);
                    if (n > 0 && ro)
                    {
                        for (int k = 0; k < n; k++)
                            DrawMloEntityLeaf(ents, att[k], $"###roomobj{idx}_{i}_{k}");
                        ImGui.TreePop();
                    }
                }
                ImGui.TreePop();
            }
            var portals = mlo.portals;
            if (revealPortal || revealEntPortal != null) ImGui.SetNextItemOpen(true);
            if (portals != null && ImGui.TreeNodeEx($"Portals ({portals.Length})###mp{idx}", ImGuiTreeNodeFlags.SpanAvailWidth))
            {
                for (int i = 0; i < portals.Length; i++)
                {
                    var p = portals[i];
                    if (p == null) continue;
                    var att = p.AttachedObjects;
                    int n = att?.Length ?? 0;
                    var lf = ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.OpenOnArrow;
                    if (n == 0) lf |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
                    if (currentItem == p) lf |= ImGuiTreeNodeFlags.Selected;
                    if (ReferenceEquals(revealEntPortal, p)) ImGui.SetNextItemOpen(true);
                    bool po = ImGui.TreeNodeEx($"{p.Name}  ({n})###portal{idx}_{i}", lf);
                    if (revealPortal && ReferenceEquals(revealItem, p)) { ImGui.SetScrollHereY(); revealItem = null; }
                    if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) Select(p);
                    if (n > 0 && po)
                    {
                        for (int k = 0; k < n; k++)
                            DrawMloEntityLeaf(ents, att[k], $"###portalobj{idx}_{i}_{k}");
                        ImGui.TreePop();
                    }
                }
                ImGui.TreePop();
            }
            var sets = mlo.entitySets;
            if (revealSet || revealEntSet != null) ImGui.SetNextItemOpen(true);
            if (sets != null && ImGui.TreeNodeEx($"Entity Sets ({sets.Length})###ms{idx}", ImGuiTreeNodeFlags.SpanAvailWidth))
            {
                for (int i = 0; i < sets.Length; i++)
                {
                    var s = sets[i];
                    if (s == null) continue;
                    var sents = s.Entities;
                    int n = sents?.Length ?? 0;
                    var lf = ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.OpenOnArrow;
                    if (n == 0) lf |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
                    if (currentItem == s) lf |= ImGuiTreeNodeFlags.Selected;
                    if (ReferenceEquals(revealEntSet, s)) ImGui.SetNextItemOpen(true);
                    bool so = ImGui.TreeNodeEx($"{s.Name}  ({n})###set{idx}_{i}", lf);
                    if (revealSet && ReferenceEquals(revealItem, s)) { ImGui.SetScrollHereY(); revealItem = null; }
                    if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen()) Select(s);
                    if (n > 0 && so)
                    {
                        for (int k = 0; k < n; k++)
                        {
                            var me = sents[k];
                            if (me == null) continue;
                            var ef = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.SpanAvailWidth;
                            if (IsCurrentMloEntity(me)) ef |= ImGuiTreeNodeFlags.Selected;
                            ImGui.TreeNodeEx($"{me.Name}###setent{idx}_{i}_{k}", ef);
                            if (RevealIsMloEntity(me)) { ImGui.SetScrollHereY(); revealItem = null; }
                            if (ImGui.IsItemClicked()) SelectMloEntity(me);
                        }
                        ImGui.TreePop();
                    }
                }
                ImGui.TreePop();
            }
        }

        private void DrawMloEntityLeaf(MCEntityDef[] ents, uint index, string id)
        {
            if (ents == null || index >= ents.Length) { ImGui.TextDisabled($"  (entity {index} missing)"); return; }
            var me = ents[index];
            if (me == null) return;
            var ef = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.SpanAvailWidth;
            if (IsCurrentMloEntity(me)) ef |= ImGuiTreeNodeFlags.Selected;
            ImGui.TreeNodeEx($"{index}: {me.Name}{id}", ef);
            if (RevealIsMloEntity(me)) { ImGui.SetScrollHereY(); revealItem = null; }
            if (ImGui.IsItemClicked()) SelectMloEntity(me);
            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            { SelectMloEntity(me); if (CurrentEntity != null) RequestGoToEntity = true; }
        }

        private bool IsCurrentMloEntity(MCEntityDef me)
        {
            if (currentItem == me) return true;
            var ce = currentItem as YmapEntityDef;
            if (ce?.MloParent?.MloInstance == null) return false;
            return ReferenceEquals(ce.MloParent.MloInstance.TryGetArchetypeEntity(ce), me) || ReferenceEquals(LiveEntityByIndex(ce.MloParent.MloInstance, me), ce);
        }

        public Func<MloArchetype, MloInstanceData> FindMloInstance;

        public void SelectMloEntity(MCEntityDef me)
        {
            if (me == null) return;
            var inst = FindMloInstance?.Invoke(me.OwnerMlo);
            var live = inst?.TryGetYmapEntity(me) ?? LiveEntityByIndex(inst, me);
            if (live != null) { Select(live); return; }
            Select(me);
        }

        public void Select(object item)
        {
            currentItem = item;
            CurrentEntity = null; CurrentArchetype = null;
            CurrentRoom = null; CurrentPortal = null; CurrentEntitySet = null;
            switch (item)
            {
                case YmapFile y: CurrentYmap = y; break;
                case YtypFile t: CurrentYtyp = t; break;
                case YmapEntityDef e:
                    CurrentEntity = e;
                    if (e.Ymap != null) CurrentYmap = e.Ymap;
                    else if (e.MloParent?.Archetype?.Ytyp != null) CurrentYtyp = e.MloParent.Archetype.Ytyp;
                    break;
                case Archetype a: CurrentArchetype = a; if (a.Ytyp != null) CurrentYtyp = a.Ytyp; break;
                case MCMloRoomDef r: CurrentRoom = r; CurrentYtyp = r.OwnerMlo?.Ytyp ?? CurrentYtyp; break;
                case MCMloPortalDef p: CurrentPortal = p; CurrentYtyp = p.OwnerMlo?.Ytyp ?? CurrentYtyp; break;
                case MCMloEntitySet s: CurrentEntitySet = s; CurrentYtyp = s.OwnerMlo?.Ytyp ?? CurrentYtyp; break;
                case MCEntityDef me: CurrentArchetype = me.OwnerMlo; CurrentYtyp = me.OwnerMlo?.Ytyp ?? CurrentYtyp; break;
            }
            OnSelected_I2(item);
        }
        partial void OnSelected_I2(object item);

        public void ShowWorldSelection(YmapEntityDef e)
        {
            if (e == null) return;
            Select(e);
        }

        private void DrawPage()
        {
            switch (currentItem)
            {
                case null: DrawWelcome(); break;
                case CwProject: DrawProjectPage(); break;
                case YmapFile y: DrawYmapPage(y); break;
                case YtypFile t: DrawYtypPage(t); break;
                case YmapEntityDef e: DrawEntityPage(e); break;
                case Archetype a: DrawArchetypePage(a); break;
                case MCMloRoomDef r: DrawRoomPage(r); break;
                case MCMloPortalDef p: DrawPortalPage(p); break;
                case MCMloEntitySet s: DrawEntitySetPage(s); break;
                case MCEntityDef me: DrawMloEntityDefPage(me); break;
                case YmtFile ymt: DrawScenarioPage(ymt); break;
                case ManifestPage: DrawManifestPage(); break;
            }
        }

        private void DrawManifestPage()
        {
            ImGui.TextDisabled("MANIFEST  (_manifest.ymf)");
            ImGui.TextWrapped("What the game needs to know which ytyps each ymap of the project depends on. " +
                              "Generated from the project as it is now; regenerate after adding files.");
            if (ImGui.Button("Save As...")) RequestSaveManifest = true;
            ImGui.SameLine();
            if (ImGui.Button("Copy")) ImGui.SetClipboardText(ManifestText ?? "");
            ImGui.SameLine();
            if (ImGui.Button("Regenerate")) RequestManifestGenerator = true;
            var text = ManifestText ?? "";
            ImGui.InputTextMultiline("##manifestxml", ref text, 1 << 20, new Vector2(-1, -1), ImGuiInputTextFlags.ReadOnly);
        }

        private void DrawWelcome()
        {
            ImGui.TextDisabled("PROJECT");
            ImGui.TextWrapped("Create a project, then add ymaps and ytyps to it. Files you edit in the " +
                              "project OVERRIDE the game's copy in the world view, so what you see is what " +
                              "you will ship. Select anything in the world to open it here.");
        }

        private void DrawProjectPage()
        {
            ImGui.TextDisabled("PROJECT");
            var name = Project.Name ?? "";
            ImGui.SetNextItemWidth(-90);
            if (ImGui.InputText("Name##pn", ref name, 128)) { Project.Name = name; Project.HasChanged = true; }
            ImGui.TextDisabled("Path: " + (Project.Filepath ?? "(not saved yet)"));
            ImGui.Text($"{Project.YmapFiles.Count} ymap(s), {Project.YtypFiles.Count} ytyp(s)");
        }

        private void DrawYmapPage(YmapFile y)
        {
            if (editsFor != y)
            {
                ymapNameEdit = Path.GetFileNameWithoutExtension(y.Name ?? "");
                ymapParentEdit = y._CMapData.parent.Hash == 0 ? "" : y._CMapData.parent.ToString();
                editsFor = y;
            }
            ImGui.TextDisabled("YMAP");
            ImGui.SetNextItemWidth(-260);
            if (ImGui.InputText("Name##yn", ref ymapNameEdit, 128))
            {
                uint hash = uint.TryParse(ymapNameEdit, out var h) ? h : JenkHash.GenHash(ymapNameEdit);
                if (!uint.TryParse(ymapNameEdit, out _)) JenkIndex.Ensure(ymapNameEdit);
                y.SetName(ymapNameEdit + ".ymap");
                if (!File.Exists(y.FilePath ?? "")) y.FilePath = ymapNameEdit + ".ymap";
                y.HasChanged = true;
            }
            ImGui.SameLine(); ImGui.TextDisabled(".ymap   Hash: " + y._CMapData.name.Hash);

            ImGui.SetNextItemWidth(-260);
            if (ImGui.InputText("Parent##yp", ref ymapParentEdit, 128))
            {
                uint hash = 0;
                if (!string.IsNullOrWhiteSpace(ymapParentEdit))
                {
                    if (!uint.TryParse(ymapParentEdit, out hash)) { hash = JenkHash.GenHash(ymapParentEdit); JenkIndex.Ensure(ymapParentEdit); }
                }
                if (y._CMapData.parent.Hash != hash) { y._CMapData.parent = new MetaHash(hash); y.HasChanged = true; }
            }
            ImGui.SameLine(); ImGui.TextDisabled(".ymap   Hash: " + y._CMapData.parent.Hash);

            ImGui.Spacing();
            ImGui.TextDisabled("CONTENT FLAGS");
            uint cf = y._CMapData.contentFlags;
            bool ch = false;
            ch |= FlagBox("HD (1)", ref cf, 0);           ImGui.SameLine(150); ch |= FlagBox("LOD (2)", ref cf, 1);          ImGui.SameLine(300); ch |= FlagBox("SLOD2+ (4)", ref cf, 2);
            ch |= FlagBox("Interior (8)", ref cf, 3);     ImGui.SameLine(150); ch |= FlagBox("SLOD (16)", ref cf, 4);        ImGui.SameLine(300); ch |= FlagBox("Occlusion (32)", ref cf, 5);
            ch |= FlagBox("Physics (64)", ref cf, 6);     ImGui.SameLine(150); ch |= FlagBox("LOD Lights (128)", ref cf, 7); ImGui.SameLine(300); ch |= FlagBox("Distant Lights (256)", ref cf, 8);
            ch |= FlagBox("Critical (512)", ref cf, 9);   ImGui.SameLine(150); ch |= FlagBox("Grass (1024)", ref cf, 10);
            int cfv = unchecked((int)cf);
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputInt("Value##cfv", ref cfv, 0, 0)) { cf = unchecked((uint)cfv); ch = true; }
            if (ch && cf != y._CMapData.contentFlags) { y._CMapData.contentFlags = cf; y.HasChanged = true; }

            ImGui.TextDisabled("FLAGS");
            uint fl = y._CMapData.flags;
            bool fch = false;
            fch |= FlagBox("Scripted (1)", ref fl, 0); ImGui.SameLine(150); fch |= FlagBox("LOD (2)##f", ref fl, 1);
            int flv = unchecked((int)fl);
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputInt("Value##flv", ref flv, 0, 0)) { fl = unchecked((uint)flv); fch = true; }
            if (fch && fl != y._CMapData.flags) { y._CMapData.flags = fl; y.HasChanged = true; }
            ImGui.SameLine();
            if (ImGui.Button("Calculate all flags")) { if (y.CalcFlags()) y.HasChanged = true; }

            ImGui.Spacing();
            ImGui.TextDisabled("EXTENTS");
            var eMin = y._CMapData.entitiesExtentsMin; var eMax = y._CMapData.entitiesExtentsMax;
            var sMin = y._CMapData.streamingExtentsMin; var sMax = y._CMapData.streamingExtentsMax;
            if (Vec3Field("Entities Extents Min", ref eMin)) { y._CMapData.entitiesExtentsMin = eMin; y.HasChanged = true; }
            if (Vec3Field("Entities Extents Max", ref eMax)) { y._CMapData.entitiesExtentsMax = eMax; y.HasChanged = true; }
            if (Vec3Field("Streaming Extents Min", ref sMin)) { y._CMapData.streamingExtentsMin = sMin; y.HasChanged = true; }
            if (Vec3Field("Streaming Extents Max", ref sMax)) { y._CMapData.streamingExtentsMax = sMax; y.HasChanged = true; }
            if (ImGui.Button("Calculate extents")) { if (y.CalcExtents()) y.HasChanged = true; }

            ImGui.Spacing();
            ImGui.TextDisabled("File location: " + (y.RpfFileEntry?.Path ?? y.FilePath ?? ""));
            ImGui.TextDisabled("Project path: " + (Project?.GetRelativePath(y.FilePath ?? "") ?? ""));

            ImGui.Spacing();
            if (ImGui.Button("New Entity")) RequestNewEntity = true; ImGui.SameLine();
            if (ImGui.Button("Save Ymap")) RequestSaveYmap = true; ImGui.SameLine();
            if (ImGui.Button("Save As...")) RequestSaveYmapAs = true;
        }

        private static readonly string[] EntityFlagNames =
        {
            "1 - Allow Full Rotation", "2 - Stream Low Priority", "4 - Disable embedded Collisions",
            "8 - LOD in parent map", "16 - LOD Adopt me", "32 - Static Entity", "64 - Interior LOD",
            "128 - Unused", "256 - Unused", "512 - Unused", "1024 - Unused", "2048 - Unused", "4096 - Unused",
            "8192 - Unused", "16384 - Unused", "32768 - LOD Use Alt Fade", "65536 - Underwater",
            "131072 - Doesn't touch water", "262144 - Doesn't spawn peds", "524288 - Cast Static Shadows",
            "1048576 - Cast Dynamic Shadows", "2097152 - Ignore Time Settings", "4194304 - Don't render shadows",
            "8388608 - Only render shadows", "16777216 - Don't render reflections", "33554432 - Only render reflections",
            "67108864 - Don't render water reflections", "134217728 - Only render water reflections",
            "268435456 - Don't render mirror reflections", "536870912 - Only render mirror reflections",
            "1073741824 - Unused", "2147483648 - Unused",
        };
        private static readonly string[] LodLevelNames =
        {
            "LODTYPES_DEPTH_HD", "LODTYPES_DEPTH_LOD", "LODTYPES_DEPTH_SLOD1", "LODTYPES_DEPTH_SLOD2",
            "LODTYPES_DEPTH_SLOD3", "LODTYPES_DEPTH_ORPHANHD", "LODTYPES_DEPTH_SLOD4",
        };
        private static readonly string[] PriorityNames =
        {
            "PRI_REQUIRED", "PRI_OPTIONAL_HIGH", "PRI_OPTIONAL_MEDIUM", "PRI_OPTIONAL_LOW",
        };
        private string entArchEdit;

        private void DrawEntityPage(YmapEntityDef ent)
        {
            if (editsFor != ent) { entArchEdit = ent._CEntityDef.archetypeName.ToString(); editsFor = ent; }
            var e = ent._CEntityDef;
            bool changed = false;
            ImGui.TextDisabled("ENTITY" + (ent.MloParent != null ? "  (interior)" : ""));

            var pos = new Vector3(e.position.X, e.position.Y, e.position.Z);
            ImGui.SetNextItemWidth(-160);
            if (ImGui.DragFloat3("Position", ref pos, 0.01f))
            {
                ent.SetPositionRaw(new SDX.Vector3(pos.X, pos.Y, pos.Z));
                changed = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Go to##ent")) RequestGoToEntity = true;

            var rot = new Vector4(e.rotation.X, e.rotation.Y, e.rotation.Z, e.rotation.W);
            ImGui.SetNextItemWidth(-160);
            if (ImGui.DragFloat4("Rotation (quat)", ref rot, 0.001f))
            {
                var q = new SDX.Quaternion(rot.X, rot.Y, rot.Z, rot.W); q.Normalize();
                ent.SetOrientationRaw(q);
                changed = true;
            }
            var eul = WorldEditor.ToEulerDegrees(ent.Orientation);
            var eulN = new Vector3(eul.X, eul.Y, eul.Z);
            ImGui.SetNextItemWidth(-160);
            if (ImGui.DragFloat3("Rotation (deg)", ref eulN, 0.5f))
            {
                ent.SetOrientation(WorldEditor.FromEulerDegrees(new SDX.Vector3(eulN.X, eulN.Y, eulN.Z)));
                changed = true;
            }

            ImGui.SetNextItemWidth(-160);
            if (ImGui.InputText("Archetype", ref entArchEdit, 128))
            {
                uint hash = uint.TryParse(entArchEdit, out var h) ? h : JenkHash.GenHash(entArchEdit);
                if (!uint.TryParse(entArchEdit, out _)) JenkIndex.Ensure(entArchEdit);
                if (e.archetypeName.Hash != hash)
                {
                    SetEntityArchetype_U31(ent, hash);
                    changed = true;
                }
            }
            ImGui.SameLine(); ImGui.TextDisabled("# " + e.archetypeName.Hash);

            int guid = unchecked((int)e.guid);
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("GUID", ref guid, 0, 0)) { ent._CEntityDef.guid = unchecked((uint)guid); changed = true; }

            float sxy = e.scaleXY, sz = e.scaleZ;
            ImGui.SetNextItemWidth(160);
            if (ImGui.DragFloat("ScaleXY", ref sxy, 0.01f, 0.001f, 100.0f)) { ent.SetScale(new SDX.Vector3(sxy, sxy, ent.Scale.Z)); changed = true; }
            ImGui.SetNextItemWidth(160);
            if (ImGui.DragFloat("ScaleZ", ref sz, 0.01f, 0.001f, 100.0f)) { ent.SetScale(new SDX.Vector3(ent.Scale.X, ent.Scale.X, sz)); changed = true; }

            float lod = e.lodDist, clod = e.childLodDist;
            ImGui.SetNextItemWidth(160);
            if (ImGui.DragFloat("LodDist", ref lod, 1.0f, -1.0f, 20000.0f)) { ent._CEntityDef.lodDist = lod; ent.LodDist = lod; changed = true; }
            ImGui.SetNextItemWidth(160);
            if (ImGui.DragFloat("ChildLodDist", ref clod, 1.0f, -1.0f, 20000.0f)) { ent._CEntityDef.childLodDist = clod; ent.ChildLodDist = clod; changed = true; }

            int lodLevel = Array.IndexOf(LodLevelNames, e.lodLevel.ToString());
            if (lodLevel < 0) lodLevel = 0;
            ImGui.SetNextItemWidth(220);
            if (ImGui.Combo("LodLevel", ref lodLevel, LodLevelNames, LodLevelNames.Length))
            { ent._CEntityDef.lodLevel = (rage__eLodType)Enum.Parse(typeof(rage__eLodType), LodLevelNames[lodLevel]); changed = true; }

            int pri = Array.IndexOf(PriorityNames, e.priorityLevel.ToString());
            if (pri < 0) pri = 0;
            ImGui.SetNextItemWidth(220);
            if (ImGui.Combo("PriorityLevel", ref pri, PriorityNames, PriorityNames.Length))
            { ent._CEntityDef.priorityLevel = (rage__ePriorityLevel)Enum.Parse(typeof(rage__ePriorityLevel), PriorityNames[pri]); changed = true; }

            int pidx = e.parentIndex, nchild = (int)e.numChildren;
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("ParentIndex", ref pidx, 0, 0)) { ent._CEntityDef.parentIndex = pidx; changed = true; }
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("NumChildren", ref nchild, 0, 0)) { ent._CEntityDef.numChildren = (uint)Math.Max(nchild, 0); changed = true; }

            int ao = e.ambientOcclusionMultiplier, aao = e.artificialAmbientOcclusion, tint = (int)e.tintValue;
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("AOMultiplier", ref ao, 0, 0)) { ent._CEntityDef.ambientOcclusionMultiplier = Math.Clamp(ao, 0, 255); changed = true; }
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("ArtificialAO", ref aao, 0, 0)) { ent._CEntityDef.artificialAmbientOcclusion = Math.Clamp(aao, 0, 255); changed = true; }
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("TintValue", ref tint, 0, 0)) { ent._CEntityDef.tintValue = (uint)Math.Max(tint, 0); changed = true; }

            ImGui.Spacing();
            uint flags = e.flags;
            int fv = unchecked((int)flags);
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("Flags", ref fv, 0, 0)) { flags = unchecked((uint)fv); }
            ImGui.BeginChild("##entflags", new Vector2(0, 200), ImGuiChildFlags.Borders);
            for (int i = 0; i < 32; i++)
            {
                bool on = (flags & (1u << i)) != 0;
                if (ImGui.Checkbox(EntityFlagNames[i] + "##ef" + i, ref on))
                    flags = on ? (flags | (1u << i)) : (flags & ~(1u << i));
            }
            ImGui.EndChild();
            if (flags != e.flags) { ent._CEntityDef.flags = flags; changed = true; }

            if (ent.MloInstance != null)
            {
                ImGui.Spacing();
                ImGui.TextDisabled("MLO INSTANCE");
                var milo = ent.MloInstance._Instance;
                int gid = (int)milo.groupId, fid = (int)milo.floorId, nexit = (int)milo.numExitPortals, mflags = unchecked((int)milo.MLOInstflags);
                ImGui.SetNextItemWidth(160);
                if (ImGui.InputInt("Group ID", ref gid, 0, 0)) { ent.MloInstance._Instance.groupId = (uint)Math.Max(gid, 0); changed = true; }
                ImGui.SetNextItemWidth(160);
                if (ImGui.InputInt("Floor ID", ref fid, 0, 0)) { ent.MloInstance._Instance.floorId = (uint)Math.Max(fid, 0); changed = true; }
                ImGui.SetNextItemWidth(160);
                if (ImGui.InputInt("Num Exit Portals", ref nexit, 0, 0)) { ent.MloInstance._Instance.numExitPortals = (uint)Math.Max(nexit, 0); changed = true; }
                ImGui.SetNextItemWidth(160);
                if (ImGui.InputInt("MLO Flags", ref mflags, 0, 0)) { ent.MloInstance._Instance.MLOInstflags = unchecked((uint)mflags); changed = true; }
                var sets = ent.MloInstance.EntitySets;
                if (sets != null)
                {
                    ImGui.TextDisabled("Entity sets");
                    foreach (var es in sets)
                    {
                        if (es == null) continue;
                        bool vis = es.Visible;
                        if (ImGui.Checkbox(es.EntitySet?.Name + "##es" + es.GetHashCode(), ref vis)) { es.Visible = vis; changed = true; }
                    }
                }
            }

            ImGui.Spacing();
            bool inProject = ent.Ymap != null && Project != null && Project.ContainsYmap(ent.Ymap);
            if (!inProject)
            {
                if (ImGui.Button("Add to Project")) RequestAddSelectedToProject = true;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("This entity's ymap is the GAME's copy. Adding it to the project makes\\n" +
                                     "an editable copy that overrides the original in the world.");
                ImGui.SameLine();
            }
            if (LightPanel.DangerButton("Delete Entity", Vector2.Zero)) RequestDeleteEntity = true;
            ImGui.SameLine();
            if (ImGui.Button("Edit Archetype...") && ent.Archetype != null) Select(ent.Archetype);

            if (changed)
            {
                if (ent.Ymap != null) ent.Ymap.HasChanged = true;
                else if (ent.MloParent?.Archetype?.Ytyp != null) ent.MloParent.Archetype.Ytyp.HasChanged = true;
                EntityChangedInPage = ent;
            }
        }

        public GameFileCache ArchetypeCache;

        private MCEntityDef defEditsFor; private string defArchEdit = "";

        private void DrawMloEntityDefPage(MCEntityDef me)
        {
            if (defEditsFor != me) { defArchEdit = me._Data.archetypeName.ToString(); defEditsFor = me; }
            var mlo = me.OwnerMlo;
            var d = me._Data;
            bool changed = false;
            ImGui.TextDisabled("INTERIOR ENTITY  (definition - the interior is not loaded in the world)");
            var room = mlo?.GetEntityRoom(me); var portal = mlo?.GetEntityPortal(me); var set = mlo?.GetEntitySet(me);
            ImGui.TextDisabled($"in {mlo?.Name ?? "?"}" +
                               (room != null ? $"   room {room.Index}: {room.RoomName}" : "") +
                               (portal != null ? $"   portal {portal.Name}" : "") +
                               (set != null ? $"   set {set.Name}" : ""));
            if (mlo != null && ImGui.Button("Edit Interior Archetype...")) Select(mlo);

            var pos = new Vector3(d.position.X, d.position.Y, d.position.Z);
            ImGui.SetNextItemWidth(-160);
            if (ImGui.DragFloat3("Position (interior)", ref pos, 0.01f))
            { me._Data.position = new SDX.Vector3(pos.X, pos.Y, pos.Z); changed = true; }
            var rot = new Vector4(d.rotation.X, d.rotation.Y, d.rotation.Z, d.rotation.W);
            ImGui.SetNextItemWidth(-160);
            if (ImGui.DragFloat4("Rotation (quat)", ref rot, 0.001f))
            {
                var q = new SDX.Quaternion(rot.X, rot.Y, rot.Z, rot.W); q.Normalize();
                me._Data.rotation = new SDX.Vector4(q.X, q.Y, q.Z, q.W); changed = true;
            }
            ImGui.SetNextItemWidth(-160);
            if (ImGui.InputText("Archetype", ref defArchEdit, 128))
            {
                uint hash = uint.TryParse(defArchEdit, out var h) ? h : JenkHash.GenHash(defArchEdit);
                if (!uint.TryParse(defArchEdit, out _)) JenkIndex.Ensure(defArchEdit);
                if (d.archetypeName.Hash != hash)
                {
                    var live = SetMloDefArchetype_U31(me, hash);
                    if (live != null) EntityChangedInPage = live;
                    changed = true;
                }
            }
            ImGui.SameLine(); ImGui.TextDisabled("# " + d.archetypeName.Hash);
            int guid = unchecked((int)d.guid);
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("GUID", ref guid, 0, 0)) { me._Data.guid = unchecked((uint)guid); changed = true; }
            float sxy = d.scaleXY, sz = d.scaleZ;
            ImGui.SetNextItemWidth(160);
            if (ImGui.DragFloat("ScaleXY", ref sxy, 0.01f, 0.001f, 100.0f)) { me._Data.scaleXY = sxy; changed = true; }
            ImGui.SetNextItemWidth(160);
            if (ImGui.DragFloat("ScaleZ", ref sz, 0.01f, 0.001f, 100.0f)) { me._Data.scaleZ = sz; changed = true; }
            float lod = d.lodDist, clod = d.childLodDist;
            ImGui.SetNextItemWidth(160);
            if (ImGui.DragFloat("LodDist", ref lod, 1.0f, -1.0f, 20000.0f)) { me._Data.lodDist = lod; changed = true; }
            ImGui.SetNextItemWidth(160);
            if (ImGui.DragFloat("ChildLodDist", ref clod, 1.0f, -1.0f, 20000.0f)) { me._Data.childLodDist = clod; changed = true; }
            int lodLevel = Array.IndexOf(LodLevelNames, d.lodLevel.ToString());
            if (lodLevel < 0) lodLevel = 0;
            ImGui.SetNextItemWidth(220);
            if (ImGui.Combo("LodLevel", ref lodLevel, LodLevelNames, LodLevelNames.Length))
            { me._Data.lodLevel = (rage__eLodType)Enum.Parse(typeof(rage__eLodType), LodLevelNames[lodLevel]); changed = true; }
            int pri = Array.IndexOf(PriorityNames, d.priorityLevel.ToString());
            if (pri < 0) pri = 0;
            ImGui.SetNextItemWidth(220);
            if (ImGui.Combo("PriorityLevel", ref pri, PriorityNames, PriorityNames.Length))
            { me._Data.priorityLevel = (rage__ePriorityLevel)Enum.Parse(typeof(rage__ePriorityLevel), PriorityNames[pri]); changed = true; }
            int pidx = d.parentIndex, nchild = (int)d.numChildren;
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("ParentIndex", ref pidx, 0, 0)) { me._Data.parentIndex = pidx; changed = true; }
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("NumChildren", ref nchild, 0, 0)) { me._Data.numChildren = (uint)Math.Max(nchild, 0); changed = true; }
            int ao = d.ambientOcclusionMultiplier, aao = d.artificialAmbientOcclusion, tint = (int)d.tintValue;
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("AOMultiplier", ref ao, 0, 0)) { me._Data.ambientOcclusionMultiplier = Math.Clamp(ao, 0, 255); changed = true; }
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("ArtificialAO", ref aao, 0, 0)) { me._Data.artificialAmbientOcclusion = Math.Clamp(aao, 0, 255); changed = true; }
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("TintValue", ref tint, 0, 0)) { me._Data.tintValue = (uint)Math.Max(tint, 0); changed = true; }

            ImGui.Spacing();
            uint flags = d.flags;
            int fv = unchecked((int)flags);
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("Flags", ref fv, 0, 0)) { flags = unchecked((uint)fv); }
            ImGui.BeginChild("##defentflags", new Vector2(0, 200), ImGuiChildFlags.Borders);
            for (int i = 0; i < 32; i++)
            {
                bool on = (flags & (1u << i)) != 0;
                if (ImGui.Checkbox(EntityFlagNames[i] + "##def" + i, ref on))
                    flags = on ? (flags | (1u << i)) : (flags & ~(1u << i));
            }
            ImGui.EndChild();
            if (flags != d.flags) { me._Data.flags = flags; changed = true; }

            if (changed && mlo?.Ytyp != null) mlo.Ytyp.HasChanged = true;
        }

        private void DrawYtypPage(YtypFile t)
        {
            ImGui.TextDisabled("YTYP");
            ImGui.Text(t.Name ?? "ytyp");
            ImGui.TextDisabled("Hash: " + t.NameHash + $"   {t.AllArchetypes?.Length ?? 0} archetype(s)");
            ImGui.TextDisabled("File location: " + (t.RpfFileEntry?.Path ?? t.FilePath ?? ""));
            ImGui.Spacing();
            if (ImGui.Button("New Archetype")) RequestNewArchetype = true; ImGui.SameLine();
            if (ImGui.Button("From YDRs...")) RequestArchetypesFromYdrs = true; ImGui.SameLine();
            if (ImGui.Button("Save Ytyp")) RequestSaveYtyp = true; ImGui.SameLine();
            if (ImGui.Button("Save As...")) RequestSaveYtypAs = true;
        }

        private static readonly string[] ArchFlagNames =
        {
            "1 - Wet Road Reflection", "2 - Dont Fade", "4 - Draw Last", "8 - Climbable By AI", "16 - Suppress HD TXDs",
            "32 - Static", "64 - Disable alpha sorting", "128 - Tough For Bullets", "256 - Is Generic", "512 - Has Anim (YCD)",
            "1024 - UV anims (YCD)", "2048 - Shadow Only", "4096 - Damage Model", "8192 - Dont Cast Shadows",
            "16384 - Cast Texture Shadows", "32768 - Dont Collide With Flyer", "65536 - Double-sided rendering", "131072 - Dynamic",
            "262144 - Override Physics Bounds", "524288 - Auto Start Anim", "1048576 - Pre Reflected Water Proxy",
            "2097152 - Proxy For Water Reflections", "4194304 - No AI Cover", "8388608 - No Player Cover",
            "16777216 - Is Ladder Deprecated", "33554432 - Has Cloth", "67108864 - Enable Door Physics",
            "134217728 - Is Fixed For Navigation", "268435456 - Dont Avoid By Peds", "536870912 - Use Ambient Scale",
            "1073741824 - Is Debug", "2147483648 - Has Alpha Shadow",
        };

        private void DrawArchetypePage(Archetype a)
        {
            if (editsFor != a)
            {
                var d0 = a._BaseArchetypeDef;
                archNameEdit = d0.name.ToString();
                archAssetEdit = d0.assetName.ToString();
                archTxdEdit = d0.textureDictionary.ToString();
                archClipEdit = d0.clipDictionary.ToString();
                archDrawDictEdit = d0.drawableDictionary.ToString();
                archPhysEdit = d0.physicsDictionary.ToString();
                editsFor = a;
            }
            var ytyp = a.Ytyp;
            var d = a._BaseArchetypeDef;
            bool changed = false;
            ImGui.TextDisabled("ARCHETYPE" + (a is MloArchetype ? "  (interior)" : a is TimeArchetype ? "  (time)" : ""));

            MetaHash HashField(string label, ref string buf, MetaHash current, ref bool ch)
            {
                ImGui.SetNextItemWidth(-200);
                if (ImGui.InputText(label, ref buf, 128))
                {
                    uint hash = uint.TryParse(buf, out var h) ? h : JenkHash.GenHash(buf);
                    if (!uint.TryParse(buf, out _)) JenkIndex.Ensure(buf);
                    if (current.Hash != hash) { ch = true; current = new MetaHash(hash); }
                }
                ImGui.SameLine(); ImGui.TextDisabled("# " + current.Hash);
                return current;
            }
            d.name = HashField("Name", ref archNameEdit, d.name, ref changed);
            d.assetName = HashField("Asset Name", ref archAssetEdit, d.assetName, ref changed);
            d.textureDictionary = HashField("Texture Dict", ref archTxdEdit, d.textureDictionary, ref changed);
            d.clipDictionary = HashField("Clip Dict", ref archClipEdit, d.clipDictionary, ref changed);
            d.drawableDictionary = HashField("Drawable Dict", ref archDrawDictEdit, d.drawableDictionary, ref changed);
            d.physicsDictionary = HashField("Physics Dict", ref archPhysEdit, d.physicsDictionary, ref changed);

            int at = (int)d.assetType;
            string[] assetTypes = { "ASSET_TYPE_UNINITIALIZED", "ASSET_TYPE_FRAGMENT", "ASSET_TYPE_DRAWABLE", "ASSET_TYPE_DRAWABLEDICTIONARY", "ASSET_TYPE_ASSETLESS" };
            int ati = Array.IndexOf(assetTypes, d.assetType.ToString()); if (ati < 0) ati = 0;
            ImGui.SetNextItemWidth(260);
            if (ImGui.Combo("Asset Type", ref ati, assetTypes, assetTypes.Length))
            { d.assetType = (rage__fwArchetypeDef__eAssetType)Enum.Parse(typeof(rage__fwArchetypeDef__eAssetType), assetTypes[ati]); changed = true; }

            float lodd = d.lodDist, hdd = d.hdTextureDist, bsr = d.bsRadius;
            ImGui.SetNextItemWidth(160);
            if (ImGui.DragFloat("LodDist##a", ref lodd, 1.0f, 0.0f, 20000.0f)) { d.lodDist = lodd; changed = true; }
            ImGui.SetNextItemWidth(160);
            if (ImGui.DragFloat("HD Texture Dist", ref hdd, 1.0f, 0.0f, 20000.0f)) { d.hdTextureDist = hdd; changed = true; }
            var bbmin = d.bbMin; var bbmax = d.bbMax; var bsc = d.bsCentre;
            if (Vec3Field("BB Min", ref bbmin)) { d.bbMin = bbmin; changed = true; }
            if (Vec3Field("BB Max", ref bbmax)) { d.bbMax = bbmax; changed = true; }
            if (Vec3Field("BS Center", ref bsc)) { d.bsCentre = bsc; changed = true; }
            ImGui.SetNextItemWidth(160);
            if (ImGui.DragFloat("BS Radius", ref bsr, 0.1f, 0.0f, 10000.0f)) { d.bsRadius = bsr; changed = true; }
            int sa = (int)d.specialAttribute;
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("Special Attribute", ref sa, 0, 0)) { d.specialAttribute = (uint)Math.Max(sa, 0); changed = true; }

            ImGui.Spacing();
            uint flags = d.flags;
            int fv = unchecked((int)flags);
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("Flags##af", ref fv, 0, 0)) flags = unchecked((uint)fv);
            ImGui.BeginChild("##archflags", new Vector2(0, 200), ImGuiChildFlags.Borders);
            for (int i = 0; i < 32; i++)
            {
                bool on = (flags & (1u << i)) != 0;
                if (ImGui.Checkbox(ArchFlagNames[i] + "##af" + i, ref on))
                    flags = on ? (flags | (1u << i)) : (flags & ~(1u << i));
            }
            ImGui.EndChild();
            if (flags != d.flags) { d.flags = flags; changed = true; }

            if (a is TimeArchetype ta)
            {
                int tf = unchecked((int)ta.TimeFlags);
                ImGui.SetNextItemWidth(160);
                if (ImGui.InputInt("Time Flags", ref tf, 0, 0))
                {
                    ta.TimeFlags = unchecked((uint)tf);
                    var td = ta._TimeArchetypeDef; var tdd = td._TimeArchetypeDef;
                    tdd.timeFlags = ta.TimeFlags; td._TimeArchetypeDef = tdd; ta._TimeArchetypeDef = td;
                    changed = true;
                }
            }
            if (a is MloArchetype mlo)
            {
                ImGui.Spacing();
                ImGui.TextDisabled(MapProject.DescribeMlo(mlo));
                if (ImGui.Button("Update Portal Counts")) { mlo.UpdatePortalCounts(); changed = true; }
                ImGui.SameLine();
                if (ImGui.Button("Validate")) Status = MloEditor.Validate(mlo) is { Length: > 0 } v ? v : "interior valid";
            }

            if (changed)
            {
                a._BaseArchetypeDef = d;
                a.LodDist = d.lodDist;
                if (ytyp != null) ytyp.HasChanged = true;
            }

            ImGui.Spacing();
            if (LightPanel.DangerButton("Delete Archetype", Vector2.Zero)) RequestDeleteArchetype = true;
        }

        private string roomNameEdit, roomTcEdit, roomTc2Edit;

        private void DrawRoomPage(MCMloRoomDef r)
        {
            var mlo = r.OwnerMlo;
            if (editsFor != r)
            {
                roomNameEdit = MloEditor.GetRoomName(r);
                roomTcEdit = MloEditor.GetRoomTimecycle(r);
                roomTc2Edit = MloEditor.GetRoomSecondaryTimecycle(r);
                editsFor = r;
            }
            bool ch = false;
            ImGui.TextDisabled("MLO ROOM");
            ImGui.SetNextItemWidth(-160);
            if (ImGui.InputText("Name##rn", ref roomNameEdit, 64)) { MloEditor.SetRoomName(mlo, r, roomNameEdit); ch = true; }
            ImGui.SetNextItemWidth(-160);
            if (ImGui.InputText("Timecycle", ref roomTcEdit, 64)) { MloEditor.SetRoomTimecycle(mlo, r, roomTcEdit); ch = true; }
            ImGui.SetNextItemWidth(-160);
            if (ImGui.InputText("Secondary Timecycle", ref roomTc2Edit, 64)) { MloEditor.SetRoomSecondaryTimecycle(mlo, r, roomTc2Edit); ch = true; }
            var mn = MloEditor.GetRoomBBMin(r); var mx = MloEditor.GetRoomBBMax(r);
            bool bb = Vec3Field("BB Min##r", ref mn); bb |= Vec3Field("BB Max##r", ref mx);
            if (bb) { MloEditor.SetRoomBB(mlo, r, mn, mx); ch = true; }
            float blend = MloEditor.GetRoomBlend(r);
            ImGui.SetNextItemWidth(160);
            if (ImGui.DragFloat("Blend", ref blend, 0.01f, 0.0f, 1.0f)) { MloEditor.SetRoomBlend(mlo, r, blend); ch = true; }
            int floor = MloEditor.GetRoomFloorId(r), evd = MloEditor.GetRoomExteriorVisibilityDepth(r);
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("Floor ID", ref floor, 0, 0)) { MloEditor.SetRoomFloorId(mlo, r, floor); ch = true; }
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("Exterior Visibility Depth", ref evd, 0, 0)) { MloEditor.SetRoomExteriorVisibilityDepth(mlo, r, evd); ch = true; }
            uint rflags = MloEditor.GetRoomFlags(r);
            int fl = unchecked((int)rflags);
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("Flags##rf", ref fl, 0, 0)) rflags = unchecked((uint)fl);
            ImGui.BeginChild("##roomflags", new Vector2(0, RoomFlagNames.Length * 22 + 8), ImGuiChildFlags.Borders);
            for (int i = 0; i < RoomFlagNames.Length; i++)
            {
                bool on = (rflags & (1u << i)) != 0;
                if (ImGui.Checkbox(RoomFlagNames[i] + "##rf" + i, ref on)) rflags = on ? (rflags | (1u << i)) : (rflags & ~(1u << i));
            }
            ImGui.EndChild();
            if (rflags != MloEditor.GetRoomFlags(r)) { MloEditor.SetRoomFlags(mlo, r, rflags); ch = true; }
            ImGui.TextDisabled($"Portal count: {MloEditor.GetRoomPortalCount(r)}");
            DrawAttachedObjects(mlo, r.AttachedObjects, "room");
            if (ch && mlo?.Ytyp != null) mlo.Ytyp.HasChanged = true;
        }

        private void DrawAttachedObjects(MloArchetype mlo, uint[] attached, string tag)
        {
            int n = attached?.Length ?? 0;
            ImGui.Spacing();
            ImGui.TextDisabled($"OBJECTS IN THIS {tag.ToUpperInvariant()}  ({n})");
            if (n == 0) { ImGui.TextDisabled("  none attached"); return; }
            var ents = mlo?.entities;
            ImGui.BeginChild("##attached" + tag, new Vector2(0, Math.Min(22.0f * n + 8.0f, 200.0f)), ImGuiChildFlags.Borders);
            for (int k = 0; k < n; k++)
            {
                uint idx = attached[k];
                var me = ents != null && idx < ents.Length ? ents[idx] : null;
                if (me == null) { ImGui.TextDisabled($"{idx}: (missing)"); continue; }
                bool sel = IsCurrentMloEntity(me);
                if (ImGui.Selectable($"{idx}: {me.Name}##att{tag}{k}", sel)) SelectMloEntity(me);
                if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                { SelectMloEntity(me); if (CurrentEntity != null) RequestGoToEntity = true; }
            }
            ImGui.EndChild();
        }

        private static readonly string[] RoomFlagNames =
        {
            "1 - Freeze Vehicles", "2 - Freeze Peds", "4 - No Directional Light", "8 - No Exterior Lights",
            "16 - Force Freeze", "32 - Reduce Cars", "64 - Reduce Peds", "128 - Force Directional Light On",
            "256 - Dont Render Exterior", "512 - Mirror Potentially Visible",
        };
        private static readonly string[] PortalFlagNames =
        {
            "1 - One-Way", "2 - Link Interiors together", "4 - Mirror", "8 - Disable Timecycle Modifier",
            "16 - Mirror Using Expensive Shaders", "32 - Low LOD Only", "64 - Hide when door closed",
            "128 - Mirror Can See Directional", "256 - Mirror Using Portal Traversal", "512 - Mirror Floor",
            "1024 - Mirror Can See Exterior View", "2048 - Water Surface", "4096 - Water Surface Extend To Horizon",
            "8192 - Use Light Bleed",
        };

        private void DrawPortalPage(MCMloPortalDef p)
        {
            var mlo = p.OwnerMlo;
            bool ch = false;
            ImGui.TextDisabled("MLO PORTAL");
            int from = (int)MloEditor.GetPortalRoomFrom(p), to = (int)MloEditor.GetPortalRoomTo(p);
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputInt("Room From", ref from, 0, 0)) ch |= MloEditor.SetPortalRoomFrom(mlo, p, (uint)Math.Max(from, 0));
            ImGui.SetNextItemWidth(120);
            if (ImGui.InputInt("Room To", ref to, 0, 0)) ch |= MloEditor.SetPortalRoomTo(mlo, p, (uint)Math.Max(to, 0));
            ImGui.TextDisabled($"{MloEditor.RoomNameFor(mlo, (uint)from)}  ->  {MloEditor.RoomNameFor(mlo, (uint)to)}");
            uint pflags = MloEditor.GetPortalFlags(p);
            int fl = unchecked((int)pflags), mp = (int)MloEditor.GetPortalMirrorPriority(p);
            int op = (int)MloEditor.GetPortalOpacity(p), ao = (int)MloEditor.GetPortalAudioOcclusion(p);
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("Flags##pf", ref fl, 0, 0)) pflags = unchecked((uint)fl);
            ImGui.BeginChild("##portalflags", new Vector2(0, PortalFlagNames.Length * 22 + 8), ImGuiChildFlags.Borders);
            for (int i = 0; i < PortalFlagNames.Length; i++)
            {
                bool on = (pflags & (1u << i)) != 0;
                if (ImGui.Checkbox(PortalFlagNames[i] + "##pf" + i, ref on)) pflags = on ? (pflags | (1u << i)) : (pflags & ~(1u << i));
            }
            ImGui.EndChild();
            if (pflags != MloEditor.GetPortalFlags(p)) { MloEditor.SetPortalFlags(mlo, p, pflags); ch = true; }
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("Mirror Priority", ref mp, 0, 0)) { MloEditor.SetPortalMirrorPriority(mlo, p, (uint)Math.Max(mp, 0)); ch = true; }
            ImGui.SetNextItemWidth(160);
            if (UiSlider_U28.Int("Opacity", ref op, 0, 100)) { MloEditor.SetPortalOpacity(mlo, p, (uint)op); ch = true; }
            ImGui.SetNextItemWidth(160);
            if (ImGui.InputInt("Audio Occlusion", ref ao, 0, 0)) { MloEditor.SetPortalAudioOcclusion(mlo, p, (uint)Math.Max(ao, 0)); ch = true; }
            ImGui.TextDisabled("CORNERS");
            int nc = MloEditor.GetPortalCornerCount(p);
            for (int i = 0; i < nc; i++)
            {
                var c = MloEditor.GetPortalCorner(p, i);
                if (Vec3Field($"Corner {i}##pc{i}", ref c)) { MloEditor.SetPortalCorner(mlo, p, i, c); ch = true; }
            }
            if (ImGui.SmallButton("Add corner")) { MloEditor.AddPortalCorner(mlo, p, MloEditor.GetPortalCenter(p)); ch = true; }
            if (nc > 3) { ImGui.SameLine(); if (ImGui.SmallButton("Remove last corner")) { MloEditor.RemovePortalCorner(mlo, p, nc - 1); ch = true; } }
            DrawAttachedObjects(mlo, p.AttachedObjects, "portal");
            if (ch && mlo?.Ytyp != null) mlo.Ytyp.HasChanged = true;
        }

        private string setNameEdit;
        private void DrawEntitySetPage(MCMloEntitySet s)
        {
            var mlo = s.OwnerMlo;
            if (editsFor != s) { setNameEdit = MloEditor.GetEntitySetName(s); editsFor = s; }
            ImGui.TextDisabled("MLO ENTITY SET");
            ImGui.SetNextItemWidth(-160);
            if (ImGui.InputText("Name##esn", ref setNameEdit, 64)) { MloEditor.SetEntitySetName(mlo, s, setNameEdit); if (mlo?.Ytyp != null) mlo.Ytyp.HasChanged = true; }
            ImGui.TextDisabled(MloEditor.DescribeEntitySet(s));

            bool fv = s.ForceVisible;
            if (ImGui.Checkbox("Force visible in editor", ref fv)) { s.ForceVisible = fv; EntitySetVisibilityChanged = true; }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Show this set's entities in the world even when no MLO instance\n" +
                                 "enables it. Editor-only; not saved into the ytyp.");

            var ents = s.Entities;
            int n = ents?.Length ?? 0;
            ImGui.Spacing();
            ImGui.TextDisabled($"ENTITIES ({n})");
            if (n > 0)
            {
                ImGui.BeginChild("##setents", new Vector2(0, Math.Min(n * 21 + 8, 180)), ImGuiChildFlags.Borders);
                for (int i = 0; i < n; i++)
                {
                    var me = ents[i];
                    if (me == null) continue;
                    uint room = s.Locations != null && i < s.Locations.Length ? s.Locations[i] : 0u;
                    ImGui.Selectable($"{me._Data.archetypeName}   (room {room})##se{i}", false);
                }
                ImGui.EndChild();
            }
            if (ImGui.Button("Add Entity")) RequestNewMloEntity = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("A new entity in this set, at the camera, in room 0");
            ImGui.SameLine();
            if (LightPanel.DangerButton("Delete Set", Vector2.Zero)) RequestDeleteEntitySet = true;
        }
        public bool EntitySetVisibilityChanged;

        private static bool FlagBox(string label, ref uint flags, int bit)
        {
            bool on = (flags & (1u << bit)) != 0;
            if (!ImGui.Checkbox(label, ref on)) return false;
            flags = on ? (flags | (1u << bit)) : (flags & ~(1u << bit));
            return true;
        }

        private static bool Vec3Field(string label, ref SDX.Vector3 v)
        {
            var n = new Vector3(v.X, v.Y, v.Z);
            ImGui.SetNextItemWidth(-200);
            if (!ImGui.DragFloat3(label, ref n, 0.01f)) return false;
            v = new SDX.Vector3(n.X, n.Y, n.Z);
            return true;
        }
    }
}

