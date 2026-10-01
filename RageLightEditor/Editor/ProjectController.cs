using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CodeWalker.GameFiles;
using SharpDX;

namespace RageLightEditor.Editor
{
    public partial class ProjectController
    {
        private readonly ProjectWindow win;
        private readonly Func<IWin32Window> owner;
        private readonly Func<GameFileCache> cache;

        public event Action ProjectYmapsChanged;
        public event Action<CwProject> ProjectSaved;
        public event Action<CwProject> ProjectClosing;
        public event Action<YmapEntityDef> EntityChanged;
        public event Action<YmapEntityDef> GoToEntity;
        public Func<Vector3> SpawnPos = () => Vector3.Zero;
        public Func<YmapEntityDef, bool> NewEntityInInterior;
        public event Action PropsPanelRequested;
        public event Action EntitySetsChanged;
        public event Action<Vector3, float> GoToPosition;
        public event Action UndoRequested, RedoRequested, CutRequested, CopyRequested, PasteRequested, CloneRequested, DeleteRequested;
        public event Action<int> ThemeRequested;

        public const string AllTypesFilter =
            "All supported|*.ymap;*.ytyp;*.ybn;*.ydr;*.ydd;*.yft;*.ytd;*.ynd;*.ynv;*.dat;*.ymt;*.rel|" +
            "Ymap files|*.ymap|Ytyp files|*.ytyp|Ybn files|*.ybn|Ydr files|*.ydr|Ydd files|*.ydd|" +
            "Yft files|*.yft|Ytd files|*.ytd|Ynd files|*.ynd|Ynv files|*.ynv|Dat files|*.dat|" +
            "Ymt files|*.ymt|Rel files|*.rel|All files|*.*";

        public CwProject Project => win.Project;

        public ProjectController(ProjectWindow window, Func<IWin32Window> owner, Func<GameFileCache> cache)
        {
            win = window;
            this.owner = owner;
            this.cache = cache;
        }

        public void Tick()
        {
            if (win.RequestNewProject) { win.RequestNewProject = false; NewProject(); }
            if (win.RequestOpenProject) { win.RequestOpenProject = false; OpenProject(); }
            if (win.RequestSaveProject) { win.RequestSaveProject = false; SaveProject(false); }
            if (win.RequestSaveProjectAs) { win.RequestSaveProjectAs = false; SaveProject(true); }
            if (win.RequestCloseProject) { win.RequestCloseProject = false; CloseProject(); }
            if (win.RequestSaveAll) { win.RequestSaveAll = false; SaveAll(); }

            if (win.RequestNewYmap) { win.RequestNewYmap = false; NewYmap(); }
            if (win.RequestSaveYmap) { win.RequestSaveYmap = false; SaveYmap(false); }
            if (win.RequestSaveYmapAs) { win.RequestSaveYmapAs = false; SaveYmap(true); }
            if (win.RequestRemoveYmap) { win.RequestRemoveYmap = false; RemoveYmap(); }
            if (win.RequestCalcAllExtents) { win.RequestCalcAllExtents = false; CalcAll(extents: true); }
            if (win.RequestCalcAllFlags) { win.RequestCalcAllFlags = false; CalcAll(extents: false); }

            if (win.RequestNewYtyp) { win.RequestNewYtyp = false; NewYtyp(); }
            if (win.RequestSaveYtyp) { win.RequestSaveYtyp = false; SaveYtyp(false); }
            if (win.RequestSaveYtypAs) { win.RequestSaveYtypAs = false; SaveYtyp(true); }
            if (win.RequestRemoveYtyp) { win.RequestRemoveYtyp = false; RemoveYtyp(); }

            if (win.RequestNewEntity) { win.RequestNewEntity = false; NewEntity(); }
            if (win.RequestDeleteEntity) { win.RequestDeleteEntity = false; DeleteEntity(); }
            if (win.RequestGoToEntity) { win.RequestGoToEntity = false; if (win.CurrentEntity != null) GoToEntity?.Invoke(win.CurrentEntity); }
            if (win.RequestAddSelectedToProject) { win.RequestAddSelectedToProject = false; AddEntityToProject(); }

            if (win.RequestNewArchetype) { win.RequestNewArchetype = false; NewArchetype(); }
            if (win.RequestDeleteArchetype) { win.RequestDeleteArchetype = false; DeleteArchetype(); }
            if (win.RequestArchetypesFromYdrs) { win.RequestArchetypesFromYdrs = false; ArchetypesFromYdrs(); }

            if (win.RequestImportYmapXml) { win.RequestImportYmapXml = false; ImportYmapXml(); }
            if (win.RequestImportMenyoo) { win.RequestImportMenyoo = false; ImportMenyooXml(); }
            if (win.RequestOpenAny) { win.RequestOpenAny = false; OpenFiles(AllTypesFilter); }
            if (win.RequestManifestGenerator) { win.RequestManifestGenerator = false; GenerateManifest(); }
            if (win.RequestSaveManifest) { win.RequestSaveManifest = false; SaveManifestText(); }
            if (win.RequestPropsPanel) { win.RequestPropsPanel = false; PropsPanelRequested?.Invoke(); }
            if (win.RequestNewEntitySet) { win.RequestNewEntitySet = false; NewEntitySet(); }
            if (win.RequestDeleteEntitySet) { win.RequestDeleteEntitySet = false; DeleteEntitySet(); }
            if (win.RequestNewMloEntity) { win.RequestNewMloEntity = false; NewMloEntity(); }
            if (win.EntitySetVisibilityChanged) { win.EntitySetVisibilityChanged = false; EntitySetsChanged?.Invoke(); }

            if (win.RequestOpenFolder) { win.RequestOpenFolder = false; OpenFolder(); }
            if (win.RequestSaveItem) { win.RequestSaveItem = false; SaveItem(false); }
            if (win.RequestSaveItemAs) { win.RequestSaveItemAs = false; SaveItem(true); }
            if (win.RequestGoToSelected) { win.RequestGoToSelected = false; GoToSelected(); }
            if (win.RequestUndo) { win.RequestUndo = false; UndoRequested?.Invoke(); }
            if (win.RequestRedo) { win.RequestRedo = false; RedoRequested?.Invoke(); }
            if (win.RequestCut) { win.RequestCut = false; if (win.CurrentEntity != null) CutRequested?.Invoke(); }
            if (win.RequestCopy) { win.RequestCopy = false; if (win.CurrentEntity != null) CopyRequested?.Invoke(); }
            if (win.RequestPaste) { win.RequestPaste = false; PasteRequested?.Invoke(); }
            if (win.RequestClone) { win.RequestClone = false; if (win.CurrentEntity != null) CloneRequested?.Invoke(); }
            if (win.RequestDeleteItem)
            {
                win.RequestDeleteItem = false;
                if (win.CurrentEntity != null) DeleteRequested?.Invoke();
                else if (win.CurrentArchetype != null) DeleteArchetype();
            }
            if (win.RequestTheme >= 0) { int t = win.RequestTheme; win.RequestTheme = -1; ThemeRequested?.Invoke(t); }
            TickScenario_I2();

            if (win.EntityChangedInPage != null)
            {
                EntityChanged?.Invoke(win.EntityChangedInPage);
                win.EntityChangedInPage = null;
            }
        }

