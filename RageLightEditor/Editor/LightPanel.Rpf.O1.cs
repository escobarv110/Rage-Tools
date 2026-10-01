using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using CodeWalker.GameFiles;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {

        public bool RpfEditMode;
        public string RpfGameFolder = "";

        public bool RequestRpfImport, RequestRpfImportRaw;
        public bool RequestRpfPasteOs;
        public bool RequestRpfOpenFolder;
        public string RequestRpfOpenDiskFile;
        public string RequestRpfShowInExplorer;

        private RpfExplorer.Row rpfSelRow;
        private bool rpfHasSelRow;
        private RpfExplorer.Node rpfSelNode;

        private enum RpfPromptKind { None, NewFolder, NewArchive, Rename }
        private RpfPromptKind rpfPrompt;
        private string rpfPromptText = "";
        private bool rpfPromptOpening;
        private RpfEdit.Item rpfPromptItem;

        private string rpfConfirmTitle = "", rpfConfirmBody = "", rpfConfirmYes = "Yes";
        private Action rpfConfirmAction;
        private bool rpfConfirmOpening;

        private bool rpfDefragOpen, rpfDefragOpening;
        private RpfFile rpfDefragTarget;
        private bool rpfDefragRecursive = true;

        private bool RpfRowSelected_O1(in RpfExplorer.Row r)
        {
            if (!rpfHasSelRow) return false;
            if (InMulti_U19(r)) return true;
            if (r.Entry != null || rpfSelRow.Entry != null) return ReferenceEquals(r.Entry, rpfSelRow.Entry);
            return string.Equals(r.Path, rpfSelRow.Path, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(r.Name, rpfSelRow.Name, StringComparison.OrdinalIgnoreCase);
        }

        private void SelectRpfRow_O1(in RpfExplorer.Row r)
        {
            rpfSelRow = r;
            rpfHasSelRow = true;
            rpfSelNode = Rpf.Current;
            Rpf.Selected = r.Entry;
            rpfMulti_U19.Clear();
            rpfMulti_U19.Add(r);
        }

        private void ClearRpfSelection_O1()
        {
            rpfHasSelRow = false;
            rpfSelNode = Rpf.Current;
            Rpf.Selected = null;
            rpfMulti_U19.Clear();
            rpfAnchor_U19 = -1;
        }

        private void ForgetStaleRpfSelection_O1()
        {
            if (rpfHasSelRow && !ReferenceEquals(rpfSelNode, Rpf.Current)) ClearRpfSelection_O1();
        }

        private RpfEdit.Item RpfSelectedItem_O1() =>
            rpfHasSelRow ? RpfEdit.ItemOf(rpfSelRow) : default;

        private RpfEdit.Target RpfTarget_O1() => RpfEdit.TargetOf(Rpf);

        private void EnsureRpfTree_O1()
        {
            if (Rpf.Ready) return;
            Rpf.BuildFromGameFolder(RpfGameFolder, Archive);
        }

        public static readonly Vector4 EditModeOnText_V4  = new Vector4(1.00f, 0.86f, 0.82f, 1.00f);
        public static readonly Vector4 EditModeOnFill_V4  = new Vector4(0.78f, 0.16f, 0.12f, 1.00f);
        public static readonly Vector4 EditModeOnHover_V4 = new Vector4(0.90f, 0.24f, 0.18f, 1.00f);
        public static readonly Vector4 EditModeOnTick_V4  = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);

        private void DrawRpfEditToolbar_O1()
        {
            ForgetStaleRpfSelection_O1();
            var t = RpfTarget_O1();
            bool canWrite = RpfEditMode && t.Valid;
            bool haveSel = rpfHasSelRow;
            bool searching = rpfSearchAll && rpfSearch.Trim().Length > 0;

            bool was = RpfEditMode;
            if (was)
            {
                ImGui.PushStyleColor(ImGuiCol.Text,           EditModeOnText_V4);
                ImGui.PushStyleColor(ImGuiCol.FrameBg,        EditModeOnFill_V4);
                ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, EditModeOnHover_V4);
                ImGui.PushStyleColor(ImGuiCol.FrameBgActive,  EditModeOnHover_V4);
                ImGui.PushStyleColor(ImGuiCol.CheckMark,      EditModeOnTick_V4);
            }
            if (ImGui.Checkbox("Edit mode", ref RpfEditMode))
            {
                if (!was && RpfEditMode)
                {
                    RpfEditMode = false;
                    AskRpf_O1("Warning - Entering edit mode",
                              "While in edit mode, all changes are automatically saved.\n" +
                              "There is no undo. Do you want to continue?\n\n" +
                              "Close GTA V first. There is no way to tell from here whether the game\n" +
                              "has an archive open, and writing to one it is reading can break it.\n" +
                              "The first change to an archive keeps a copy of it as <name>.rpf.bak.",
                              "Enter edit mode",
                              () =>
                              {
                                  RpfEditMode = true;
                                  RpfStatus = "edit mode is ON - changes are written immediately";
                              });
                }
                else RpfStatus = "edit mode is off - the archives are read-only again";
            }
            if (was) ImGui.PopStyleColor(5);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Off, nothing can be written. On, New folder / New archive / Import /\n" +
                                 "Paste / Rename / Delete write into the archive or the folder you are\n" +
                                 "standing in, immediately and with no undo.");

            ImGui.SameLine(0, 12);
            string whyNot = WhyNoWrite_U4(t, searching);
            ImGui.BeginDisabled(!RpfWriteGate_U4.Enabled(t.Valid, searching));
            if (ImGui.Button("Import..."))
                if (EnsureRpfWritable_U4("Importing", () => AskThenImport_S3(false))) AskThenImport_S3(false);
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(whyNot ??
                                 "Files from disk into this folder. A file named prop.ytyp.xml is\n" +
                                 "converted back to binary on the way in; anything else goes in as\n" +
                                 "it is, resources keeping their RSC7 header.");
            ImGui.SameLine(0, 4);
            if (ImGui.Button("Import raw..."))
                if (EnsureRpfWritable_U4("Importing", () => AskThenImport_S3(true))) AskThenImport_S3(true);
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(whyNot ?? "The same, with no XML conversion - the bytes exactly as they are.");
            ImGui.EndDisabled();

            RpfToolbarFit_V42(180);
            ImGui.BeginDisabled(!haveSel);
            if (ImGui.Button("Copy")) ApplyRpf_O1(RpfEdit.Copy(SelectedRpfRows_O1()));
            ImGui.SameLine(0, 4);
            if (ImGui.Button(RpfMultiSelected_U19 ? "Copy paths" : "Copy path"))
            {
                if (RpfMultiSelected_U19) CopyRpfPaths_U19(SelectedRpfRows_O1(), false);
                else
                {
                    ImGui.SetClipboardText(rpfSelRow.Path ?? "");
                    RpfStatus = "copied " + (rpfSelRow.Path ?? "");
                }
            }
            ImGui.EndDisabled();

            ImGui.SameLine(0, 4);
            ImGui.BeginDisabled(!RpfWriteGate_U4.Enabled(t.Valid, searching));
            if (ImGui.Button("Paste"))
            {
                void DoPaste()
                {
                    if (RpfEdit.Clipboard.Count == 0) RequestRpfPasteOs = true;
                    else ApplyRpfWrite_O1(() => RpfEdit.Paste(RpfEditMode, Rpf, RpfTarget_O1(), null));
                }
                if (EnsureRpfWritable_U4("Pasting", DoPaste)) DoPaste();
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && whyNot != null) ImGui.SetTooltip(whyNot);
            else if (ImGui.IsItemHovered())
                ImGui.SetTooltip(RpfEdit.Clipboard.Count > 0
                    ? $"{RpfEdit.Clipboard.Count:N0} copied item(s) into this folder."
                    : "Nothing copied here - pastes the files on the Windows clipboard instead.");
            ImGui.EndDisabled();

            RpfToolbarFit_V42(150);
            ImGui.BeginDisabled(!RpfEditMode || !haveSel || searching || RpfMultiSelected_U19);
            if (ImGui.Button("Rename")) OpenRpfPrompt_O1(RpfPromptKind.Rename, rpfSelRow.Name);
            ImGui.EndDisabled();
            if (RpfMultiSelected_U19 && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Renaming is one item at a time.");
            ImGui.SameLine(0, 4);
            ImGui.BeginDisabled(!RpfEditMode || !haveSel || searching);
            if (ImGui.Button(RpfMultiSelected_U19 ? $"Delete {RpfSelectedCount_U19:N0}" : "Delete")) AskDeleteRpf_O1();
            ImGui.EndDisabled();

            var defragTarget = SelectedArchive_O1();
            RpfToolbarFit_V42(120);
            ImGui.BeginDisabled(!RpfEditMode || defragTarget == null);
            if (ImGui.Button("Defragment..."))
            {
                rpfDefragTarget = defragTarget;
                rpfDefragOpen = rpfDefragOpening = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(defragTarget == null
                    ? "Select an .rpf archive to repack it."
                    : "Repack " + defragTarget.Name + " so its files sit end to end.\n" +
                      "Deleting and replacing entries leaves holes the header still allocates.");
            ImGui.EndDisabled();
        }

        private RpfFile SelectedArchive_O1()
        {
            if (!rpfHasSelRow) return null;
            var item = RpfEdit.ItemOf(rpfSelRow);
            if (item.Archive != null) return item.Archive;
            return null;
        }

        private List<RpfExplorer.Row> SelectedRpfRows_O1()
        {
            var list = new List<RpfExplorer.Row>(rpfMulti_U19.Count + 1);
            if (rpfMulti_U19.Count > 0) list.AddRange(rpfMulti_U19);
            else if (rpfHasSelRow) list.Add(rpfSelRow);
            return list;
        }

        private void DrawRpfEditBanner_O1()
        {
            if (!RpfEditMode) return;
            bool searching = rpfSearchAll && rpfSearch.Trim().Length > 0;
            if (searching) return;

            if (Rpf.CurrentIsInGameFolder && !Rpf.CurrentIsInModsFolder)
            {
                var dl = ImGui.GetWindowDrawList();
                var p = ImGui.GetCursorScreenPos();
                float w = ImGui.GetContentRegionAvail().X;
                float h = ImGui.GetTextLineHeightWithSpacing() + 4;
                dl.AddRectFilled(p, new Vector2(p.X + w, p.Y + h),
                                 ImGui.GetColorU32(new Vector4(0.55f, 0.06f, 0.06f, 1f)), 3f);
                ImGui.Dummy(new Vector2(w, h));
                var text = "  Warning: You are directly editing base game files";
                dl.AddText(new Vector2(p.X + 6, p.Y + 3),
                           ImGui.GetColorU32(new Vector4(1f, 0.92f, 0.9f, 1f)), text);
            }
            else if (Rpf.CurrentIsInModsFolder)
            {
                var dl = ImGui.GetWindowDrawList();
                var p = ImGui.GetCursorScreenPos();
                float w = ImGui.GetContentRegionAvail().X;
                float h = ImGui.GetTextLineHeightWithSpacing() + 4;
                dl.AddRectFilled(p, new Vector2(p.X + w, p.Y + h),
                                 ImGui.GetColorU32(new Vector4(0.06f, 0.35f, 0.12f, 1f)), 3f);
                ImGui.Dummy(new Vector2(w, h));
                dl.AddText(new Vector2(p.X + 6, p.Y + 3),
                           ImGui.GetColorU32(new Vector4(0.9f, 1f, 0.92f, 1f)),
                           "  Editing files in the mods folder");
            }
        }

        private void DrawRpfFooter_O1()
        {
            if (RpfEditMode)
            {
                ImGui.TextColored(new Vector4(1.0f, 0.55f, 0.4f, 1f),
                                  "Edit mode: changes are written into the archive immediately. No undo.");
                if (RpfEdit.BackupCount > 0)
                {
                    ImGui.SameLine();
                    ImGui.TextDisabled($"  {RpfEdit.BackupCount:N0} archive(s) backed up to .bak this session.");
                }
            }
            else
            {
                ImGui.TextDisabled("Read-only. Turn on Edit mode to create folders, import files, rename or delete.");
            }
            if (!string.IsNullOrEmpty(RpfStatus))
            {
                ImGui.SameLine();
                ImGui.TextColored(UiTheme.Accent, "   " + RpfStatus);
            }
        }

        private void ApplyRpf_O1(in RpfEdit.Result r) { RpfStatus = r.Message; UiSound.Result(r.Ok); }

        private void ApplyRpfWrite_O1(Func<RpfEdit.Result> op)
        {
            var t = RpfTarget_O1();
            if (RpfEdit.NeedsEncryptionChange(t))
            {
                var enc = RpfEdit.EncryptionOf(t);
                AskRpf_O1("Change RPF encryption type",
                          $"This archive is currently set to {enc} encryption.\n" +
                          "Are you sure you want to change it to OPEN encryption?\n\n" +
                          "Loading by the game will require a mod loader such as OpenRPF.asi or OpenIV.asi.",
                          "Change to OPEN",
                          () =>
                          {
                              if (!RpfEdit.MakeEncryptionValid(RpfTarget_O1()))
                              {
                                  RpfStatus = "could not change the archive's encryption";
                                  return;
                              }
                              RunRpfWrite_O1(op);
                          });
                return;
            }
            RunRpfWrite_O1(op);
        }

        private void RunRpfWrite_O1(Func<RpfEdit.Result> op)
        {
            var archive = RpfTarget_O1().Dir?.File;
            var result = op();
            ApplyRpf_O1(result);
            if (!result.Ok) return;
            if (archive != null) Archive?.ReindexArchive(archive);
            ClearRpfSelection_O1();
            Rpf.Invalidate();
        }

        private void OpenRpfPrompt_O1(RpfPromptKind kind, string initial)
        {
            rpfPrompt = kind;
            rpfPromptText = initial ?? "";
            rpfPromptItem = RpfSelectedItem_O1();
            rpfPromptOpening = true;
        }

        public string PendingRpfConfirm_U4 => rpfConfirmAction != null ? rpfConfirmTitle : null;

        public bool AcceptRpfConfirmForTest_U4()
        {
            var act = rpfConfirmAction;
            if (act == null) return false;
            rpfConfirmAction = null;
            rpfConfirmOpening = false;
            act();
            return true;
        }

        private bool EnsureRpfWritable_U4(string what, Action onReady)
        {
            if (RpfEditMode) return true;
            AskRpf_O1("Edit mode is off",
                      what + " writes into the archive, and only edit mode may do that.\n" +
                      "While in edit mode, all changes are automatically saved.\n" +
                      "There is no undo. Turn it on and carry on?\n\n" +
                      "Close GTA V first. There is no way to tell from here whether the game\n" +
                      "has an archive open, and writing to one it is reading can break it.\n" +
                      "The first change to an archive keeps a copy of it as <name>.rpf.bak.",
                      "Turn on edit mode and continue",
                      () =>
                      {
                          RpfEditMode = true;
                          RpfStatus = "edit mode is ON - changes are written immediately";
                          onReady();
                      });
            return false;
        }

        private string WhyNoWrite_U4(in RpfEdit.Target t, bool searching) =>
            RpfWriteGate_U4.Why(RpfEditMode, t.Valid, searching);

        private void AskRpf_O1(string title, string body, string yes, Action onYes)
        {
            rpfConfirmTitle = title;
            rpfConfirmBody = body;
            rpfConfirmYes = yes;
            rpfConfirmAction = onYes;
            rpfConfirmOpening = true;
        }

        private void AskDeleteRpf_O1()
        {
            if (RpfMultiSelected_U19) { AskDeleteRpfMany_U19(); return; }
            var item = RpfSelectedItem_O1();
            if (!item.Valid) return;
            string extra = RpfEdit.DeleteNeedsConfirm(item, out var what) ? what : item.Name;
            AskRpf_O1("Confirm delete",
                      "Permanently delete " + extra + "?\n" + item.Display + "\n\nThis cannot be undone.",
                      "Delete",
                      () => ApplyRpfWrite_O1(() => RpfEdit.Delete(RpfEditMode, Rpf, item)));
        }

        private void DrawRpfModals_O1()
        {
            if (rpfPromptOpening) { ImGui.OpenPopup("##rpfprompt"); rpfPromptOpening = false; }
            var centre = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(centre, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            if (ImGui.BeginPopupModal("##rpfprompt", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextUnformatted(rpfPrompt switch
                {
                    RpfPromptKind.NewFolder => "Enter a name for the new folder:",
                    RpfPromptKind.NewArchive => "Enter a name for the new RPF7 archive:",
                    RpfPromptKind.Rename => "Enter the new name for this item:",
                    _ => "Name:",
                });
                ImGui.SetNextItemWidth(360);
                if (rpfPrompt != RpfPromptKind.None && ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
                bool enter = ImGui.InputText("##rpfpromptname", ref rpfPromptText, 128,
                                             ImGuiInputTextFlags.EnterReturnsTrue);
                bool ok = RpfEdit.IsFilenameOk(rpfPromptText);
                if (!ok) ImGui.TextColored(new Vector4(1f, 0.5f, 0.4f, 1f),
                                           "A name cannot be empty or contain \\ / : * ? \" < > |");
                else ImGui.TextDisabled(" ");

                ImGui.BeginDisabled(!ok);
                if (ImGui.Button("OK", new Vector2(120, 0)) || (enter && ok))
                {
                    CommitRpfPrompt_O1();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndDisabled();
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    rpfPrompt = RpfPromptKind.None;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            if (rpfConfirmOpening) { ImGui.OpenPopup("##rpfconfirm"); rpfConfirmOpening = false; }
            ImGui.SetNextWindowPos(centre, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            if (ImGui.BeginPopupModal("##rpfconfirm", ImGuiWindowFlags.AlwaysAutoResize))
            {
                ImGui.TextColored(new Vector4(1f, 0.65f, 0.45f, 1f), rpfConfirmTitle);
                ImGui.Separator();
                ImGui.TextUnformatted(rpfConfirmBody);
                ImGui.Spacing();
                if (ImGui.Button(rpfConfirmYes, new Vector2(160, 0)))
                {
                    var act = rpfConfirmAction;
                    rpfConfirmAction = null;
                    ImGui.CloseCurrentPopup();
                    act?.Invoke();
                }
                ImGui.SameLine();
                if (ImGui.Button("Cancel", new Vector2(120, 0)))
                {
                    rpfConfirmAction = null;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            DrawRpfDefragModal_O1();
        }

        private void CommitRpfPrompt_O1()
        {
            var name = (rpfPromptText ?? "").Trim();
            var kind = rpfPrompt;
            rpfPrompt = RpfPromptKind.None;
            switch (kind)
            {
                case RpfPromptKind.NewFolder:
                    ApplyRpfWrite_O1(() => RpfEdit.NewFolder(RpfEditMode, Rpf, RpfTarget_O1(), name));
                    break;
                case RpfPromptKind.NewArchive:
                    ApplyRpfWrite_O1(() =>
                    {
                        var r = RpfEdit.NewArchive(RpfEditMode, Rpf, RpfTarget_O1(), name);
                        return r;
                    });
                    break;
                case RpfPromptKind.Rename:
                    var item = rpfPromptItem;
                    ApplyRpfWrite_O1(() => RpfEdit.Rename(RpfEditMode, Rpf, item, name));
                    break;
            }
        }

        private void DrawRpfDefragModal_O1()
        {
            if (!rpfDefragOpen) return;
            if (rpfDefragOpening) { ImGui.OpenPopup("##rpfdefrag"); rpfDefragOpening = false; }
            var centre = ImGui.GetMainViewport().GetCenter();
            ImGui.SetNextWindowPos(centre, ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
            if (ImGui.BeginPopupModal("##rpfdefrag", ImGuiWindowFlags.AlwaysAutoResize))
            {
                var rpf = rpfDefragTarget;
                ImGui.TextUnformatted("Defragment RPF archive");
                ImGui.Separator();
                ImGui.TextDisabled(rpf?.Path ?? "");
                long cur = rpf?.FileSize ?? 0;
                long want = RpfEdit.DefragmentedSize(rpf, rpfDefragRecursive);
                ImGui.Text("Current size:      " + RpfExplorer.SizeText(cur));
                ImGui.Text("Defragmented size: " + RpfExplorer.SizeText(want));
                ImGui.Text("Size reduction:    " + RpfExplorer.SizeText(Math.Max(0, cur - want)));
                ImGui.Checkbox("Recursive", ref rpfDefragRecursive);
                ImGui.Spacing();
                ImGui.BeginDisabled(rpf == null || !RpfEditMode);
                if (ImGui.Button("Begin defragment", new Vector2(180, 0)))
                {
                    var res = RpfEdit.Defragment(RpfEditMode, rpf, rpfDefragRecursive);
                    ApplyRpf_O1(res);
                    if (res.Ok && rpf != null) Archive?.ReindexArchive(rpf);
                    Rpf.RefreshCurrent_O1();
                    rpfDefragOpen = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndDisabled();
                ImGui.SameLine();
                if (ImGui.Button("Close", new Vector2(120, 0)))
                {
                    rpfDefragOpen = false;
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        private void DrawRpfRowEditMenu_O1(in RpfExplorer.Row r)
        {
            var item = RpfEdit.ItemOf(r);
            ImGui.Separator();
            if (!RpfEditMode)
            {
                if (ImGui.MenuItem("Turn on edit mode..."))
                    EnsureRpfWritable_U4("Editing an archive", () => { });
                return;
            }
            RpfCreateMenuItems_V46();
            ImGui.Separator();
            if (ImGui.MenuItem("Rename...")) OpenRpfPrompt_O1(RpfPromptKind.Rename, r.Name);
            if (ImGui.MenuItem("Delete...")) AskDeleteRpf_O1();
            if (ImGui.MenuItem("Copy")) ApplyRpf_O1(RpfEdit.Copy(SelectedRpfRows_O1()));
            if (item.Archive != null && ImGui.MenuItem("Defragment..."))
            {
                rpfDefragTarget = item.Archive;
                rpfDefragOpen = rpfDefragOpening = true;
            }
        }

        public bool OpenRpfDialog_O1(string which)
        {
            switch ((which ?? "").Trim().ToLowerInvariant())
            {
                case "newfolder": OpenRpfPrompt_O1(RpfPromptKind.NewFolder, "folder"); return true;
                case "newrpf": OpenRpfPrompt_O1(RpfPromptKind.NewArchive, "new.rpf"); return true;
                case "rename":
                    if (!rpfHasSelRow) SelectFirstRpfRow_O1();
                    OpenRpfPrompt_O1(RpfPromptKind.Rename, rpfHasSelRow ? rpfSelRow.Name : "name");
                    return true;
                case "delete":
                    if (!rpfHasSelRow) SelectFirstRpfRow_O1();
                    AskDeleteRpf_O1();
                    return rpfConfirmOpening;
                case "editmode":
                    RpfEditMode = false;
                    AskRpf_O1("Warning - Entering edit mode",
                              "While in edit mode, all changes are automatically saved.\n" +
                              "There is no undo. Do you want to continue?\n\n" +
                              "Close GTA V first. There is no way to tell from here whether the game\n" +
                              "has an archive open, and writing to one it is reading can break it.\n" +
                              "The first change to an archive keeps a copy of it as <name>.rpf.bak.",
                              "Enter edit mode", () => { RpfEditMode = true; });
                    return true;
                case "defrag":
                    if (!rpfHasSelRow) SelectFirstRpfRow_O1();
                    rpfDefragTarget = SelectedArchive_O1();
                    if (rpfDefragTarget == null) return false;
                    rpfDefragOpen = rpfDefragOpening = true;
                    return true;
                default: return false;
            }
        }

        private void SelectFirstRpfRow_O1()
        {
            Rpf.EnsureList();
            if (Rpf.Rows.Count > 0) SelectRpfRow_O1(Rpf.Rows[0]);
        }

        private void DrawRpfFsSelected_O1()
        {
            var r = rpfSelRow;
            ImGui.TextWrapped(r.Name ?? "");
            ImGui.TextDisabled(r.Type ?? "");
            if (!r.IsFolder) ImGui.TextDisabled(RpfExplorer.SizeText(r.Size) + " on disk");
            else if (!string.IsNullOrEmpty(r.Attr)) ImGui.TextDisabled(r.Attr);

            ImGui.Spacing();
            ImGui.TextDisabled("Where it lives");
            ImGui.TextWrapped(r.Path ?? "");
            if (ImGui.Button("Copy path", new Vector2(-1, 0))) ImGui.SetClipboardText(r.Path ?? "");

            ImGui.Spacing();
            ImGui.Separator();
            if (r.IsFolder)
            {
                if (ImGui.Button("Open", new Vector2(-1, 0))) ActivateRpfRow_N4(r);
            }
            else
            {
                if (ImGui.Button("Open", new Vector2(-1, 0))) RequestRpfOpenDiskFile = r.Path;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Shows it: a model, texture dictionary or collision in the viewer\n" +
                                     "window, a ytyp / ymap / meta file as XML text, anything else as text.\n" +
                                     "Nothing is loaded into the light editor or the MLO by opening it.");
                if (MainForm.IsConvertibleXml_V52(r.Name))
                {
                    if (ImGui.Button("Convert to " + System.IO.Path.GetExtension(MainForm.ConvertTargetName_V52(r.Name)),
                                     new Vector2(-1, 0)))
                        RequestRpfDiskConvert_V52 = r.Path;
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Write the real game file beside this XML - the reverse of Export XML.\n" +
                                         "The same conversion CodeWalker's import runs.");
                }
                float halfX = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) * 0.5f;
                if (ImGui.Button("Export XML...", new Vector2(halfX, 0))) RequestRpfDiskExportXml_V42 = r.Path;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Convert it to XML and write it to a file, so another tool can read it - or you can edit it and convert it back.");
                ImGui.SameLine();
                if (ImGui.Button("View hex", new Vector2(halfX, 0))) RequestRpfDiskHex_V42 = r.Path;
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("The raw bytes, with their offsets and characters.");
                DrawRpfFsMetaActions_Q1(r.Path);
            }
            if (ImGui.Button("Show this file in Explorer", new Vector2(-1, 0))) RequestRpfShowInExplorer = r.Path;

            ImGui.Spacing();
            ImGui.Separator();
            DrawRpfFolderActions_N4();
        }

        private void DrawRpfTreeButtons_O1()
        {
            if (ImGui.Button("Open folder...", new Vector2(-1, 0))) RequestRpfOpenFolder = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Show another folder in the tree beside the GTA V install -\n" +
                                 "a mod's folder, a FiveM resource, a working copy. Its .rpf\n" +
                                 "archives open the same way the game's do.");
            var cur = Rpf.Current;
            var root = cur;
            while (root?.Parent != null) root = root.Parent;
            bool closeable = root != null && root.IsRoot && !ReferenceEquals(root, Rpf.GameRoot);
            ImGui.BeginDisabled(!closeable);
            if (ImGui.Button("Close this folder", new Vector2(-1, 0)) && closeable)
            {
                var label = root.Label;
                Rpf.CloseRootFolder(root);
                ClearRpfSelection_O1();
                RpfStatus = "closed " + label;
            }
            ImGui.EndDisabled();
        }
    }
}

