using System;
using System.Numerics;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class MloCreatorPanel
    {
        public int AssetView_O3 = 1;
        public int AssetColumns_O3 = 4;
        private int assetCursor_O3 = -1;
        private bool assetScrollToCursor_O3;

        public static readonly string[] AssetViewNames_O3 = { "List", "Large icons" };

        public void DrawAssetViewSwitch_O3(MloAssetLibrary lib, bool compact)
        {
            ImGui.SetNextItemWidth(compact ? 110 : 130);
            if (ImGui.Combo("##o3assetview", ref AssetView_O3, AssetViewNames_O3, AssetViewNames_O3.Length))
            { assetScrollToCursor_O3 = true; AssetViewChosen_P2 = true; }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("List: one row per model, name and details.\n" +
                                 "Large icons: a grid of big pictures, four to a line - pick a prop by looking at it.");
            if (AssetView_O3 != 1) return;
            ImGui.SameLine();
            ImGui.SetNextItemWidth(compact ? 90 : 110);
            if (UiSlider_U28.Int("##o3assetcols", ref AssetColumns_O3, 2, 8, "%d per line"))
                AssetColumns_O3 = Math.Clamp(AssetColumns_O3, 2, 8);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("How many tiles fit on a line. Fewer = bigger pictures.");
        }

        public bool DrawAssetGrid_O3(MloAssetLibrary lib, bool compact)
        {
            if (AssetView_O3 != 1 || lib == null) return false;
            int n = lib.Results.Count;
            if (n == 0)
            {
                ImGui.TextDisabled(string.IsNullOrWhiteSpace(lib.Query)
                    ? "Type a name to search. Empty search + a source other than the archives lists that source."
                    : "Nothing matches.");
                return true;
            }

            int cols = Math.Clamp(AssetColumns_O3, 1, 8);
            var style = ImGui.GetStyle();
            float avail = ImGui.GetContentRegionAvail().X;
            float cellW = Math.Max((avail - style.ItemSpacing.X * (cols - 1)) / cols, 48.0f);
            float pad = 4.0f;
            float img = Math.Min(cellW - pad * 2, 160.0f);
            float lineH = ImGui.GetTextLineHeight();
            float cellH = img + pad * 2 + lineH + 4;

            HandleAssetKeys_O3(lib, cols);

            int rows = (n + cols - 1) / cols;
            float scrollY = ImGui.GetScrollY(), viewH = ImGui.GetWindowHeight();
            int firstRow = Math.Max(0, (int)(scrollY / (cellH + style.ItemSpacing.Y)) - 1);
            int lastRow = Math.Min(rows - 1, (int)((scrollY + viewH) / (cellH + style.ItemSpacing.Y)) + 1);
            float rowStride = cellH + style.ItemSpacing.Y;

            if (assetScrollToCursor_O3 && assetCursor_O3 >= 0 && assetCursor_O3 < n)
            {
                float want = (assetCursor_O3 / cols) * rowStride;
                if (want < scrollY) ImGui.SetScrollY(want);
                else if (want + cellH > scrollY + viewH) ImGui.SetScrollY(want + cellH - viewH);
                assetScrollToCursor_O3 = false;
            }

            if (firstRow > 0) ImGui.Dummy(new Vector2(1, firstRow * rowStride));
            var dl = ImGui.GetWindowDrawList();
            uint colFrame = ImGui.GetColorU32(ImGuiCol.FrameBg);
            uint colText = ImGui.GetColorU32(ImGuiCol.Text);
            uint colDim = ImGui.GetColorU32(ImGuiCol.TextDisabled);
            uint colSel = ImGui.GetColorU32(ImGuiCol.HeaderActive);
            uint colCursor = ImGui.ColorConvertFloat4ToU32(UiTheme.AccentBright);

            for (int r = firstRow; r <= lastRow; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int i = r * cols + c;
                    if (i >= n) break;
                    if (c > 0) ImGui.SameLine();
                    var it = lib.Results[i];
                    ImGui.PushID(i);
                    var p0 = ImGui.GetCursorScreenPos();
                    bool sel = lib.Selected == it;
                    if (ImGui.Selectable("##o3tile", sel, ImGuiSelectableFlags.AllowDoubleClick, new Vector2(cellW, cellH)))
                    {
                        lib.Selected = it;
                        assetCursor_O3 = i;
                        if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left)) { if (!lib.ClickPlaces) lib.RequestPlace = it; }
                        else if (lib.ClickPlaces) lib.RequestPlace = it;
                    }
                    bool hovered = ImGui.IsItemHovered();
                    if (hovered)
                        ImGui.SetTooltip($"{it.FileName}\n{(it.FromArchive ? "game archives: " : "")}{it.Path}\n{it.SourceLabel}" +
                                         $"{(it.LightCount > 0 ? $", {it.LightCount} light(s)" : "")}{(it.Size > 0 ? $", {it.Size / 1024.0:0.#} KB" : "")}\n\n" +
                                         (lib.ClickPlaces ? "Click to load it into the scene at the place chosen above; drag into the viewport to drop it under the cursor."
                                                          : "Double-click to place; drag into the viewport to drop it under the cursor."));
                    if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 6.0f)) lib.BeginDrag?.Invoke(it.ToEntry());

                    var q0 = new Vector2(p0.X + (cellW - img) * 0.5f, p0.Y + pad);
                    var q1 = new Vector2(q0.X + img, q0.Y + img);
                    if (sel) dl.AddRectFilled(p0, new Vector2(p0.X + cellW, p0.Y + cellH), colSel, 4.0f);
                    var tex = lib.ShowThumbnails && lib.ThumbnailOf != null ? lib.ThumbnailOf(it.ToEntry()) : IntPtr.Zero;
                    if (tex != IntPtr.Zero) dl.AddImage(tex, q0, q1);
                    else
                    {
                        dl.AddRectFilled(q0, q1, colFrame, 4.0f);
                        string ph = lib.ShowThumbnails ? "..." : (it.IsYft ? "yft" : "ydr");
                        var ts = ImGui.CalcTextSize(ph);
                        dl.AddText(new Vector2(q0.X + (img - ts.X) * 0.5f, q0.Y + (img - ts.Y) * 0.5f), colDim, ph);
                    }
                    if (i == assetCursor_O3) dl.AddRect(q0 - new Vector2(2, 2), q1 + new Vector2(2, 2), colCursor, 4.0f, ImDrawFlags.None, 2.0f);
                    else if (hovered) dl.AddRect(q0 - new Vector2(1, 1), q1 + new Vector2(1, 1), colDim, 4.0f);

                    string label = Fit_O3(it.Name, cellW - 4);
                    var lw = ImGui.CalcTextSize(label);
                    dl.AddText(new Vector2(p0.X + (cellW - lw.X) * 0.5f, q1.Y + 3), colText, label);
                    if (it.LightCount > 0)
                    {
                        string lc = it.LightCount + "L";
                        var ls = ImGui.CalcTextSize(lc);
                        dl.AddRectFilled(new Vector2(q1.X - ls.X - 5, q0.Y + 1), new Vector2(q1.X - 1, q0.Y + ls.Y + 3), colFrame, 3.0f);
                        dl.AddText(new Vector2(q1.X - ls.X - 3, q0.Y + 2), colCursor, lc);
                    }
                    ImGui.PopID();
                }
            }
            int belowRows = rows - 1 - lastRow;
            if (belowRows > 0) ImGui.Dummy(new Vector2(1, belowRows * rowStride));
            return true;
        }

        private void HandleAssetKeys_O3(MloAssetLibrary lib, int cols)
        {
            int n = lib.Results.Count;
            if (n == 0) return;
            if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.ChildWindows) && !ImGui.IsWindowFocused(ImGuiFocusedFlags.ChildWindows)) return;
            if (assetCursor_O3 < 0 || assetCursor_O3 >= n) assetCursor_O3 = Math.Max(lib.Results.IndexOf(lib.Selected), 0);
            int move = 0;
            if (ImGui.IsKeyPressed(ImGuiKey.RightArrow)) move = 1;
            else if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow)) move = -1;
            else if (ImGui.IsKeyPressed(ImGuiKey.DownArrow)) move = cols;
            else if (ImGui.IsKeyPressed(ImGuiKey.UpArrow)) move = -cols;
            else if (ImGui.IsKeyPressed(ImGuiKey.Home)) { assetCursor_O3 = 0; move = 0; assetScrollToCursor_O3 = true; lib.Selected = lib.Results[0]; }
            else if (ImGui.IsKeyPressed(ImGuiKey.End)) { assetCursor_O3 = n - 1; move = 0; assetScrollToCursor_O3 = true; lib.Selected = lib.Results[n - 1]; }
            if (move != 0)
            {
                assetCursor_O3 = Math.Clamp(assetCursor_O3 + move, 0, n - 1);
                lib.Selected = lib.Results[assetCursor_O3];
                assetScrollToCursor_O3 = true;
            }
            if (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter))
            {
                if (assetCursor_O3 >= 0 && assetCursor_O3 < n) { lib.Selected = lib.Results[assetCursor_O3]; lib.RequestPlace = lib.Selected; }
            }
        }

        private static string Fit_O3(string s, float width)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (ImGui.CalcTextSize(s).X <= width) return s;
            for (int cut = 1; cut < s.Length; cut++)
            {
                string t = "..." + s.Substring(cut);
                if (ImGui.CalcTextSize(t).X <= width) return t;
            }
            return "...";
        }
    }
}