        private void LetGoOfCurrent()
        {
            var closing = win.Project;
            if (closing == null) return;
            ProjectClosing?.Invoke(closing);
            win.Project = null;
            win.Select(null);
            win.ClearPage();
        }

        public void NewProject()
        {
            if (!ConfirmDiscard()) return;
            LetGoOfCurrent();
            win.Project = new CwProject { Name = "New Project", HasChanged = true };
            win.Select(win.Project);
            win.Visible = true;
            win.Status = "new project";
            ProjectYmapsChanged?.Invoke();
        }

        public void OpenProject()
        {
            if (!ConfirmDiscard()) return;
            using var dlg = new OpenFileDialog { Filter = "Map projects (*.cwproj)|*.cwproj|All files|*.*" };
            if (dlg.ShowDialog(owner()) != DialogResult.OK) return;
            OpenProject(dlg.FileName);
        }

        public void OpenProject(string path)
        {
            try
            {
                var p = CwProject.Load(path);
                var problems = p.LoadFiles(cache());
                LoadProjectScenarios(p, problems);
                LetGoOfCurrent();
                win.Project = p;
                win.Select(p);
                win.Visible = true;
                win.Status = problems.Count == 0
                    ? $"opened {p.Name}: {p.YmapFiles.Count} ymap(s), {p.YtypFiles.Count} ytyp(s)"
                    : $"opened with {problems.Count} problem(s): {problems[0]}";
                ProjectYmapsChanged?.Invoke();
            }
            catch (Exception ex) { win.Status = "could not open project: " + ex.Message; }
        }

        public void SaveProject(bool saveas)
        {
            var p = win.Project;
            if (p == null) return;
            var path = p.Filepath;
            if (saveas || string.IsNullOrEmpty(path))
            {
                using var dlg = new SaveFileDialog
                {
                    Filter = "Map projects (*.cwproj)|*.cwproj",
                    FileName = (p.Name ?? "project") + ".cwproj",
                };
                if (dlg.ShowDialog(owner()) != DialogResult.OK) return;
                path = dlg.FileName;
                var oldDir = p.Directory;
                p.Filepath = path;
                p.Name = Path.GetFileNameWithoutExtension(path);
                RebaseRelativePaths(p, oldDir);
            }
            try { p.Save(path); win.Status = "saved " + Path.GetFileName(path); ProjectSaved?.Invoke(p); }
            catch (Exception ex) { win.Status = "project save failed: " + ex.Message; }
        }

