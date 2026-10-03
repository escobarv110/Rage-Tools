using System;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public static bool ShellChrome_U27;
        public string ShellPopup_U27;
        public bool RequestRestart_U27;
        private string shellPopupOpen_U27;

        public bool ShellCanUndo_U27 => WorldMode ? WorldHistory?.CanUndo == true : scene.CanUndo;
        public bool ShellCanRedo_U27 => WorldMode ? WorldHistory?.CanRedo == true : scene.CanRedo;
        public bool ShellCanSave_U27 => scene.HasModel;
        public bool ShellMultiSave_U27 => scene.Files.Count > 1;
        public bool ShellHasSelection_U27 => WorldSel != null || WorldSelection.HasValue;
        public string ShellDocName_U27 => !string.IsNullOrEmpty(ProjectName) ? ProjectName + (scene.Dirty ? " *" : "")
            : scene.HasModel ? scene.FileName : "";

        public void ShellCommand_U27(string id)
        {
            switch (id)
            {
                case "open": RequestOpenFile?.Invoke(); break;
                case "addprop": RequestAddFile?.Invoke(); break;
                case "newlightprop": RequestNewLightProxy?.Invoke(); break;
                case "loadytd": RequestOpenYtd?.Invoke(); break;
                case "save": if (scene.HasModel) RequestSave?.Invoke(); break;
                case "saveas": if (scene.HasModel) RequestSaveAs?.Invoke(null); break;
                case "registertypes": RequestRegisterFileTypes?.Invoke(true); break;
                case "unregistertypes": RequestRegisterFileTypes?.Invoke(false); break;
                case "session.new": RequestNewProject?.Invoke(); break;
                case "session.open": RequestOpenProject?.Invoke(); break;
                case "session.save": RequestSaveProject?.Invoke(false); break;
                case "session.saveas": RequestSaveProject?.Invoke(true); break;
                case "undo":
                    if (WorldMode) RequestWorldUndo = true;
                    else if (scene.CanUndo) scene.Undo();
                    break;
                case "redo":
                    if (WorldMode) RequestWorldRedo = true;
                    else if (scene.CanRedo) scene.Redo();
                    break;
                case "matundo": if (Materials.CanUndo) Materials.Undo(); break;
                case "matredo": if (Materials.CanRedo) Materials.Redo(); break;
                case "frame": RequestFrameWorldSelection_M3(); break;
                case "tutorial": openTutorial = true; break;
                case "log": AppLog_U21.ShowWindow = true; break;
                case "logfolder": OpenLogFolder_U21(); break;
                case "mirrorjoke":
                    if (settings != null) { settings.MirrorSurprise = !settings.MirrorSurprise; settings.Save(); }
                    break;
            }
        }

        public System.Collections.Generic.List<(string text, bool warn)> ShellWorldStatus_U27()
        {
            var list = new System.Collections.Generic.List<(string, bool)>();
            if (WorldSel != null) list.Add(((WorldSel.Archetype?.Name ?? "?") + DirtyMark_V19(WorldSel), false));
            if (WorldDirtyCount > 0) list.Add(($"{WorldDirtyCount} unsaved ymap{(WorldDirtyCount == 1 ? "" : "s")}", true));
            if (!string.IsNullOrEmpty(WorldClipboardSummary)) list.Add(("copied: " + WorldClipboardSummary, false));
            if (WorldTruncated) list.Add(("budget reached", true));
            if (list.Count == 0) list.Add((WorldYmapsOpen < WorldYmapsWanted ? "loading the map..." : "ready", false));
            return list;
        }

        public bool MirrorSurprise_U27 => settings?.MirrorSurprise == true;

        public void ShellToolbarLogic_U27()
        {
            bool collMode = SelectionModeEnum == WorldSelectionMode.Collision;
            if (collMode != lastCollisionMode_U22)
            {
                if (collMode) { collisionShownBefore_U22 = WorldShowCollision; WorldShowCollision = true; }
                else WorldShowCollision = collisionShownBefore_U22;
                lastCollisionMode_U22 = collMode;
            }
            if (WorldGizmo != null)
            {
                float snapNow = RotateSnapSteps_U5.Clamp(WorldGizmo.RotateSnapDeg);
                WorldGizmo.RotateSnapDeg = snapNow;
                if (settings != null && Math.Abs(settings.RotateSnapDeg - snapNow) > 0.0001f)
                    settings.RotateSnapDeg = snapNow;
                SnapLabelDrawn_U5 = RotateSnapSteps_U5.Label(snapNow);
            }
        }

        public bool NewUi_U27
        {
            get => settings?.NewUiU27 == true;
            set { if (settings != null && settings.NewUiU27 != value) { settings.NewUiU27 = value; settings.Save(); } }
        }

        public bool InterfaceChangePending_U27 => settings != null && settings.NewUiU27 != ShellChrome_U27;

        private void DrawInterfaceSection_U27()
        {
            ImGui.Separator();
            ImGui.TextDisabled("Interface");
            bool newUi = NewUi_U27;
            if (ImGui.RadioButton("Classic##u27", !newUi)) NewUi_U27 = false;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The ImGui interface drawn inside the viewport.");
            ImGui.SameLine();
            if (ImGui.RadioButton("New##u27", newUi)) NewUi_U27 = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The Avalonia window: menus, toolbar and tool palette around the viewport.");
            if (InterfaceChangePending_U27)
            {
                ImGui.TextDisabled("Takes effect after a restart.");
                if (ImGui.Button("Restart now##u27")) RequestRestart_U27 = true;
            }
        }

        public const int ShellPageInspector_U27 = 0, ShellPageProject_U27 = 1, ShellPageAssets_U27 = 2, ShellPageArea_U27 = 3, ShellPageOptions_U27 = 4;
        public int ShellRightPage_U27;
        public bool ShellRightImGui_U27;
        public float ShellRightPx_U27 = 340.0f;
        public bool ShellOwnsRight_U27 => ShellChrome_U27 && WorldMode && !NavMode;

        public bool ShellInspectorNative_U27
        {
            get
            {
                var s = WorldSelection;
                if (WorldSel == null)
                    return !s.HasValue && !EditLightActive && SelectionModeEnum != WorldSelectionMode.Collision;
                if (!s.HasValue) return true;
                bool spaceData = s.PathNode != null || s.TrainTrackNode != null || s.ScenarioNode != null ||
                                 s.NavPoly != null || s.NavPoint != null || s.NavPortal != null || s.Audio != null;
                return !spaceData && s.Light == null && s.MloEntityDef == null && s.EntityDef != null &&
                       s.CollisionBounds == null && s.CollisionPoly == null && s.CollisionVertex == null;
            }
        }

        public string ConsumeRightTabRequest_U27()
        {
            var r = rightTabRequest;
            rightTabRequest = null;
            if (AreaTool != null && AreaTool.RequestShowTab) { AreaTool.RequestShowTab = false; r = "Area"; }
            return r;
        }

        private static readonly Vector4 ShellPanelBg_U27 = new Vector4(0x14 / 255f, 0x15 / 255f, 0x18 / 255f, 1f);
        private static readonly Vector4 ShellPanelLine_U27 = new Vector4(0x23 / 255f, 0x25 / 255f, 0x2A / 255f, 1f);

        private void DrawShellRightPage_U27(float displayWidth, float displayHeight)
        {
            var pw = ProjectWindow;
            if (pw != null) pw.DockAvailable = true;
            if (AreaTool != null) AreaTool.TabActive = ShellRightImGui_U27 && ShellRightPage_U27 == ShellPageArea_U27;
            if (!ShellRightImGui_U27) return;
            float w = Math.Clamp(ShellRightPx_U27, 120.0f, displayWidth);
            ImGui.SetNextWindowPos(new Vector2(displayWidth - w, 0), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(w, displayHeight), ImGuiCond.Always);
            ImGui.PushStyleColor(ImGuiCol.WindowBg, ShellPanelBg_U27);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0.0f);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 10));
            var flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                        ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking |
                        ImGuiWindowFlags.NoBringToFrontOnFocus;
            ImGui.Begin("##shellright_u27", flags);
            ImGui.PopStyleVar(3);
            ImGui.PopStyleColor();
            var dl = ImGui.GetWindowDrawList();
            dl.AddLine(new Vector2(displayWidth - w, 0), new Vector2(displayWidth - w, displayHeight), ImGui.GetColorU32(ShellPanelLine_U27));
            switch (ShellRightPage_U27)
            {
                case ShellPageInspector_U27:
                    ImGui.BeginChild("##shinsp", new Vector2(0, 0));
                    DrawInspectorTab();
                    ImGui.EndChild();
                    break;
                case ShellPageProject_U27:
                    if (pw == null) { ImGui.TextWrapped("The world is not up yet."); break; }
                    DrawProjectPlacement_U28(pw);
                    if (pw.Detached) ImGui.TextWrapped("The project is open in its own window.");
                    else if (pw.ShowsDocked) { pw.Minimized = false; pw.DrawEmbedded(); }
                    else ImGui.TextWrapped("The project is floating over the viewport.");
                    break;
                case ShellPageAssets_U27:
                    ImGui.BeginChild("##shassets", new Vector2(0, 0));
                    DrawAssetsTab();
                    ImGui.EndChild();
                    break;
                case ShellPageArea_U27:
                    ImGui.BeginChild("##sharea", new Vector2(0, 0));
                    DrawAreaTab();
                    ImGui.EndChild();
                    break;
                case ShellPageOptions_U27:
                    ImGui.BeginChild("##shopts", new Vector2(0, 0));
                    DrawWorldOptions();
                    ImGui.EndChild();
                    break;
            }
            ImGui.End();
        }

        public bool ProjectInOwnWindow_U28 => ProjectWindow?.Detached == true;

        public void SetProjectInOwnWindow_U28(bool own)
        {
            var pw = ProjectWindow;
            if (pw == null) return;
            if (own) { pw.Visible = true; pw.RequestDetach = true; }
            else { if (pw.Detached) pw.RequestAttach = true; pw.Docked = true; pw.Visible = true; }
        }

        private void DrawProjectPlacement_U28(ProjectWindow pw)
        {
            ImGui.TextDisabled("Show the project");
            ImGui.SameLine();
            if (ImGui.RadioButton("In this panel##u28pp", !pw.Detached)) SetProjectInOwnWindow_U28(false);
            ImGui.SameLine();
            if (ImGui.RadioButton("In its own window##u28pp", pw.Detached)) SetProjectInOwnWindow_U28(true);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("A separate window you can move to another monitor, like CodeWalker's project window.");
            ImGui.Separator();
        }

        private void DrawShellPopups_U27()
        {
            if (ShellPopup_U27 != null)
            {
                shellPopupOpen_U27 = ShellPopup_U27;
                ShellPopup_U27 = null;
                ImGui.OpenPopup("##shellpop_" + shellPopupOpen_U27);
            }
            if (shellPopupOpen_U27 == null) return;
            ImGui.SetNextWindowPos(new Vector2(8, 8), ImGuiCond.Appearing);
            if (!ImGui.BeginPopup("##shellpop_" + shellPopupOpen_U27)) { shellPopupOpen_U27 = null; return; }
            switch (shellPopupOpen_U27)
            {
                case "appearance": DrawAppearanceBody_V19(); break;
                case "shortcuts": DrawShortcutsSection(); break;
            }
            ImGui.EndPopup();
        }
    }
}
