using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using CodeWalker.GameFiles;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class ProjectWindow
    {
        public bool RequestOpenFolder;
        public bool RequestSaveItem, RequestSaveItemAs;
        public bool RequestGoToSelected;
        public bool RequestUndo, RequestRedo, RequestCut, RequestCopy, RequestPaste, RequestClone, RequestDeleteItem;
        public int RequestTheme = -1;
        public bool RequestAddWorldSelectionToProject;
        public string WorldSelectionSummary;
        public bool CanUndo, CanRedo, CanPaste;
        public int ThemeIndex = UiTheme.DefaultTheme;

        public bool ShowExplorer = true;
        public bool AutoCalcFlags = true, AutoCalcExtents = true;
        public bool DisplayEntityIndexes;
        public bool OptionsChanged;

        public bool CanGoTo =>
            CurrentEntity != null || CurrentYmap != null || CurrentRoom != null || CurrentPortal != null ||
            CurrentArchetype is MloArchetype || CurrentScenario != null;

        public bool Focused { get; private set; }

        private void DrawFileMenu()
        {
            bool hasProj = Project != null;
            if (!ImGui.BeginMenu("File")) return;
            if (ImGui.BeginMenu("New"))
            {
                if (ImGui.MenuItem("Project", "Ctrl+N")) RequestNewProject = true;
                ImGui.Separator();
                if (ImGui.MenuItem("Ymap File")) RequestNewYmap = true;
                if (ImGui.MenuItem("Ytyp File")) RequestNewYtyp = true;
                ImGui.EndMenu();
            }
            if (ImGui.MenuItem("Open Project...", "Ctrl+O")) RequestOpenProject = true;
            if (ImGui.MenuItem("Open Files...")) RequestOpenAny = true;
            if (ImGui.MenuItem("Open Folder...")) RequestOpenFolder = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add every supported file under a folder (and its subfolders) to the project.");
            ImGui.Separator();
            bool hasWorldSel = !string.IsNullOrEmpty(WorldSelectionSummary);
            if (ImGui.MenuItem("Add Selected to Project", null, false, hasWorldSel)) RequestAddWorldSelectionToProject = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip(AddSelectedTooltip());
            ImGui.Separator();
            if (ImGui.MenuItem("Close Project", null, false, hasProj)) RequestCloseProject = true;
            ImGui.Separator();
            if (ImGui.MenuItem("Save Project", "Ctrl+S", false, hasProj)) RequestSaveProject = true;
            if (ImGui.MenuItem("Save Project As...", null, false, hasProj)) RequestSaveProjectAs = true;
            bool hasItem = CurrentYmap != null || CurrentYtyp != null || CurrentScenario != null;
            if (ImGui.MenuItem("Save Item", null, false, hasItem)) RequestSaveItem = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Save the ymap or ytyp the selection lives in.");
            if (ImGui.MenuItem("Save Item As...", null, false, hasItem)) RequestSaveItemAs = true;
            if (ImGui.MenuItem("Save All", "Ctrl+Shift+S", false, hasProj)) RequestSaveAll = true;
            ImGui.EndMenu();
        }

        private void DrawEditMenu()
        {
            if (!ImGui.BeginMenu("Edit")) return;
            if (ImGui.MenuItem("Undo", "Ctrl+Z", false, CanUndo)) RequestUndo = true;
            if (ImGui.MenuItem("Redo", "Ctrl+Y", false, CanRedo)) RequestRedo = true;
            ImGui.Separator();
            bool ent = CurrentEntity != null;
            if (ImGui.MenuItem("Cut Item", "Ctrl+X", false, ent)) RequestCut = true;
            if (ImGui.MenuItem("Copy Item", "Ctrl+C", false, ent)) RequestCopy = true;
            if (ImGui.MenuItem("Paste Item", "Ctrl+V", false, CanPaste)) RequestPaste = true;
            if (ImGui.MenuItem("Clone Item", null, false, ent)) RequestClone = true;
            if (ImGui.MenuItem("Delete Item", "Shift+Del", false, ent || CurrentArchetype != null)) RequestDeleteItem = true;
            ImGui.Separator();
            if (ImGui.MenuItem("Go to Selected", "F", false, CanGoTo)) RequestGoToSelected = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Fly the camera to the selected entity, ymap, room, portal or interior.");
            ImGui.EndMenu();
        }

        private void DrawViewMenu()
        {
            if (!ImGui.BeginMenu("View")) return;
            ImGui.MenuItem("Project Explorer", null, ref ShowExplorer);
            ImGui.Separator();
            ImGui.TextDisabled("Colours and size: Appearance, on the main window");
            ImGui.EndMenu();
        }

        private void DrawOptionsMenu()
        {
            if (!ImGui.BeginMenu("Options")) return;
            bool renderGta = !HideGtaMap;
            if (ImGui.MenuItem("Render GTAV Map", null, ref renderGta)) HideGtaMap = !renderGta;
            ImGui.MenuItem("Render Project Items", null, ref RenderProjectItems);
            if (ImGui.MenuItem("Auto Calculate Ymap Flags", null, ref AutoCalcFlags)) OptionsChanged = true;
            if (ImGui.MenuItem("Auto Calculate Ymap Extents", null, ref AutoCalcExtents)) OptionsChanged = true;
            if (ImGui.MenuItem("Display Entity Indexes", null, ref DisplayEntityIndexes)) OptionsChanged = true;
            ImGui.EndMenu();
        }

        private void DrawToolStrip()
        {
            bool hasProj = Project != null;
            if (ImGui.Button("New v")) ImGui.OpenPopup("##pwnewpop");
            if (ImGui.BeginPopup("##pwnewpop"))
            {
                if (ImGui.MenuItem("New Project")) RequestNewProject = true;
                if (ImGui.MenuItem("New Ymap File")) RequestNewYmap = true;
                if (ImGui.MenuItem("New Ytyp File")) RequestNewYtyp = true;
                ImGui.EndPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("Open v")) ImGui.OpenPopup("##pwopenpop");
            if (ImGui.BeginPopup("##pwopenpop"))
            {
                if (ImGui.MenuItem("Open Project...")) RequestOpenProject = true;
                if (ImGui.MenuItem("Open Files...")) RequestOpenAny = true;
                if (ImGui.MenuItem("Open Folder...")) RequestOpenFolder = true;
                ImGui.EndPopup();
            }
            ImGui.SameLine();
            if (!hasProj) ImGui.BeginDisabled();
            if (ImGui.Button("Save")) RequestSaveProject = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Save the project (Ctrl+S)");
            ImGui.SameLine();
            if (ImGui.Button("Save All")) RequestSaveAll = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Save every changed ymap and ytyp, then the project (Ctrl+Shift+S)");
            if (!hasProj) ImGui.EndDisabled();
            DrawCloseProjectButton_J2();
            ImGui.SameLine();
            ImGui.TextDisabled("|"); ImGui.SameLine();
            if (!CanGoTo) ImGui.BeginDisabled();
            if (ImGui.Button("Go to")) RequestGoToSelected = true;
            if (!CanGoTo) ImGui.EndDisabled();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Fly the camera to the selected item (F)");
            ImGui.SameLine();
            bool hasWorldSel = !string.IsNullOrEmpty(WorldSelectionSummary);
            if (!hasWorldSel) ImGui.BeginDisabled();
            if (ImGui.Button("Add selected")) RequestAddWorldSelectionToProject = true;
            if (!hasWorldSel) ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) ImGui.SetTooltip(AddSelectedTooltip());
            ImGui.SameLine();
            ImGui.TextDisabled("|"); ImGui.SameLine();
            if (CurrentYmap != null || CurrentRoom != null || CurrentPortal != null || CurrentEntitySet != null || CurrentEntity?.MloParent != null)
            {
                if (ImGui.Button("+ Entity")) RequestNewEntity = true;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(CurrentRoom != null || CurrentPortal != null || CurrentEntitySet != null || CurrentEntity?.MloParent != null
                        ? "Adds a prop to the selected interior room, portal or entity set (saved in its .ytyp)."
                        : "Adds a prop to the selected ymap. Standing inside an interior room puts it in that room instead.");
                ImGui.SameLine();
            }
            if (CurrentYtyp != null) { if (ImGui.Button("+ Archetype")) RequestNewArchetype = true; ImGui.SameLine(); }
            if (!string.IsNullOrEmpty(Status)) ImGui.TextDisabled(Status);
            else ImGui.NewLine();
            ImGui.Separator();
        }

        private string AddSelectedTooltip()
        {
            if (string.IsNullOrEmpty(WorldSelectionSummary))
                return "Nothing is selected in the world. Select an entity, interior, car generator,\n" +
                       "LOD light, occluder, scenario point, path node, nav poly, train node or audio\n" +
                       "zone in the viewport, then add the file it lives in to the project here.";
            return "Add the file the world selection lives in to the project (a project is created\n" +
                   "when there is none), and open the window on it:\n    " + WorldSelectionSummary;
        }

        private void HandleShortcuts()
        {
            Focused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);
            if (!Focused) return;
            var io = ImGui.GetIO();
            if (io.WantTextInput) return;
            bool ctrl = io.KeyCtrl, shift = io.KeyShift;
            if (ctrl && shift && ImGui.IsKeyPressed(ImGuiKey.S)) { RequestSaveAll = true; return; }
            if (ctrl && !shift && ImGui.IsKeyPressed(ImGuiKey.S)) { RequestSaveProject = true; return; }
            if (ctrl && ImGui.IsKeyPressed(ImGuiKey.O)) { RequestOpenProject = true; return; }
            if (ctrl && ImGui.IsKeyPressed(ImGuiKey.N)) { RequestNewProject = true; return; }
            if (ctrl && ImGui.IsKeyPressed(ImGuiKey.Z)) { if (CanUndo) RequestUndo = true; return; }
            if (ctrl && ImGui.IsKeyPressed(ImGuiKey.Y)) { if (CanRedo) RequestRedo = true; return; }
            if (ctrl && ImGui.IsKeyPressed(ImGuiKey.X)) { if (CurrentEntity != null) RequestCut = true; return; }
            if (ctrl && ImGui.IsKeyPressed(ImGuiKey.C)) { if (CurrentEntity != null) RequestCopy = true; return; }
            if (ctrl && ImGui.IsKeyPressed(ImGuiKey.V)) { if (CanPaste) RequestPaste = true; return; }
            if (shift && ImGui.IsKeyPressed(ImGuiKey.Delete)) { if (CurrentEntity != null || CurrentArchetype != null) RequestDeleteItem = true; return; }
            if (!ctrl && !shift && ImGui.IsKeyPressed(ImGuiKey.F)) { if (CanGoTo) RequestGoToSelected = true; return; }
        }

        public event Action FileListsChanged;

        private void DrawFileFolder(string title, List<string> names)
        {
            if (names == null || names.Count == 0) return;
            if (!ImGui.TreeNodeEx($"{title} ({names.Count})###{title}", ImGuiTreeNodeFlags.SpanAvailWidth)) return;
            for (int i = 0; i < names.Count; i++)
            {
                var n = names[i];
                ImGui.TreeNodeEx($"{Path.GetFileName(n)}###{title}_{i}",
                    ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.SpanAvailWidth);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip(n);
                if (ImGui.BeginPopupContextItem($"##filectx_{title}_{i}"))
                {
                    if (ImGui.MenuItem("Remove from Project"))
                    {
                        names.RemoveAt(i);
                        if (Project != null)
                        {
                            Project.HasChanged = true;
                            Project.SyncYbnFiles();
                        }
                        FileListsChanged?.Invoke();
                        ImGui.EndPopup();
                        break;
                    }
                    ImGui.EndPopup();
                }
            }
            ImGui.TreePop();
        }

        private void DrawFileFolders()
        {
            var p = Project;
            if (p == null) return;
            DrawFileFolder("Ybn Files", p.YbnFilenames);
            DrawFileFolder("Ynd Files", p.YndFilenames);
            DrawFileFolder("Ynv Files", p.YnvFilenames);
            DrawFileFolder("Trains Files", p.TrainsFilenames);
            DrawScenarioFolder();
            DrawFileFolder("Audio Rel Files", p.AudioRelFilenames);
            DrawFileFolder("Ydr Files", p.YdrFilenames);
            DrawFileFolder("Ydd Files", p.YddFilenames);
            DrawFileFolder("Yft Files", p.YftFilenames);
            DrawFileFolder("Ytd Files", p.YtdFilenames);
        }

        private static void DrawCountNode(string title, int count, string id)
        {
            if (count <= 0) return;
            ImGui.TreeNodeEx($"{title} ({count})###{id}",
                ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen | ImGuiTreeNodeFlags.SpanAvailWidth);
        }

        private void DrawYmapContext(YmapFile y)
        {
            if (!ImGui.BeginPopupContextItem($"##ymapctx{y.GetHashCode()}")) return;
            if (ImGui.MenuItem("Go to")) { Select(y); RequestGoToSelected = true; }
            ImGui.Separator();
            if (ImGui.MenuItem("Save")) { Select(y); RequestSaveYmap = true; }
            if (ImGui.MenuItem("Save As...")) { Select(y); RequestSaveYmapAs = true; }
            ImGui.Separator();
            if (ImGui.MenuItem("Remove from Project")) { Select(y); RequestRemoveYmap = true; }
            ImGui.EndPopup();
        }

        private void DrawYtypContext(YtypFile t)
        {
            if (!ImGui.BeginPopupContextItem($"##ytypctx{t.GetHashCode()}")) return;
            if (ImGui.MenuItem("Save")) { Select(t); RequestSaveYtyp = true; }
            if (ImGui.MenuItem("Save As...")) { Select(t); RequestSaveYtypAs = true; }
            ImGui.Separator();
            if (ImGui.MenuItem("Remove from Project")) { Select(t); RequestRemoveYtyp = true; }
            ImGui.EndPopup();
        }

        private void DrawEntityContext(YmapEntityDef e, string id)
        {
            if (!ImGui.BeginPopupContextItem(id)) return;
            if (ImGui.MenuItem("Go to")) { Select(e); RequestGoToSelected = true; }
            ImGui.Separator();
            if (ImGui.MenuItem("Copy")) { Select(e); RequestCopy = true; }
            if (ImGui.MenuItem("Clone")) { Select(e); RequestClone = true; }
            if (ImGui.MenuItem("Delete")) { Select(e); RequestDeleteItem = true; }
            ImGui.EndPopup();
        }
    }
}