        private static void RebaseRelativePaths(CwProject p, string oldDir)
        {
            for (int i = 0; i < p.YmapFilenames.Count; i++)
                if (!Path.IsPathRooted(p.YmapFilenames[i]) && !string.IsNullOrEmpty(oldDir))
                    p.YmapFilenames[i] = p.GetRelativePath(Path.Combine(oldDir, p.YmapFilenames[i]));
            for (int i = 0; i < p.YtypFilenames.Count; i++)
                if (!Path.IsPathRooted(p.YtypFilenames[i]) && !string.IsNullOrEmpty(oldDir))
                    p.YtypFilenames[i] = p.GetRelativePath(Path.Combine(oldDir, p.YtypFilenames[i]));
        }

        public void CloseProject()
        {
            if (!ConfirmDiscard()) { win.CloseViaWindow_V22 = false; return; }
            var closing = win.Project;
            if (win.CloseViaWindow_V22) { win.Visible = false; win.CloseViaWindow_V22 = false; }
            if (closing != null) ProjectClosing?.Invoke(closing);
            win.Project = null;
            win.Select(null);
            win.ClearPage();
            win.Status = closing != null ? $"closed {closing.Name}" : "project closed";
            ProjectYmapsChanged?.Invoke();
        }

        public void SaveAll()
        {
            var p = win.Project;
            if (p == null) return;
            foreach (var y in p.YmapFiles.ToList()) if (y.HasChanged) SaveYmap(y, false);
            foreach (var t in p.YtypFiles.ToList()) if (t.HasChanged) SaveYtyp(t, false);
            SaveSpaceFiles();
            SaveProject(false);
        }

