using System;
using System.Linq;
using System.Numerics;
using CodeWalker.GameFiles;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public bool ExtensionMode => Workspace == Space.Extension;

        private static readonly Vector4 ExtWorkspaceColour = new Vector4(0.62f, 0.42f, 0.86f, 1f);

        private const string ExtWorkspaceTooltip =
            "Extensions workspace: everything a prop DOES beyond being a model.\n" +
            "Open a .ytyp, a .ymap or a model, pick an archetype, and add light shafts,\n" +
            "ladders, particle effects, spawn points, doors, wind and the rest.\n" +
            "Corners and points snap onto the model's own vertices, and every edit\n" +
            "shows in the viewport as you make it.";

        public readonly ExtensionWorkspace_V68 Ext = new ExtensionWorkspace_V68();

        public bool RequestExtOpenYtyp_V68, RequestExtOpenYmap_V68, RequestExtOpenModel_V68;
        public bool RequestExtSaveYtyp_V68, RequestExtSaveYtypAs_V68, RequestExtNewArchetype_V68;
        public bool RequestExtFrame_V68;
        public string RequestExtGameArchetype_V68;
        public bool RequestExtFromWorld_V68, RequestExtFromMlo_V68;
        public int RequestExtPickProp_V69 = -1;
        public bool RequestExtUndo_V70, RequestExtRedo_V70;
        public string ExtGameName_V68 = "";
        public bool ExtAutoModel_V68 = true;
        public bool ExtPlayEffects_V73 = true;
        public int ExtShownFor_V68 = -1;
        public string ExtWorldSelName_V68, ExtMloSessionName_V68;

        partial void WorkspaceTabs_V68();
        partial void WorkspaceTabColour_V68(Space space, ref Vector4 col);
        partial void WorkspaceTabTip_V68(Space space, ref string tip);
        partial void WorkspaceLeft_V68(float displayHeight, ref bool handled);
        partial void WorkspaceRight_V68(ref bool handled);
        partial void WorkspaceTheme_V68(ref bool handled);

        partial void WorkspaceTabs_V68()
        {
            ImGui.SameLine(0, 2);
            DrawWorkspaceTab("Extensions", Space.Extension);
        }

        partial void WorkspaceTabColour_V68(Space space, ref Vector4 col)
        {
            if (space == Space.Extension) col = ExtWorkspaceColour;
        }

        partial void WorkspaceTabTip_V68(Space space, ref string tip)
        {
            if (space == Space.Extension) tip = ExtWorkspaceTooltip;
        }

        partial void WorkspaceTheme_V68(ref bool handled)
        {
            if (!ExtensionMode) return;
            UiTheme.Apply(settings.ThemeIndex,
                new Vector3(ExtWorkspaceColour.X, ExtWorkspaceColour.Y, ExtWorkspaceColour.Z));
            handled = true;
        }

        partial void WorkspaceLeft_V68(float displayHeight, ref bool handled)
        {
            if (!ExtensionMode) return;
            handled = true;

            ImGui.TextColored(ExtWorkspaceColour, "OPEN A FILE");
            float topHalf = (ImGui.GetContentRegionAvail().X - 4) / 2.0f;
            if (ImGui.Button(".ytyp...", new Vector2(topHalf, 0))) RequestExtOpenYtyp_V68 = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The archetypes in it become editable here.");
            ImGui.SameLine(0, 4);
            if (ImGui.Button(".ymap...", new Vector2(-1, 0))) RequestExtOpenYmap_V68 = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Brings in the archetypes its entities place, so you can\n" +
                                 "edit the props of a map you already have.");
            if (ImGui.Button("Open model...", new Vector2(-1, 0))) RequestExtOpenModel_V68 = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The model to look at while you place the extensions on it,\n" +
                                 "and the one whose vertices the corners snap to.");

            ImGui.Spacing();
            ImGui.TextColored(ExtWorkspaceColour, "OR TAKE WHAT IS OPEN");
            bool haveWorldSel = !string.IsNullOrEmpty(ExtWorldSelName_V68);
            if (!haveWorldSel) ImGui.BeginDisabled();
            if (ImGui.Button(haveWorldSel ? "World pick: " + ExtWorldSelName_V68
                                          : "The prop picked in the World", new Vector2(-1, 0)))
                RequestExtFromWorld_V68 = true;
            if (!haveWorldSel) ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(haveWorldSel
                    ? "Brings that archetype in here, model and all."
                    : "Right-click a prop in the World workspace first, then come back.");

            bool haveMlo = !string.IsNullOrEmpty(ExtMloSessionName_V68);
            if (!haveMlo) ImGui.BeginDisabled();
            if (ImGui.Button(haveMlo ? "MLO Creator: " + ExtMloSessionName_V68
                                     : "The MLO Creator interior", new Vector2(-1, 0)))
                RequestExtFromMlo_V68 = true;
            if (!haveMlo) ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(haveMlo
                    ? "Its archetype, so you can hang extensions on the interior you are building."
                    : "Start an interior in the MLO Creator first.");

            ImGui.SetNextItemWidth(-64);
            bool go = ImGui.InputTextWithHint("##extgame", "a prop name from the game...",
                                              ref ExtGameName_V68, 64, ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.SameLine(0, 4);
            if (ImGui.Button("Find##extgame", new Vector2(-1, 0)) || go)
                RequestExtGameArchetype_V68 = ExtGameName_V68;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Any archetype the game defines - prop_bench_01a, v_ilev_...");

            if (Ext.Sources.Count > 0)
            {
                if (ImGui.BeginChild("##extsrc", new Vector2(0, Math.Min(Ext.Sources.Count * ImGui.GetTextLineHeightWithSpacing() + 8, 78)), ImGuiChildFlags.Borders))
                    foreach (var s in Ext.Sources)
                    {
                        ImGui.TextDisabled(s.Kind);
                        ImGui.SameLine();
                        ImGui.TextUnformatted(s.Name ?? "?");
                    }
                ImGui.EndChild();
            }

            if (Ext.Props.Count > 0)
            {
                ImGui.Spacing();
                ImGui.TextColored(ExtWorkspaceColour, "PROPS");
                ImGui.SameLine();
                ImGui.TextDisabled("(" + Ext.Props.Count + ")");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("##extpropfind", "find a prop...", ref Ext.PropFilter, 64);
                float ph = Math.Max(96, displayHeight * 0.22f);
                if (ImGui.BeginChild("##extprops", new Vector2(0, ph), ImGuiChildFlags.Borders))
                {
                    foreach (var pr in Ext.VisibleProps())
                    {
                        int i = Ext.Props.IndexOf(pr);
                        if (ImGui.Selectable(pr.Name + "##pr" + i, Ext.SelectedProp == i))
                            RequestExtPickProp_V69 = i;
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip(pr.Name + "\n" + pr.From + "\n" +
                                             $"at {pr.Position.X:0.##}, {pr.Position.Y:0.##}, {pr.Position.Z:0.##}\n" +
                                             "Click it to edit the extensions on its archetype.");
                    }
                }
                ImGui.EndChild();
            }

            ImGui.Spacing();
            ImGui.TextColored(ExtWorkspaceColour, "ARCHETYPES");
            ImGui.SameLine();
            ImGui.TextDisabled("(" + Ext.Targets.Count + ")");
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##extfind", "find an archetype...", ref Ext.Filter, 64);

            float listH = Math.Max(120, displayHeight * 0.42f);
            if (ImGui.BeginChild("##extlist68", new Vector2(0, listH), ImGuiChildFlags.Borders))
            {
                bool any = false;
                foreach (var t in Ext.Visible())
                {
                    any = true;
                    int i = Ext.Targets.IndexOf(t);
                    int n = ArchetypeExtensions_V62.Get(t.Archetype).Length;
                    if (ImGui.Selectable(t.Name + "##ext" + i, Ext.Selected == i))
                    {
                        Ext.Selected = i;
                        Ext.SelectedExtension = -1;
                        Ext.CancelSnap();
                        RequestExtFrame_V68 = true;
                    }
                    if (n > 0)
                    {
                        ImGui.SameLine();
                        ImGui.TextColored(UiTheme.Accent, n.ToString());
                    }
                    if (!t.Editable)
                    {
                        ImGui.SameLine();
                        ImGui.TextDisabled("game");
                    }
                }
                if (!any)
                    ImGui.TextDisabled(Ext.Targets.Count == 0
                        ? "Open a .ytyp, a .ymap or a model to start."
                        : "Nothing matches that.");
            }
            ImGui.EndChild();

            if (ImGui.Checkbox("Show its model", ref ExtAutoModel_V68)) ExtShownFor_V68 = -1;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Loads the archetype's own model from the archives, so you can see " +
                                 "what you are working on and snap the corners onto it.");

            if (ImGui.Button("New archetype", new Vector2(-1, 0))) RequestExtNewArchetype_V68 = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("For a loose model with no .ytyp yet: makes one named after it,\n" +
                                 "so its extensions have somewhere to live.");

            var cur = Ext.Current;
            if (cur != null && cur.Editable)
            {
                float half = (ImGui.GetContentRegionAvail().X - 4) / 2.0f;
                if (ImGui.Button("Save .ytyp", new Vector2(half, 0))) RequestExtSaveYtyp_V68 = true;
                ImGui.SameLine(0, 4);
                if (ImGui.Button("Save as...", new Vector2(-1, 0))) RequestExtSaveYtypAs_V68 = true;
            }
            else if (cur != null)
            {
                ImGui.TextWrapped("From the game - Save as... writes your own copy.");
                if (ImGui.Button("Save as...", new Vector2(-1, 0))) RequestExtSaveYtypAs_V68 = true;
            }

            ImGui.Spacing();
            if (!string.IsNullOrEmpty(Ext.Status))
            {
                if (Ext.StatusIsProblem) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.65f, 0.3f, 1.0f));
                ImGui.TextWrapped(Ext.Status);
                if (Ext.StatusIsProblem) ImGui.PopStyleColor();
            }
        }

        partial void WorkspaceRight_V68(ref bool handled)
        {
            if (!ExtensionMode) return;
            handled = true;

            var t = Ext.Current;
            if (t?.Archetype == null)
            {
                ImGui.Spacing();
                ImGui.TextWrapped("Pick an archetype on the left and its extensions appear here.");
                ImGui.Spacing();
                ImGui.TextDisabled("Light shafts, ladders, particle effects, audio emitters,");
                ImGui.TextDisabled("spawn points, doors, wind - every type the format has.");
                return;
            }

            ImGui.TextDisabled("EDITING");
            ImGui.SameLine();
            ImGui.TextColored(ExtWorkspaceColour, t.Name);
            if (t.Editable) ImGui.TextDisabled(t.Source?.Name ?? "");
            else ImGui.TextDisabled("from the game - Save as... to keep your own copy");

            ImGui.Spacing();
            bool shafts = ShowLightShafts, marks = ShowExtensions;
            if (ImGui.Checkbox("Draw shafts", ref shafts)) ShowLightShafts = shafts;
            ImGui.SameLine(0, 12);
            if (ImGui.Checkbox("Draw markers", ref marks)) ShowExtensions = marks;
            ImGui.SameLine(0, 12);
            bool play = ExtPlayEffects_V73;
            if (ImGui.Checkbox("Play effects", ref play)) ExtPlayEffects_V73 = play;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Runs the selected particle effect for real, out of the game's .ypt files, " +
                                 "at the place the extension puts it.");
            ImGui.SameLine(0, 12);
            if (ImGui.SmallButton("Frame the model")) RequestExtFrame_V68 = true;

            bool canUndo = Ext.UndoDepth_V70 > 0, canRedo = Ext.RedoDepth_V70 > 0;
            if (!canUndo) ImGui.BeginDisabled();
            if (ImGui.SmallButton("Undo##ext70")) RequestExtUndo_V70 = true;
            if (!canUndo) ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(canUndo ? "Ctrl+Z - " + Ext.NextUndoName_V70 : "Nothing to undo yet (Ctrl+Z)");
            ImGui.SameLine(0, 4);
            if (!canRedo) ImGui.BeginDisabled();
            if (ImGui.SmallButton("Redo##ext70")) RequestExtRedo_V70 = true;
            if (!canRedo) ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(canRedo ? "Ctrl+Y - " + Ext.NextRedoName_V70 : "Nothing to redo (Ctrl+Y)");
            ImGui.SameLine(0, 8);
            ImGui.TextDisabled("right-click a point in the viewport to pick its extension");

            if (Ext.SnapArmed)
            {
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(ExtWorkspaceColour.X * 0.35f,
                    ExtWorkspaceColour.Y * 0.35f, ExtWorkspaceColour.Z * 0.35f, 1.0f));
                if (ImGui.BeginChild("##extsnapbar", new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 2.6f),
                                     ImGuiChildFlags.Borders))
                {
                    ImGui.TextColored(new Vector4(1, 1, 1, 1),
                        "Click a vertex for " + ArchetypeExtensions_V62.Spaced(Ext.SnapFieldName) +
                        (Ext.SnapChain.Count > 0 ? $"   ({Ext.SnapChain.Count} more after it)" : ""));
                    if (ImGui.SmallButton("Cancel")) Ext.CancelSnap();
                }
                ImGui.EndChild();
                ImGui.PopStyleColor();
            }

            ImGui.Separator();
            extSel_V62 = Ext.SelectedExtension;
            DrawArchetypeExtensions_V62(t.Archetype);
            Ext.SelectedExtension = extSel_V62;
        }

        private void DrawSnapControls_V68(MetaWrapper sel, int extIndex)
        {
            if (!ExtensionMode || sel == null) return;
            var corners = ExtensionWorkspace_V68.CornerFields_V68(sel);
            if (corners.Count < 2) return;
            if (ImGui.Button($"Snap all {corners.Count} corners", new Vector2(-1, 0)))
                Ext.ArmCornerChain(extIndex, sel);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Then click the model's vertices one after another -\n" +
                                 ArchetypeExtensions_V62.Spaced(corners[0].Prop.Name) + " first.");
        }

        private bool DrawSnapFieldButton_V68(ArchetypeExtensions_V62.Field f, int extIndex)
        {
            if (!ExtensionMode || !ExtensionWorkspace_V68.IsPointField_V68(f.Prop)) return false;
            bool armed = Ext.SnapArmed && Ext.SnapExtension == extIndex && Ext.SnapFieldName == f.Prop.Name;
            ImGui.SameLine(0, 4);
            if (armed) ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.Accent);
            bool hit = ImGui.SmallButton((armed ? "picking##snap" : "snap##snap") + f.Prop.Name);
            if (armed) ImGui.PopStyleColor();
            if (hit)
            {
                if (armed) Ext.CancelSnap();
                else Ext.ArmSnap(extIndex, f.Prop.Name);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Click this, then click a vertex on the model - it lands exactly there.");
            return hit;
        }
    }
}
