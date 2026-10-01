using System;
using System.Linq;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class MloCreatorPanel
    {
        public readonly MloAssetLibrary Assets = new MloAssetLibrary();
        public Action LightsPageDrawer;
        public LoadedFile RequestSelectPropLights;

        private string assetFolderToPlace = "";

        private void DrawToolNodes_L3()
        {
            var f3 = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.NoTreePushOnOpen | (Page == PageKind.Lights ? ImGuiTreeNodeFlags.Selected : 0);
            ImGui.TreeNodeEx("Lights###mloclightsnode", f3);
            if (ImGui.IsItemClicked()) ShowPage(PageKind.Lights);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Select a prop's light and edit it exactly as in the Lights workspace - over THIS interior's props only.");
            var f4 = ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.NoTreePushOnOpen | (Page == PageKind.Assets ? ImGuiTreeNodeFlags.Selected : 0);
            ImGui.TreeNodeEx("Assets library###mlocassetsnode", f4);
            if (ImGui.IsItemClicked()) ShowPage(PageKind.Assets);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Search the game archives and your prop folders for models and place them in the interior.");
        }

        private void DrawToolbarButtons_L3()
        {
            bool onLights = Page == PageKind.Lights;
            if (onLights) ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]);
            if (ImGui.Button("Lights")) ShowPage(PageKind.Lights);
            if (onLights) ImGui.PopStyleColor();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("The light editor over this interior's props.");
        }

        private void DrawEntityPageExtras_L3(MloCreatorEntity e)
        {
            if (e == null) return;
            if (e.SourceFile != null)
            {
                ImGui.SameLine();
                if (ImGui.Button("Edit lights##eel3")) { RequestSelectPropLights = e.SourceFile; ShowPage(PageKind.Lights); }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Open the Lights page on this prop: its lights listed, the first one selected.");
            }
            else if (!e.FromImportedMlo)
            {
                ImGui.SameLine();
                if (ImGui.Button("Resolve model##erl3")) Assets.RequestResolveTyped = true;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("A typed entity has no model here: find its archetype in the game archives / prop folders and place the model at its position, so it shows and can be lit.");
            }
        }

        private void DrawLightsPage_L3()
        {
            if (LightsPageDrawer != null) LightsPageDrawer();
            else ImGui.TextDisabled("The light editor is not attached.");
        }

        public void DrawAssetsPage_L3(Scene scene, bool compact = false)
        {
            var lib = Assets;
            if (!compact)
            {
                ImGui.TextDisabled("ASSETS LIBRARY");
                ImGui.SameLine();
            }
            string counts = $"{lib.ArchiveStatus}, {lib.FolderFiles:N0} in prop folders, {lib.ProjectFiles:N0} in the interior's folder, {lib.LightPropCount:N0} light props";
            if (compact) { ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]); ImGui.TextWrapped(counts); ImGui.PopStyleColor(); }
            else ImGui.TextDisabled(counts);

            if (compact)
            {
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputTextWithHint("##mlassetq", "search models (prop_chair, v_ilev_, bench ...)", ref lib.Query, 96)) { lib.Dirty = true; lib.ResetPaging_P2(); }
                float half = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f;
                ImGui.SetNextItemWidth(half);
                if (ImGui.Combo("##mlassetsrc", ref lib.SourceFilter, MloAssetLibrary.SourceLabels, MloAssetLibrary.SourceLabels.Length)) lib.Dirty = true;
                ImGui.SameLine();
                ImGui.SetNextItemWidth(-1);
                if (ImGui.Combo("##mlassetkind", ref lib.KindFilter, MloAssetLibrary.KindLabels, MloAssetLibrary.KindLabels.Length)) lib.Dirty = true;
            }
            else
            {
                ImGui.SetNextItemWidth(-260);
                if (ImGui.InputTextWithHint("##mlassetq", "search models by name (prop_chair, v_ilev_, bench ...)", ref lib.Query, 96)) { lib.Dirty = true; lib.ResetPaging_P2(); }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(150);
                if (ImGui.Combo("##mlassetsrc", ref lib.SourceFilter, MloAssetLibrary.SourceLabels, MloAssetLibrary.SourceLabels.Length)) lib.Dirty = true;
                ImGui.SameLine();
                ImGui.SetNextItemWidth(100);
                if (ImGui.Combo("##mlassetkind", ref lib.KindFilter, MloAssetLibrary.KindLabels, MloAssetLibrary.KindLabels.Length)) lib.Dirty = true;
            }

            DrawAssetCategoryRow_P2(lib, compact);

            ImGui.TextDisabled("Place at:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(compact ? -1 : 230);
            ImGui.Combo("##mlassetat", ref lib.PlaceAt, MloAssetLibrary.PlaceAtLabels, MloAssetLibrary.PlaceAtLabels.Length);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Where a placed model lands. The placement point is the last snapped click / the point 3ds Max sent\n(Vertex snap page); drag the entity with the gizmo afterwards.");
            DrawPlaceRoomRow_N3(compact);
            if (!compact) ImGui.SameLine();
            ImGui.Checkbox("Click places##mlaclick", ref lib.ClickPlaces);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("On: one click on a result loads the model and drops it in the scene, as an entity of the room it lands in.\nOff: a click selects; double-click or Place puts it down.");
            ImGui.SameLine();
            ImGui.Checkbox(compact ? "Select##mlasel" : "Select after placing##mlasel", ref lib.SelectAfterPlace);
            if (compact && ImGui.IsItemHovered()) ImGui.SetTooltip("Select the placed entity (the gizmo lands on it).");
            ImGui.SameLine();
            ImGui.Checkbox(compact ? "Thumbs##mlathumb" : "Thumbnails##mlathumb", ref lib.ShowThumbnails);
            DrawAssetViewSwitch_O3(lib, compact);
            if (!compact) ImGui.SameLine();
            if (ImGui.SmallButton("Rescan##mlarescan")) lib.RequestRescan = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Re-read the prop folders and the interior's folder.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Add prop folder...##mlaaddf")) lib.RequestAddPropFolder = true;
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add a folder of your own .ydr / .yft props to the library (the same prop folders the light workspace scans).");
            if (!string.IsNullOrEmpty(lib.Status)) ImGui.TextDisabled(lib.Status);

            float bottomH = 118 + (lib.Recent.Count > 0 ? 26 : 0);
            int fromGame = lib.Results.Count(r => r.FromArchive);
            string split = fromGame > 0 ? $"  ({fromGame:N0} game, {lib.Results.Count - fromGame:N0} yours)" : "";
            ImGui.TextDisabled(ResultsNote_P2(lib) + split +
                               (lib.Category_P2 != 0 ? "  in " + MloAssetCategories_P2.Names[lib.Category_P2] : ""));
            if (!compact)
            {
                ImGui.SameLine();
                ImGui.TextDisabled(lib.ClickPlaces ? "  click a row to load it into the scene; drag a row into the viewport to drop it under the cursor"
                                                   : "  double-click or Place puts it in the interior; drag a row into the viewport to drop it under the cursor");
            }
            float listH = compact ? (AssetView_O3 == 1 ? 420 : 240) : -bottomH;
            ImGui.BeginChild("##mlassetrows", new Vector2(0, listH), ImGuiChildFlags.Borders);
            if (!DrawAssetGrid_O3(lib, compact))
            {
            float contentW = ImGui.GetContentRegionAvail().X;
            float rowH = lib.ShowThumbnails ? 44 : ImGui.GetTextLineHeightWithSpacing();
            float scrollY = ImGui.GetScrollY(), viewH = ImGui.GetWindowHeight();
            int first = Math.Max(0, (int)(scrollY / rowH) - 1);
            int last = Math.Min(lib.Results.Count - 1, (int)((scrollY + viewH) / rowH) + 1);
            if (first > 0) ImGui.Dummy(new Vector2(1, first * rowH));
            for (int i = first; i <= last; i++)
            {
                var it = lib.Results[i];
                ImGui.PushID(i);
                var p0 = ImGui.GetCursorScreenPos();
                bool sel = lib.Selected == it;
                if (ImGui.Selectable("##row", sel, ImGuiSelectableFlags.AllowDoubleClick, new Vector2(0, rowH)))
                {
                    lib.Selected = it;
                    if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) { if (!lib.ClickPlaces) lib.RequestPlace = it; }
                    else if (lib.ClickPlaces) lib.RequestPlace = it;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"{it.FileName}\n{(it.FromArchive ? "game archives: " : "")}{it.Path}\n{it.SourceLabel}{(it.LightCount > 0 ? $", {it.LightCount} light(s)" : "")}{(it.Size > 0 ? $", {it.Size / 1024.0:0.#} KB" : "")}\n\n" +
                                     (lib.ClickPlaces ? "Click to load it into the scene at the place chosen above; drag into the viewport to drop it under the cursor." : "Double-click to place; drag into the viewport to drop it under the cursor."));
                if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 6.0f)) lib.BeginDrag?.Invoke(it.ToEntry());
                var dl = ImGui.GetWindowDrawList();
                float x = p0.X + 4;
                if (lib.ShowThumbnails)
                {
                    var tex = lib.ThumbnailOf != null ? lib.ThumbnailOf(it.ToEntry()) : IntPtr.Zero;
                    var q0 = new Vector2(x, p0.Y + 2); var q1 = new Vector2(x + rowH - 4, p0.Y + rowH - 2);
                    if (tex != IntPtr.Zero) dl.AddImage(tex, q0, q1);
                    else { dl.AddRectFilled(q0, q1, ImGui.GetColorU32(ImGuiCol.FrameBg), 3.0f); dl.AddText(new Vector2(q0.X + 12, q0.Y + 12), ImGui.GetColorU32(ImGuiCol.TextDisabled), "..."); }
                    x += rowH + 4;
                }
                float ty = p0.Y + (rowH - ImGui.GetTextLineHeight()) * 0.5f;
                dl.AddText(new Vector2(x, ty), ImGui.GetColorU32(ImGuiCol.Text), it.Name);
                string right = compact ? (it.IsYft ? "yft" : "ydr") + (it.LightCount > 0 ? $"  {it.LightCount}L" : "")
                                       : (it.IsYft ? "yft" : "ydr") + "   " + it.SourceLabel + (it.LightCount > 0 ? $"   {it.LightCount} light{(it.LightCount == 1 ? "" : "s")}" : "") + (it.Size > 0 ? $"   {it.Size / 1024.0:0.#} KB" : "");
                float rw = ImGui.CalcTextSize(right).X;
                dl.AddText(new Vector2(p0.X + contentW - rw - 8, ty), ImGui.GetColorU32(ImGuiCol.TextDisabled), right);
                ImGui.PopID();
            }
            int below = lib.Results.Count - 1 - last;
            if (below > 0) ImGui.Dummy(new Vector2(1, below * rowH));
            if (lib.Results.Count == 0)
                ImGui.TextDisabled(string.IsNullOrWhiteSpace(lib.Query) ? "Type a name to search. Empty search + a source other than the archives lists that source." : "Nothing matches.");
            }
            RequestMoreIfNearEnd_P2(lib);
            ImGui.EndChild();

            var s = lib.Selected;
            if (s != null)
            {
                ImGui.Text(s.Name);
                ImGui.SameLine();
                ImGui.TextDisabled($"({(s.IsYft ? "yft" : "ydr")}, {s.SourceLabel})");
                if (!compact) ImGui.SameLine();
                if (ImGui.Button("Place##mlaplace")) lib.RequestPlace = s;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Put this model in the interior at the place chosen above, as an entity of the current room.");
                ImGui.SameLine();
                if (ImGui.Button("Place 3 in a row##mlaplace3")) { lib.RequestPlace = s; lib.RequestPlaceCount = 3; }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Three copies a metre apart along X - for benches, shelves, lockers.");
            }
            else ImGui.TextDisabled(lib.ClickPlaces ? "Click a result to load it into the scene." : "Select a result to place it.");
            if (compact)
            {
                if (ImGui.Button("Add all from folder...##mlaaddall")) lib.RequestPickFolderToPlace = true;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Every model of a folder, placed in a grid around the placement point (a picker opens).");
                ImGui.SameLine();
                if (ImGui.Button("Resolve typed##mlares")) lib.RequestResolveTyped = true;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Entities with a name but no model here (from an .mloproj, or typed in): find their models in the archives / folders and place them.");
            }
            else
            {
                ImGui.SetNextItemWidth(-260);
                ImGui.InputTextWithHint("##mlafolder", "a folder of .ydr / .yft to add all of", ref assetFolderToPlace, 260);
                ImGui.SameLine();
                if (ImGui.Button("Add all from folder##mlaaddall"))
                {
                    if (!string.IsNullOrWhiteSpace(assetFolderToPlace)) lib.RequestPlaceFolder = assetFolderToPlace.Trim();
                    else lib.RequestPickFolderToPlace = true;
                }
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Every model of that folder, placed in a grid around the placement point (a picker opens when the box is empty).");
                ImGui.SameLine();
                if (ImGui.Button("Browse...##mlabrowse")) lib.RequestPickFolderToPlace = true;
                ImGui.SameLine();
                if (ImGui.Button("Resolve typed entities##mlares")) lib.RequestResolveTyped = true;
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("Entities with a name but no model here (from an .mloproj, or typed in): find their models in the archives / folders and place them.");
            }

            if (lib.Recent.Count > 0)
            {
                ImGui.TextDisabled("Recent:");
                int shown = compact ? 6 : 12;
                for (int i = 0; i < lib.Recent.Count && i < shown; i++)
                {
                    ImGui.SameLine();
                    var r = lib.Recent[i];
                    if (ImGui.SmallButton(r.Name + "##mlarec" + i)) { lib.Selected = r; lib.RequestPlace = r; }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Place " + r.Name + " again.");
                }
            }
        }
    }
}