        private bool ConfirmDiscard()
        {
            var p = win.Project;
            if (p == null || !p.AnyUnsaved) return true;
            var r = MessageBox.Show(owner(), "The project has unsaved changes. Save before closing?",
                                    "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (r == DialogResult.Cancel) return false;
            if (r == DialogResult.Yes) SaveAll();
            return true;
        }

        public void NewYmap()
        {
            if (win.Project == null) NewProject();
            if (win.Project == null) return;
            var y = win.Project.NewYmap();
            if (y == null) return;
            win.Select(y);
            win.Status = "created " + y.Name;
            ProjectYmapsChanged?.Invoke();
        }

        public void OpenFiles(string filter)
        {
            if (win.Project == null) NewProject();
            if (win.Project == null) return;
            using var dlg = new OpenFileDialog { Filter = filter, Multiselect = true };
            if (dlg.ShowDialog(owner()) != DialogResult.OK) return;
            AddFilesToProject(dlg.FileNames);
        }

        public void OpenFolder()
        {
            if (win.Project == null) NewProject();
            if (win.Project == null) return;
            using var dlg = new FolderBrowserDialog
            {
                Description = "Add every supported file under a folder to the project",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false,
            };
            if (dlg.ShowDialog(owner()) != DialogResult.OK) return;
            OpenFolder(dlg.SelectedPath, confirmLarge: true);
        }

        public int OpenFolder(string folder, bool confirmLarge)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) { win.Status = "folder not found"; return 0; }
            string[] files;
            try { files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories); }
            catch (Exception ex) { win.Status = "could not read the folder: " + ex.Message; return 0; }
            if (confirmLarge && files.Length > 100 &&
                MessageBox.Show(owner(), "This folder contains many files, loading may take a long time!\nAre you sure you want to continue?",
                                "Large folder warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return 0;
            return AddFilesToProject(files, quiet: true);
        }

        public void AddFilesToProject(IList<string> files) => AddFilesToProject(files, quiet: false);

        private static List<string> ListFor(CwProject p, string file, string ext)
        {
            var name = Path.GetFileName(file);
            switch (ext)
            {
                case ".ybn": return p.YbnFilenames;
                case ".ynd": return p.YndFilenames;
                case ".ynv": return p.YnvFilenames;
                case ".ydr": return p.YdrFilenames;
                case ".ydd": return p.YddFilenames;
                case ".yft": return p.YftFilenames;
                case ".ytd": return p.YtdFilenames;
                case ".dat": return name.StartsWith("trains", StringComparison.OrdinalIgnoreCase) ? p.TrainsFilenames : null;
                case ".rel": return name.EndsWith(".dat151.rel", StringComparison.OrdinalIgnoreCase) ? p.AudioRelFilenames : null;
                case ".ymt":
                    try
                    {
                        var ymt = new YmtFile();
                        ymt.Load(File.ReadAllBytes(file));
                        return ymt.ContentType == YmtFileContentType.ScenarioPointRegion ? p.ScenarioFilenames : null;
                    }
                    catch { return null; }
                default: return null;
            }
        }

        public int AddFilesToProject(IList<string> files, bool quiet)
        {
            if (files == null || files.Count == 0) return 0;
            if (win.Project == null) NewProject();
            if (win.Project == null) return 0;
            var p = win.Project;
            var errors = new List<string>();
            int added = 0;
            object first = null;
            var ordered = files.OrderBy(f =>
                string.Equals(Path.GetExtension(f), ".ytyp", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ToList();
            foreach (var file in ordered)
            {
                try
                {
                    if (!File.Exists(file)) continue;
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext == ".ymap")
                    {
                        var y = p.AddYmapFile(file);
                        if (y == null) { errors.Add(Path.GetFileName(file) + " is already in the project"); continue; }
                        y.Load(File.ReadAllBytes(file));
                        y.FilePath = file;
                        y.RpfFileEntry ??= new RpfResourceFileEntry();
                        y.RpfFileEntry.Name = Path.GetFileName(file);
                        y.Name = y.RpfFileEntry.Name;
                        p.InitYmapArchetypes(y, cache());
                        y.Loaded = true;
                        first ??= y;
                        added++;
                    }
                    else if (ext == ".ytyp")
                    {
                        var t = p.AddYtypFile(file);
                        if (t == null) { errors.Add(Path.GetFileName(file) + " is already in the project"); continue; }
                        t.Load(File.ReadAllBytes(file));
                        t.FilePath = file;
                        t.RpfFileEntry ??= new RpfResourceFileEntry();
                        t.RpfFileEntry.Name = Path.GetFileName(file);
                        t.Name = t.RpfFileEntry.Name;
                        t.Loaded = true;
                        first ??= t;
                        added++;
                    }
                    else
                    {
                        var list = ListFor(p, file, ext);
                        if (list == null) { if (!quiet) errors.Add(Path.GetFileName(file) + ": not a project file type"); continue; }
                        var rel = p.GetRelativePath(file);
                        if (!list.Contains(rel)) { list.Add(rel); p.HasChanged = true; added++; }
                        if (ext == ".ymt") LoadScenarioIntoProject(file);
                        if (ext == ".ybn" && p.FindYbn(JenkHash.GenHash(Path.GetFileNameWithoutExtension(file).ToLowerInvariant())) == null)
                        {
                            p.LoadYbn(file, out var ybnProblem);
                            if (ybnProblem != null && !quiet) errors.Add(ybnProblem);
                        }
                    }
                }
                catch (Exception ex) { errors.Add(Path.GetFileName(file) + ": " + ex.Message); }
            }
            if (first != null) win.Select(first);
            win.Status = errors.Count == 0 ? $"opened {added} file(s)" : $"opened {added} file(s); {errors[0]}";
            if (added > 0) ReconnectProjectArchetypes_V67(ordered);
            ProjectYmapsChanged?.Invoke();
            return added;
        }

        public void SaveItem(bool saveas)
        {
            if (win.CurrentYmap != null) SaveYmap(win.CurrentYmap, saveas);
            else if (win.CurrentYtyp != null) SaveYtyp(win.CurrentYtyp, saveas);
            else if (win.CurrentScenario != null) SaveScenario(win.CurrentScenario, saveas);
            else SaveProject(saveas);
        }

        public void GoToSelected()
        {
            if (win.CurrentEntity != null) { GoToEntity?.Invoke(win.CurrentEntity); return; }
            if (win.CurrentScenario != null) { GoToScenario(win.CurrentScenario, win.WorldScenarioSelection); return; }
            if (win.CurrentRoom != null || win.CurrentPortal != null || win.CurrentArchetype is MloArchetype)
            {
                var mlo = win.CurrentRoom?.OwnerMlo ?? win.CurrentPortal?.OwnerMlo ?? win.CurrentArchetype as MloArchetype;
                var inst = win.FindMloInstance?.Invoke(mlo);
                var o = inst?.Owner;
                if (o != null)
                {
                    if (win.CurrentRoom != null)
                    {
                        var mn = MloEditor.GetRoomBBMin(win.CurrentRoom);
                        var mx = MloEditor.GetRoomBBMax(win.CurrentRoom);
                        var c = (mn + mx) * 0.5f;
                        var p = o.Position + Vector3.Transform(c, o.Orientation);
                        GoToPosition?.Invoke(p, Math.Max((mx - mn).Length() * 0.5f, 2.0f));
                    }
                    else if (win.CurrentPortal != null)
                    {
                        var c = MloEditor.GetPortalCenter(win.CurrentPortal);
                        GoToPosition?.Invoke(o.Position + Vector3.Transform(c, o.Orientation), 4.0f);
                    }
                    else GoToEntity?.Invoke(o);
                    return;
                }
                if (mlo != null && win.CurrentRoom == null && win.CurrentPortal == null)
                {
                    win.Status = "interior is not loaded in the world - fly near it first";
                    return;
                }
                win.Status = "interior is not loaded in the world - fly near it first";
                return;
            }
            if (win.CurrentYmap != null)
            {
                var y = win.CurrentYmap;
                var mn = y._CMapData.entitiesExtentsMin;
                var mx = y._CMapData.entitiesExtentsMax;
                if ((mx - mn).LengthSquared() < 1e-3f && y.CalcExtents())
                {
                    mn = y._CMapData.entitiesExtentsMin;
                    mx = y._CMapData.entitiesExtentsMax;
                }
                if ((mx - mn).LengthSquared() < 1e-3f && (y.AllEntities == null || y.AllEntities.Length == 0))
                { win.Status = "the ymap has no entities to go to"; return; }
                GoToPosition?.Invoke((mn + mx) * 0.5f, Math.Max((mx - mn).Length() * 0.5f, 5.0f));
            }
        }

        public void SaveYmap(bool saveas) => SaveYmap(win.CurrentYmap, saveas);

        public void SaveYmap(YmapFile y, bool saveas)
        {
            if (y == null) return;
            var p = win.Project;
            string filepath = y.FilePath;
            if (string.IsNullOrEmpty(filepath)) filepath = y.Name;
            string origfile = filepath;
            if (!File.Exists(filepath)) saveas = true;

            if (win.AutoCalcFlags) y.CalcFlags();
            if (win.AutoCalcExtents) y.CalcExtents();

            if (saveas)
            {
                using var dlg = new SaveFileDialog { Filter = "Ymap files|*.ymap", FileName = Path.GetFileName(filepath) };
                if (dlg.ShowDialog(owner()) != DialogResult.OK) return;
                filepath = dlg.FileName;
            }
            try
            {
                y.SetFilePath(filepath);
                var data = y.Save();
                if (data == null || data.Length == 0) { win.Status = "save produced no data"; return; }
                File.WriteAllBytes(filepath, data);
                UiSound.Success();
                y.HasChanged = false;
                if (saveas && p != null)
                {
                    var origRel = p.GetRelativePath(origfile);
                    var newRel = p.GetRelativePath(y.FilePath);
                    if (!p.RenameYmap(origRel, newRel)) { p.YmapFilenames.Add(newRel); }
                    p.HasChanged = true;
                }
                win.Status = "saved " + Path.GetFileName(filepath) +
                             (y.SaveWarnings != null && y.SaveWarnings.Count > 0 ? $"  ({y.SaveWarnings.Count} warning(s): {y.SaveWarnings[0]})" : "");
                ProjectYmapsChanged?.Invoke();
            }
            catch (Exception ex) { win.Status = "ymap save failed: " + ex.Message; }
        }

        public void RemoveYmap()
        {
            var y = win.CurrentYmap;
            if (y == null || win.Project == null) return;
            if (y.HasChanged && MessageBox.Show(owner(), $"{y.Name} has unsaved changes. Remove it from the project anyway?",
                    "Unsaved ymap", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            win.Project.RemoveYmapFile(y);
            win.Select(win.Project);
            win.Status = "removed " + y.Name;
            ProjectYmapsChanged?.Invoke();
        }

        private void CalcAll(bool extents)
        {
            var p = win.Project;
            if (p == null) return;
            int n = 0;
            foreach (var y in p.YmapFiles)
            {
                bool ch = extents ? y.CalcExtents() : y.CalcFlags();
                if (ch) { y.HasChanged = true; n++; }
            }
            win.Status = $"{(extents ? "extents" : "flags")} recalculated on {n} ymap(s)";
        }

        public void NewYtyp()
        {
            if (win.Project == null) NewProject();
            if (win.Project == null) return;
            var t = win.Project.NewYtyp();
            if (t == null) return;
            win.Select(t);
            win.Status = "created " + t.Name;
        }

        public void SaveYtyp(bool saveas) => SaveYtyp(win.CurrentYtyp, saveas);

        public void SaveYtyp(YtypFile t, bool saveas)
        {
            if (t == null) return;
            var p = win.Project;
            string filepath = t.FilePath;
            if (string.IsNullOrEmpty(filepath)) filepath = t.Name;
            string origfile = filepath;
            if (!File.Exists(filepath)) saveas = true;

            foreach (var a in t.AllArchetypes ?? Array.Empty<Archetype>())
                if (a is MloArchetype m)
                {
                    var v = MloEditor.Validate(m);
                    if (!string.IsNullOrEmpty(v))
                    {
                        AppLog_U21.Error($"{Path.GetFileName(filepath)} was not saved - interior {a.Name} has problems:\n{v}");
                        win.Status = $"{a.Name}: {v.Split('\n')[0]} - the full list is in Help > Show log";
                        UiSound.Error();
                        return;
                    }
                }

            if (saveas)
            {
                using var dlg = new SaveFileDialog { Filter = "Ytyp files|*.ytyp", FileName = Path.GetFileName(filepath) };
                if (dlg.ShowDialog(owner()) != DialogResult.OK) return;
                filepath = dlg.FileName;
            }
            try
            {
                t.FilePath = filepath;
                t.RpfFileEntry ??= new RpfResourceFileEntry();
                t.RpfFileEntry.Name = Path.GetFileName(filepath);
                t.Name = t.RpfFileEntry.Name;
                var data = t.Save();
                if (data == null || data.Length == 0) { win.Status = "save produced no data"; AppLog_U21.Error(Path.GetFileName(filepath) + ": the save produced no data"); return; }
                File.WriteAllBytes(filepath, data);
                UiSound.Success();
                t.HasChanged = false;
                if (t.SaveWarnings != null && t.SaveWarnings.Count > 0)
                    AppLog_U21.Warn(Path.GetFileName(filepath) + " saved with warnings:\n" + string.Join("\n", t.SaveWarnings));
                if (saveas && p != null)
                {
                    var origRel = p.GetRelativePath(origfile);
                    var newRel = p.GetRelativePath(t.FilePath);
                    if (!p.RenameYtyp(origRel, newRel)) p.YtypFilenames.Add(newRel);
                    p.HasChanged = true;
                }
                win.Status = "saved " + Path.GetFileName(filepath) +
                             (t.SaveWarnings != null && t.SaveWarnings.Count > 0 ? $"  ({t.SaveWarnings.Count} warning(s))" : "");
            }
            catch (Exception ex) { win.Status = "ytyp save failed: " + ex.Message + " - details in Help > Show log"; AppLog_U21.Error("saving " + Path.GetFileName(filepath), ex); UiSound.Error(); }
        }

        public void RemoveYtyp()
        {
            var t = win.CurrentYtyp;
            if (t == null || win.Project == null) return;
            if (t.HasChanged && MessageBox.Show(owner(), $"{t.Name} has unsaved changes. Remove it from the project anyway?",
                    "Unsaved ytyp", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            win.Project.RemoveYtypFile(t);
            win.Select(win.Project);
            win.Status = "removed " + t.Name;
        }

        public void NewEntity()
        {
            if (NewEntityInInterior?.Invoke(win.CurrentEntity) == true) return;
            var y = win.CurrentYmap;
            if (y == null) { win.Status = "select a ymap first"; return; }
            var e = win.Project.NewEntity(y, SpawnPos(), cache(), win.CurrentEntity, copyPosition: false);
            if (e == null) return;
            win.Select(e);
            EntityChanged?.Invoke(e);
            win.Status = "new entity in " + y.Name;
        }

        public void DeleteEntity()
        {
            var e = win.CurrentEntity;
            var y = win.CurrentYmap;
            if (e == null) return;
            if (e.Ymap == null)
            {
                var mlo = e.MloParent?.Archetype as MloArchetype;
                var inst = e.MloParent?.MloInstance;
                if (mlo == null || inst == null || mlo.entities == null) return;
                if (e._CEntityDef.numChildren != 0)
                {
                    MessageBox.Show(owner(), "This entity's numChildren is not 0 - deleting entities with children is not currently supported.");
                    return;
                }
                mlo.RemoveEntity(e);
                inst.DeleteEntity(e);
                if (mlo.Ytyp != null) mlo.Ytyp.HasChanged = true;
                EntityChanged?.Invoke(e);
                win.Select(mlo);
                win.Status = "deleted interior entity";
                return;
            }
            if (e.Ymap != y || y.AllEntities == null) return;
            if (e._CEntityDef.numChildren != 0)
            {
                MessageBox.Show(owner(), "This entity's numChildren is not 0 - deleting entities with children is not currently supported.");
                return;
            }
            for (int i = e.Index + 1; i < y.AllEntities.Length; i++)
                if (y.AllEntities[i]._CEntityDef.numChildren != 0)
                {
                    MessageBox.Show(owner(), "There are other entities present in this .ymap that have children. Deleting this entity is not currently supported.");
                    return;
                }
            if (y.RemoveEntity(e))
            {
                y.HasChanged = true;
                EntityChanged?.Invoke(e);
                win.Select(y);
                win.Status = "deleted entity";
                ProjectYmapsChanged?.Invoke();
            }
        }

        public void AddEntityToProject()
        {
            var e = win.CurrentEntity;
            if (e == null) return;
            if (win.Project == null) NewProject();
            var p = win.Project;
            if (e.Ymap == null)
            {
                var t = e.MloParent?.Archetype?.Ytyp;
                if (t != null && !p.ContainsYtyp(t)) { p.AddYtypFile(t); t.HasChanged = true; win.Status = "added " + t.Name; }
                return;
            }
            if (!p.ContainsYmap(e.Ymap))
            {
                if (string.IsNullOrEmpty(e.Ymap.FilePath)) e.Ymap.FilePath = e.Ymap.Name;
                p.AddYmapFile(e.Ymap);
                e.Ymap.HasChanged = true;
                win.Status = "added " + e.Ymap.Name + " to the project";
                ProjectYmapsChanged?.Invoke();
            }
            win.Select(e);
        }

        public bool AddGameFileToProject(YmapFile ymap)
        {
            if (ymap == null) return false;
            if (win.Project == null) win.Project = new CwProject { Name = "New Project", HasChanged = true };
            var p = win.Project;
            if (p.ContainsYmap(ymap)) return false;
            if (string.IsNullOrEmpty(ymap.FilePath)) ymap.FilePath = ymap.Name;
            if (!p.AddYmapFile(ymap)) return false;
            ymap.HasChanged = true;
            win.Status = ymap.Name + " has been added to the project";
            ProjectYmapsChanged?.Invoke();
            return true;
        }

        public bool AddGameFileToProject(YtypFile ytyp)
        {
            if (ytyp == null) return false;
            if (win.Project == null) win.Project = new CwProject { Name = "New Project", HasChanged = true };
            var p = win.Project;
            if (p.ContainsYtyp(ytyp)) return false;
            if (string.IsNullOrEmpty(ytyp.FilePath)) ytyp.FilePath = ytyp.Name;
            if (!p.AddYtypFile(ytyp)) return false;
            ytyp.HasChanged = true;
            win.Status = ytyp.Name + " has been added to the project";
            ProjectYmapsChanged?.Invoke();
            return true;
        }

        public void NewArchetype()
        {
            var t = win.CurrentYtyp;
            if (t == null) { win.Status = "select a ytyp first"; return; }
            var a = win.Project.NewArchetype(t, win.CurrentArchetype);
            if (a == null) return;
            win.Select(a);
            win.Status = "new archetype in " + t.Name;
        }

        public void DeleteArchetype()
        {
            var a = win.CurrentArchetype;
            var t = win.CurrentYtyp;
            if (a == null || t == null) return;
            if (t.RemoveArchetype(a))
            {
                t.HasChanged = true;
                win.Select(t);
                win.Status = "deleted archetype";
            }
        }

        public void ArchetypesFromYdrs()
        {
            var t = win.CurrentYtyp;
            if (t == null) { win.Status = "select a ytyp first"; return; }
            using var dlg = new OpenFileDialog { Filter = "Ydr files|*.ydr", Multiselect = true };
            if (dlg.ShowDialog(owner()) != DialogResult.OK) return;
            int n = win.Project.NewArchetypesFromYdrs(t, dlg.FileNames, out var problems);
            win.Status = problems.Count == 0 ? $"{n} archetype(s) created" : $"{n} created, {problems.Count} failed: {problems[0]}";
        }

        public void NewEntitySet()
        {
            var mlo = win.CurrentArchetype as MloArchetype;
            if (mlo == null) { win.Status = "select an interior archetype first"; return; }
            var set = MloEditor.AddEntitySet(mlo, "set_" + MloEditor.EntitySetCount(mlo));
            if (set == null) { win.Status = "could not add an entity set"; return; }
            if (mlo.Ytyp != null) mlo.Ytyp.HasChanged = true;
            win.Select(set);
            EntitySetsChanged?.Invoke();
            win.Status = "new entity set";
        }

        public void DeleteEntitySet()
        {
            var set = win.CurrentEntitySet;
            var mlo = set?.OwnerMlo;
            if (set == null || mlo == null) return;
            var r = MloEditor.RemoveEntitySet(mlo, set);
            if (!string.IsNullOrEmpty(r)) { win.Status = r; return; }
            if (mlo.Ytyp != null) mlo.Ytyp.HasChanged = true;
            win.Select(mlo);
            EntitySetsChanged?.Invoke();
            win.Status = "deleted entity set";
        }

        public void NewMloEntity()
        {
            if (NewEntityInInterior?.Invoke(win.CurrentEntity) == true) return;
            var set = win.CurrentEntitySet;
            var mlo = set?.OwnerMlo;
            if (set == null || mlo == null) { win.Status = "select an entity set first"; return; }
            var def = MloEditor.NewEntity(mlo, "v_ind_chickensx3", SpawnPos(), Quaternion.Identity, Vector3.One);
            if (def == null || !MloEditor.AddEntitySetEntity(mlo, set, def, 0)) { win.Status = "could not add the entity"; return; }
            if (mlo.Ytyp != null) mlo.Ytyp.HasChanged = true;
            EntitySetsChanged?.Invoke();
            win.Status = "entity added to " + MloEditor.GetEntitySetName(set);
        }

        public void GenerateManifest()
        {
            var p = win.Project;
            if (p == null || p.YmapFiles.Count == 0) { win.Status = "add a ymap to the project first"; return; }
            var mp = new MapProject();
            foreach (var y in p.YmapFiles) mp.AddYmap(y, y.FilePath ?? y.Name, false);
            foreach (var t in p.YtypFiles) mp.AddYtyp(t, t.FilePath ?? t.Name, false);
            string xml;
            try { xml = mp.BuildManifest(out int skipped, out int ytyps); win.Status = $"manifest: {p.YmapFiles.Count - skipped} ymap(s), {ytyps} ytyp(s)" + (skipped > 0 ? $" - {skipped} unnamed ymap(s) left out" : ""); }
            catch (Exception ex) { win.Status = "manifest failed: " + ex.Message; return; }
            win.ShowManifest(xml);
        }

        public void SaveManifestText()
        {
            if (string.IsNullOrEmpty(win.ManifestText)) return;
            using var dlg = new SaveFileDialog { Filter = "Manifest XML|_manifest.ymf.xml", FileName = "_manifest.ymf.xml" };
            if (dlg.ShowDialog(owner()) != DialogResult.OK) return;
            try
            {
                File.WriteAllText(dlg.FileName, win.ManifestText, new System.Text.UTF8Encoding(false));
                win.Status = "wrote " + Path.GetFileName(dlg.FileName);
            }
            catch (Exception ex) { win.Status = "could not write: " + ex.Message; }
        }

        public void ImportMenyooXml()
        {
            if (win.Project == null) NewProject();
            if (win.Project == null) return;
            using var dlg = new OpenFileDialog { Filter = "Menyoo XML|*.xml|All files|*.*" };
            if (dlg.ShowDialog(owner()) != DialogResult.OK) return;
            string xmlstr;
            try { xmlstr = File.ReadAllText(dlg.FileName); }
            catch (Exception ex) { win.Status = "could not read: " + ex.Message; return; }
            if (string.IsNullOrEmpty(xmlstr)) return;

            var menyoo = new CodeWalker.Project.MenyooXml
            {
                FilePath = dlg.FileName,
                FileName = Path.GetFileName(dlg.FileName),
                Name = Path.GetFileNameWithoutExtension(dlg.FileName),
            };
            try { menyoo.Init(xmlstr); }
            catch (Exception ex) { win.Status = "not a Menyoo file: " + ex.Message; return; }

            var p = win.Project;
            var ymap = p.AddYmapFile(menyoo.Name + ".ymap");
            if (ymap == null) { win.Status = menyoo.Name + ".ymap is already in the project"; return; }
            ymap.Loaded = true;
            ymap.HasChanged = true;
            ymap._CMapData.contentFlags = 65;

            int peds = 0, cars = 0, ents = 0, unk = 0;
            foreach (var pl in menyoo.Placements)
            {
                if (pl.Type == 1) { peds++; continue; }
                if (pl.Type == 2)
                {
                    var ccg = new CCarGen();
                    var rotq = Quaternion.Invert(new Quaternion(pl.Rotation));
                    var cdir = CodeWalker.QuaternionExtension.Multiply(rotq, new Vector3(0, 5, 0));
                    ccg.flags = 3680;
                    ccg.orientX = cdir.X;
                    ccg.orientY = cdir.Y;
                    ccg.perpendicularLength = 2.6f;
                    ccg.position = pl.Position;
                    ccg.carModel = pl.ModelHash;
                    ccg.bodyColorRemap1 = -1; ccg.bodyColorRemap2 = -1; ccg.bodyColorRemap3 = -1; ccg.bodyColorRemap4 = -1;
                    var liv = pl.VehicleProperties?.FirstOrDefault(x => x.Name == "Livery")?.Value;
                    if (sbyte.TryParse(liv, out sbyte livery)) ccg.livery = livery;
                    ymap.AddCarGen(new YmapCarGen(ymap, ccg));
                    cars++;
                    continue;
                }
                if (pl.Type == 3)
                {
                    var cent = new CEntityDef
                    {
                        archetypeName = pl.ModelHash,
                        position = pl.Position,
                        rotation = pl.Rotation,
                        scaleXY = 1.0f,
                        scaleZ = 1.0f,
                        flags = pl.Dynamic ? 0u : 32u,
                        parentIndex = -1,
                        lodDist = pl.LodDistance < 10000 ? pl.LodDistance : 10000,
                        lodLevel = rage__eLodType.LODTYPES_DEPTH_ORPHANHD,
                        priorityLevel = rage__ePriorityLevel.PRI_REQUIRED,
                        ambientOcclusionMultiplier = 255,
                        artificialAmbientOcclusion = 255,
                    };
                    var ent = new YmapEntityDef(ymap, 0, ref cent);
                    ent.SetArchetype(p.FindArchetype(cent.archetypeName, cache()));
                    ymap.AddEntity(ent);
                    ents++;
                    continue;
                }
                unk++;
            }
            ymap.CalcFlags();
            ymap.CalcExtents();
            win.Select(ymap);
            win.Status = $"imported {menyoo.FileName}: {ents} entities, {cars} car generators" +
                         (peds > 0 ? $", {peds} peds skipped" : "") + (unk > 0 ? $", {unk} unknown" : "");
            ProjectYmapsChanged?.Invoke();
        }

        public void ImportYmapXml()
        {
            if (win.Project == null) NewProject();
            using var dlg = new OpenFileDialog { Filter = "Ymap XML|*.ymap.xml|All files|*.*" };
            if (dlg.ShowDialog(owner()) != DialogResult.OK) return;
            try
            {
                var y = XmlIO.ImportYmap(dlg.FileName);
                y.FilePath = Path.ChangeExtension(dlg.FileName, null);
                y.RpfFileEntry ??= new RpfResourceFileEntry();
                y.RpfFileEntry.Name = Path.GetFileName(y.FilePath);
                y.Name = y.RpfFileEntry.Name;
                win.Project.InitYmapArchetypes(y, cache());
                y.Loaded = true;
                y.HasChanged = true;
                win.Project.AddYmapFile(y);
                win.Select(y);
                win.Status = $"imported {y.Name} ({y.AllEntities?.Length ?? 0} entities)";
                ProjectYmapsChanged?.Invoke();
            }
            catch (Exception ex) { win.Status = "XML import failed: " + ex.Message; }
        }
    }
}

