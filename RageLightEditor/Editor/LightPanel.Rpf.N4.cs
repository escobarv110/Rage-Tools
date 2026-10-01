using System;
using System.Collections.Generic;
using System.Numerics;
using CodeWalker.GameFiles;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        public readonly RpfExplorer Rpf = new RpfExplorer();

        public RpfFileEntry RequestRpfView;
        public RpfFileEntry RequestRpfExtract;
        public RpfFileEntry RequestRpfExportXml_V40;
        public RpfFileEntry RequestRpfHex_V40;
        public string RequestRpfDiskExportXml_V42;
        public string RequestRpfDiskHex_V42;
        public string RequestRpfDiskConvert_V52;
        public List<string> RequestRpfDiskConvertMany_V52;
        public System.Collections.Generic.List<RpfFileEntry> RequestRpfExportXmlMany_V40;
        public RpfDirectoryEntry RequestRpfExtractFolder;
        public List<RpfFileEntry> RequestRpfExtractMany;
        public RpfFileEntry RequestRpfSpawn;
        public RpfFileEntry RequestRpfToMloCreator;
        public string RpfStatus = "";

        public string RpfViewTitle;
        private string[] rpfViewLines = Array.Empty<string>();
        private string rpfViewRaw = "";
        public bool RpfViewOpen;

        public void ShowRpfText(string title, string text)
        {
            ClearRpfViewSource_Q1();
            RpfViewTitle = title;
            rpfViewRaw = text ?? "";
            rpfViewLines = rpfViewRaw.Replace("\r\n", "\n").Split('\n');
            RpfViewOpen = true;
            NewRpfText_R3(RpfViewTitle, rpfViewRaw);
        }

        private string rpfSearch = "";
        private bool rpfSearchAll = true;
        private bool rpfSearchBranch = true;
        private bool lastRpfSearchBranch = true;
        private string lastRpfBranchPrefix = "\0";
        private static readonly string[] RpfScopeLabels_V23 = { "Branch", "Install", "Folder" };
        private string lastRpfSearch = "\0";
        private bool lastRpfSearchAll;
        private readonly List<ArchiveBrowser.Entry> rpfHits = new List<ArchiveBrowser.Entry>();

        public void SetRpfSearch(string text, bool allArchives)
        {
            rpfSearch = text ?? "";
            rpfSearchAll = allArchives;
        }

        private int rpfHitTotal;

        private static Vector4 RpfWorkspaceColour => ArchiveWorkspaceColour;

        partial void ClearRpfViewSource_Q1();

        private void DrawRpfLeft_N4(float displayHeight)
        {
            DrawWorkspaceLogo(150.0f);

            DrawRpfExplorerNote_Q1();
            ImGui.TextDisabled("FOLDERS");
            if (Archive == null || !Archive.Ready)
            {
                if (RpfIndexWarmingNote_S3()) return;
                ImGui.TextWrapped(string.IsNullOrEmpty(GameLoadStatus)
                    ? "Waiting for the game archives to open. Set the GTA V folder in the Lights " +
                      "workspace if this does not finish."
                    : GameLoadStatus);
                return;
            }
            EnsureRpfTree_O1();

            ImGui.SameLine();
            ImGui.Text($"({Archive.FileCount:N0} files)");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{Archive.FileCount:N0} archived files indexed for the whole-install search.\n" +
                                 "The tree shows the install itself: folders, loose files and .rpf\n" +
                                 "archives, which open like folders.");

            DrawRpfTreeButtons_O1();
            if (ImGui.Button("Collapse all", new Vector2(-1, 0)))
                foreach (var r in Rpf.Roots) CollapseAll_N4(r);
            ImGui.TextDisabled("Drop a folder here to open it");

            ImGui.Spacing();
            var acc = UiTheme.Accent;
            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(acc.X, acc.Y, acc.Z, 0.30f));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(1, 1, 1, 0.07f));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(acc.X, acc.Y, acc.Z, 0.42f));
            if (ImGui.BeginChild("##rpftree", new Vector2(0, displayHeight - 200), ImGuiChildFlags.Borders))
            {
                bool atTop = Rpf.Current == null;
                if (ImGui.Selectable("All folders##rpfroot", atTop)) Rpf.Go(null);
                foreach (var r in Rpf.Roots) DrawRpfNode_N4(r);
            }
            ImGui.EndChild();
            ImGui.PopStyleColor(3);
        }

        private static void CollapseAll_N4(RpfExplorer.Node n)
        {
            n.Expanded = false;
            if (n.Children == null) return;
            foreach (var c in n.Children) CollapseAll_N4(c);
        }

        private void DrawRpfNode_N4(RpfExplorer.Node n)
        {
            ImGui.PushID(n.Path ?? n.Label);
            bool sel = ReferenceEquals(Rpf.Current, n);
            var flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
            if (sel) flags |= ImGuiTreeNodeFlags.Selected;
            bool leaf = n.Children != null && n.Children.Count == 0;
            if (leaf) flags |= ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;
            ImGui.SetNextItemOpen(n.Expanded, ImGuiCond.Always);

            bool open = ImGui.TreeNodeEx("##n", flags);
            DrawRpfNodeOpening_U1(n);
            bool toggled = ImGui.IsItemToggledOpen();
            bool clicked = ImGui.IsItemClicked() && !toggled;
            bool hovered = ImGui.IsItemHovered();

            ImGui.SameLine(0, 0);
            RpfTreeGlyph_V43(n);
            ImGui.SameLine(0, 5);
            ImGui.TextUnformatted(n.Label ?? "?");

            if (toggled) n.Expanded = !n.Expanded;
            else if (clicked)
            {
                Rpf.Go(n);
                n.Expanded = true;
            }
            if (hovered) ImGui.SetTooltip(n.Path ?? "");

            if (open && !leaf)
            {
                foreach (var c in Rpf.ChildrenOf(n)) DrawRpfNode_N4(c);
                ImGui.TreePop();
            }
            ImGui.PopID();
        }

        private void DrawRpfList_N4(float displayWidth, float displayHeight)
        {
            float x0 = (ShowLeftPanel ? settings.LeftPanelWidth : 0.0f) + HandleStripW;
            float x1 = displayWidth - (ShowRightPanel ? settings.RightPanelWidth : 0.0f) - HandleStripW;
            float y0 = TopBarHeight;
            float w = Math.Max(x1 - x0, 240.0f);
            float h = Math.Max(displayHeight - y0, 160.0f);

            ImGui.SetNextWindowPos(new Vector2(x0, y0), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(w, h), ImGuiCond.Always);
            var wf = ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse
                   | ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoBringToFrontOnFocus;
            if (!ImGui.Begin("##rpflist", wf)) { ImGui.End(); return; }

            DrawRpfToolbar_N4();
            DrawRpfEditToolbar_O1();
            DrawRpfEditBanner_O1();
            ImGui.Separator();
            DrawRpfOpenProgress_U1();
            DrawRpfRows_N4();

            ImGui.Separator();
            DrawRpfFooter_O1();
            DrawRpfModals_O1();
            ImGui.End();

            DrawRpfTextWindow_N4(displayWidth, displayHeight);
        }

        private void DrawRpfToolbar_N4()
        {
            ImGui.BeginDisabled(!Rpf.CanGoBack);
            if (ImGui.Button("<", new Vector2(26, 0))) Rpf.GoBack();
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Back");
            ImGui.SameLine(0, 3);
            ImGui.BeginDisabled(!Rpf.CanGoForward);
            if (ImGui.Button(">", new Vector2(26, 0))) Rpf.GoForward();
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Forward");
            ImGui.SameLine(0, 3);
            ImGui.BeginDisabled(Rpf.Current == null);
            if (ImGui.Button("^", new Vector2(26, 0))) Rpf.GoUp();
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Up one level");

            ImGui.SameLine(0, 10);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
            if (ImGui.SmallButton("All folders")) Rpf.Go(null);
            var trail = Rpf.CrumbTrail();
            for (int i = 0; i < trail.Count; i++)
            {
                ImGui.SameLine(0, 2);
                ImGui.TextDisabled(">");
                ImGui.SameLine(0, 2);
                bool tail = i == trail.Count - 1;
                if (tail) ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Accent);
                if (ImGui.SmallButton((trail[i].Label ?? "?") + "##crumb" + i)) Rpf.Go(trail[i]);
                if (tail) ImGui.PopStyleColor();
            }
            ImGui.PopStyleColor();

            ImGui.SetNextItemWidth(240);
            string branchLabel = Rpf.CurrentBranchLabel_V23();
            bool changed = ImGui.InputTextWithHint("##rpfsearch",
                !rpfSearchAll ? "filter this folder..."
                : rpfSearchBranch && Rpf.CurrentBranchPrefix_V23() != null ? $"search inside {branchLabel}..."
                : "search every archive by name...",
                ref rpfSearch, 96);
            ImGui.SameLine();
            int scope = !rpfSearchAll ? 2 : (rpfSearchBranch ? 0 : 1);
            ImGui.SetNextItemWidth(150);
            if (ImGui.Combo("##rpfscope", ref scope, RpfScopeLabels_V23, RpfScopeLabels_V23.Length))
            {
                rpfSearchAll = scope != 2;
                rpfSearchBranch = scope == 0;
                changed = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Selected branch: every file under the tree node you are on, however deep\n" +
                                 "(an archive, a folder of archives, a folder inside one).\n" +
                                 "Whole install: every file in every archive - CodeWalker's Find.\n" +
                                 "This folder only: a filter over the listing in front of you.\n" +
                                 "The Path column says where each hit lives; double-click one to walk there.");
            ImGui.SameLine();
            if (ImGui.Button("Clear")) { rpfSearch = ""; changed = true; }
            changed |= DrawRpfSearchScope_S3();

            string branchPrefix = rpfSearchBranch ? Rpf.CurrentBranchPrefix_V23() : null;
            if (changed || rpfSearch != lastRpfSearch || rpfSearchAll != lastRpfSearchAll ||
                rpfSearchBranch != lastRpfSearchBranch || (rpfSearchBranch && branchPrefix != lastRpfBranchPrefix))
            {
                lastRpfSearch = rpfSearch;
                lastRpfSearchAll = rpfSearchAll;
                lastRpfSearchBranch = rpfSearchBranch;
                lastRpfBranchPrefix = branchPrefix;
                if (rpfSearchAll && rpfSearch.Trim().Length > 0)
                {
                    string diskBranch = rpfSearchBranch ? Rpf.DiskBranchToSearch_V35() : null;
                    if (diskBranch != null)
                    {
                        rpfHitTotal = RunRpfDiskFind_V35(rpfSearch, diskBranch, branchLabel);
                        Rpf.ShowDiskSearchResults_V35(rpfDiskHits_V35);
                        rpfDiskResults_V42 = true;
                    }
                    else
                    {
                        rpfHitTotal = RunRpfFindAll_S3(rpfSearch, rpfHits, branchPrefix, rpfSearchBranch ? branchLabel : null);
                        if (!rpfSearchBranch) AppendOutsideFolderHits_V35(rpfSearch);
                        Rpf.ShowSearchResults(rpfHits);
                        rpfDiskResults_V42 = false;
                    }
                }
                else
                {
                    rpfHits.Clear();
                    rpfHitTotal = 0;
                    rpfDiskResults_V42 = false;
                    Rpf.Filter = rpfSearch;
                    Rpf.Invalidate();
                }
            }
        }

        private void ActivateRpfRow_N4(in RpfExplorer.Row r)
        {
            if (r.IsFolder)
            {
                if (!Rpf.EnterRow_O1(r)) RpfStatus = "could not walk to " + r.Name;
                rpfSearch = ""; rpfSearchAll = false;
                ClearRpfSelection_O1();
                return;
            }
            if (r.Entry is RpfFileEntry fe) RequestRpfView = fe;
            else if (r.IsFs) RequestRpfOpenDiskFile = r.Path;
        }

        private bool GoToDir_N4(RpfDirectoryEntry dir)
        {
            if (Rpf.Current != null)
            {
                foreach (var c in Rpf.ChildrenOf(Rpf.Current))
                    if (ReferenceEquals(c.Dir, dir)) { Rpf.Go(c); return true; }
            }
            else
            {
                foreach (var r in Rpf.Roots)
                    if (ReferenceEquals(r.Dir, dir)) { Rpf.Go(r); return true; }
            }
            return Rpf.Reveal(dir);
        }

        private void DrawRpfRowMenu_N4(in RpfExplorer.Row r)
        {
            if (RpfMultiSelected_U19) { DrawRpfMultiMenu_U19(); return; }
            var fe = r.Entry as RpfFileEntry;
            string kind = RpfExplorer.ViewKindOf(r.Entry);
            if (r.IsFolder)
            {
                if (ImGui.MenuItem("Open")) ActivateRpfRow_N4(r);
                if (r.EnterDir != null && ImGui.MenuItem("Extract this folder...")) RequestRpfExtractFolder = r.EnterDir;
            }
            else if (r.IsFs)
            {
                if (ImGui.MenuItem("Open")) RequestRpfOpenDiskFile = r.Path;
                if (MainForm.IsConvertibleXml_V52(r.Name) &&
                    ImGui.MenuItem("Convert to " + System.IO.Path.GetExtension(MainForm.ConvertTargetName_V52(r.Name))))
                    RequestRpfDiskConvert_V52 = r.Path;
                if (ImGui.MenuItem("Export XML...")) RequestRpfDiskExportXml_V42 = r.Path;
                if (ImGui.MenuItem("View hex")) RequestRpfDiskHex_V42 = r.Path;
                if (ImGui.MenuItem("Show in Windows Explorer")) RequestRpfShowInExplorer = r.Path;
            }
            else if (fe != null)
            {
                if (ImGui.MenuItem(kind != null ? "View (" + kind + ")" : "View", null, false, kind != null))
                    RequestRpfView = fe;
                if (ImGui.MenuItem("Extract...")) RequestRpfExtract = fe;
                if (ImGui.MenuItem("Export XML...")) RequestRpfExportXml_V40 = fe;
                if (ImGui.MenuItem("View hex")) RequestRpfHex_V40 = fe;
                var k = ArchiveBrowser.KindOf(fe);
                if (k == "ydr" || k == "yft")
                {
                    if (ImGui.MenuItem("Place in the world")) RequestRpfSpawn = fe;
                    if (ImGui.MenuItem("Send to the MLO Creator")) RequestRpfToMloCreator = fe;
                }
            }
            ImGui.Separator();
            if (fe != null && ImGui.MenuItem("Open file location"))
                RequestRpfShowInExplorer = fe.File?.GetPhysicalFilePath();
            if (ImGui.MenuItem("Copy path")) ImGui.SetClipboardText(r.Path ?? "");
            if (ImGui.MenuItem("Copy name")) ImGui.SetClipboardText(r.Name ?? "");
            DrawRpfRowEditMenu_O1(r);
        }

        private void DrawRpfRight_N4()
        {
            ImGui.TextDisabled("SELECTED");
            if (RpfMultiSelected_U19) { DrawRpfMultiSelected_U19(); return; }
            if (rpfHasSelRow && rpfSelRow.IsFs) { DrawRpfFsSelected_O1(); return; }
            var e = Rpf.Selected;
            if (e == null)
            {
                ImGui.TextWrapped("Nothing selected. Walk the tree on the left, or search across " +
                                  "every archive from the box above the list, then click a row.");
                ImGui.Spacing();
                ImGui.Separator();
                DrawRpfFolderActions_N4();
                if (ArchivePreview != null)
                {
                    ImGui.Spacing();
                    ImGui.Separator();
                    DrawArchivePreviewPanel();
                }
                return;
            }

            ImGui.TextWrapped(e.Name ?? "");
            ImGui.TextDisabled(RpfExplorer.TypeNameOf(e));
            var fe = e as RpfFileEntry;
            if (fe != null)
            {
                ImGui.TextDisabled(RpfExplorer.SizeText(fe.GetFileSize()) + " in memory   " +
                                   RpfExplorer.SizeText(fe.FileSize) + " packed");
                if (fe is RpfResourceFileEntry rr)
                    ImGui.TextDisabled($"resource v{rr.Version}   sys {rr.SystemSize / 1024} KB   gfx {rr.GraphicsSize / 1024} KB");
                if (fe.IsEncrypted) ImGui.TextDisabled("encrypted in the archive");
            }
            ImGui.Spacing();
            ImGui.TextDisabled("Where it lives");
            ImGui.TextWrapped(e.Path ?? "");
            if (ImGui.Button("Copy path", new Vector2(-1, 0))) ImGui.SetClipboardText(e.Path ?? "");

            ImGui.Spacing();
            ImGui.Separator();

            if (fe != null)
            {
                string kind = RpfExplorer.ViewKindOf(fe);
                ImGui.BeginDisabled(kind == null);
                if (ImGui.Button(kind == null ? "View" : "View (" + kind + ")", new Vector2(-1, 0)))
                    RequestRpfView = fe;
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(kind == null
                        ? "Nothing here reads this format - extract it and open it elsewhere."
                        : kind == "model" ? "Opens in the model viewer window: geometry tree, materials,\ndetails and options. Nothing is added to any workspace."
                        : kind == "textures" ? "Opens in the model viewer window, on its Textures tab."
                        : kind == "collision" ? "Opens in the model viewer window, drawn as collision."
                        : kind == "particles" ? "Opens in the Particles workspace."
                        : "Converted to XML and shown as text.");

                if (ImGui.Button("Extract...", new Vector2(-1, 0))) RequestRpfExtract = fe;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Written in the on-disk form - RSC7 header back on for a resource -\n" +
                                     "so OpenIV, CodeWalker and this editor can all read it back.");

                float halfX = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f;
                if (ImGui.Button("Export XML...", new Vector2(halfX, 0))) RequestRpfExportXml_V40 = fe;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Convert it to XML and write it to a file, so another tool can read it - or you can edit it and convert it back.");
                ImGui.SameLine();
                if (ImGui.Button("View hex", new Vector2(halfX, 0))) RequestRpfHex_V40 = fe;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The raw bytes, with their offsets and characters. What to reach for when a file has no XML converter, or has one and comes out wrong.");

                DrawRpfMetaActions_Q1(fe);

                var k = ArchiveBrowser.KindOf(fe);
                if (k == "ydr" || k == "yft")
                {
                    if (ImGui.Button("Place in the world", new Vector2(-1, 0))) RequestRpfSpawn = fe;
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Adds it to the project's ymap as an entity in front of the camera\n" +
                                         "(the World workspace's own placement path).");
                    if (ImGui.Button("Send to the MLO Creator", new Vector2(-1, 0))) RequestRpfToMloCreator = fe;
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Extracts it next to the MLO project and puts it in the assets library.");
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            DrawRpfFolderActions_N4();

            if (ArchivePreview != null)
            {
                ImGui.Spacing();
                ImGui.Separator();
                DrawArchivePreviewPanel();
            }
        }

        private void DrawRpfFolderActions_N4()
        {
            int files = 0;
            foreach (var r in Rpf.Rows) if (!r.IsFolder && r.Entry is RpfFileEntry) files++;
            if (files > 0)
            {
                if (ImGui.Button($"Extract all {files:N0} listed files...", new Vector2(-1, 0)))
                {
                    var list = new List<RpfFileEntry>(files);
                    foreach (var r in Rpf.Rows)
                        if (!r.IsFolder && r.Entry is RpfFileEntry fe) list.Add(fe);
                    RequestRpfExtractMany = list;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The files in the list as it stands - filtered or searched.\n" +
                                     "They land flat in one folder, so two files of the same name\n" +
                                     "from different archives get their archive's name appended.");
            }

            ImGui.TextDisabled("THIS FOLDER");
            if (Rpf.Current == null)
            {
                ImGui.TextWrapped("Standing at the top. Open GTA V to walk the install, or open " +
                                  "another folder beside it.");
                return;
            }
            ImGui.TextWrapped(Rpf.CurrentDisplayPath);
            if (Rpf.Current.Dir != null)
            {
                if (ImGui.Button("Extract this folder...", new Vector2(-1, 0)))
                    RequestRpfExtractFolder = Rpf.Current.Dir;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Everything under here, recursively, mirroring the archive's layout.\n" +
                                     "Nested .rpf archives are walked into rather than copied whole.");
            }
            else
            {
                int conv = 0;
                foreach (var r in Rpf.Rows)
                    if (r.IsFs && !r.IsFolder && MainForm.IsConvertibleXml_V52(r.Name)) conv++;
                if (conv > 0)
                {
                    if (ImGui.Button($"Convert all {conv} XML file(s) to game files", new Vector2(-1, 0)))
                    {
                        var list = new List<string>(conv);
                        foreach (var r in Rpf.Rows)
                            if (r.IsFs && !r.IsFolder && MainForm.IsConvertibleXml_V52(r.Name)) list.Add(r.Path);
                        RequestRpfDiskConvertMany_V52 = list;
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Each <name>.ybn.xml / .ydr.xml / ... becomes the real <name>.ybn / .ydr\n" +
                                         "beside it - the same conversion CodeWalker's import runs, on the whole folder.");
                }
                if (ImGui.Button("Show this folder in Explorer", new Vector2(-1, 0)))
                    RequestRpfShowInExplorer = Rpf.Current.FsPath;
            }
        }

        private void DrawRpfTextWindow_N4(float displayWidth, float displayHeight)
        {
            { bool r3 = false; RpfTextWindow_R3(displayWidth, displayHeight, ref r3); if (r3) return; }
            if (!RpfViewOpen) return;
            ImGui.SetNextWindowSize(new Vector2(Math.Min(900, displayWidth * 0.6f),
                                                Math.Min(640, displayHeight * 0.7f)), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowPos(new Vector2(displayWidth * 0.25f, displayHeight * 0.18f), ImGuiCond.FirstUseEver);
            bool open = RpfViewOpen;
            if (!ImGui.Begin((RpfViewTitle ?? "file") + "##rpftext", ref open))
            {
                RpfViewOpen = open;
                ImGui.End();
                return;
            }
            RpfViewOpen = open;

            ImGui.TextDisabled($"{rpfViewLines.Length:N0} lines   {rpfViewRaw.Length / 1024:N0} KB");
            ImGui.SameLine();
            if (ImGui.Button("Copy all")) ImGui.SetClipboardText(rpfViewRaw);
            DrawRpfViewActions_Q1();
            ImGui.Separator();
            if (ImGui.BeginChild("##rpftextbody", new Vector2(0, 0), ImGuiChildFlags.Borders,
                                 ImGuiWindowFlags.HorizontalScrollbar))
            {
                float rowH = ImGui.GetTextLineHeightWithSpacing();
                float viewH = ImGui.GetWindowSize().Y;
                float scroll = ImGui.GetScrollY();
                int first = Math.Max(0, (int)(scroll / rowH) - 2);
                int last = Math.Min(rpfViewLines.Length, first + (int)(viewH / rowH) + 5);
                if (first > 0) ImGui.Dummy(new Vector2(1, first * rowH));
                for (int i = first; i < last; i++) ImGui.TextUnformatted(rpfViewLines[i]);
                if (last < rpfViewLines.Length)
                    ImGui.Dummy(new Vector2(1, (rpfViewLines.Length - last) * rowH));
            }
            ImGui.EndChild();
            ImGui.End();
        }
    }
}

