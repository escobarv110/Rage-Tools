using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CodeWalker.GameFiles;
using ImGuiNET;
using RageLightEditor.ImGuiBackend;
using RageLightEditor.Rendering;
using GameTexture = CodeWalker.GameFiles.Texture;
using SDX = SharpDX;

namespace RageLightEditor.Editor
{
    public partial class MaterialPanel
    {
        private Scene scene;
        public Scene Scene { get => scene; set => scene = value ?? scene; }
        private readonly AppSettings settings;

        public ModelRenderer Renderer;
        public TextureLoader Textures;
        public ImGuiRenderer Imgui;
        public GameFileManager Game;

        public Func<uint, GameTexture> LocalTextures;

        public event Func<string> RequestImportTexture;
        public event Action RequestOpenYtd;
        public event Action<MaterialRef> RequestFrameMaterial;
        public event Action RequestAddFile;

        public event Func<List<GameTexture>, string> RequestExportTextures;

        public readonly List<MaterialRef> Materials = new List<MaterialRef>();
        public MaterialRef Selected => Selection.Count > 0 ? Selection[Selection.Count - 1] : null;
        public readonly List<MaterialRef> Selection = new List<MaterialRef>();

        public bool IsolateSelected;
        public string Status = "";

        public static int ForceTab = -1;

        public static bool ForcePresetPopup;
        private bool presetPopupForced;

        private LoadedFile listedFile;
        private int listedGeomVersion = -1;
        private string filter = "";

        public MaterialPanel(Scene scene, AppSettings settings)
        {
            this.scene = scene;
            this.settings = settings;
        }

        public void Sync()
        {
            var file = scene.ActiveFile ?? scene.SelectedFiles.FirstOrDefault() ?? scene.Files.FirstOrDefault();
            bool changed = file != listedFile || scene.GeometryVersion != listedGeomVersion;
            if (!changed)
            {
                MaterialEditing.AttachMeshes(scene, Materials);
                return;
            }

            var keep = Selection.Select(s => s.Shader).ToList();
            listedFile = file;
            listedGeomVersion = scene.GeometryVersion;
            Materials.Clear();
            Materials.AddRange(MaterialEditing.ForFile(scene, file));
            foreach (var m in Materials) RememberOriginals_V24(m.Shader);

            Selection.Clear();
            foreach (var s in keep)
            {
                var m = Materials.FirstOrDefault(x => x.Shader == s);
                if (m != null) Selection.Add(m);
            }
            if (Selection.Count == 0 && Materials.Count > 0) Selection.Add(Materials[0]);
        }

        public void SelectByShader(ShaderFX shader, bool additive = false)
        {
            if (shader == null) return;
            Sync();
            var m = Materials.FirstOrDefault(x => x.Shader == shader);
            if (m == null) return;
            if (!additive) Selection.Clear();
            if (!Selection.Remove(m)) Selection.Add(m);
            scrollToSelected = true;
        }

        private bool scrollToSelected;

        public bool IsSelected(MaterialRef m) => Selection.Contains(m);

        private class MatUndo
        {
            public ShaderFX Target;
            public ShaderFX Snapshot;
            public string Label;

            public GameTexture Tex;
            public uint TexUsage, TexExtra;
        }

        private readonly List<MatUndo> undo = new List<MatUndo>();
        private readonly List<MatUndo> redo = new List<MatUndo>();
        private const int MaxUndo = 128;

        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public string UndoLabel => undo.Count > 0 ? undo[undo.Count - 1].Label : "";

        public void PushUndo(MaterialRef m, string label)
        {
            if (m?.Shader == null) return;
            undo.Add(new MatUndo { Target = m.Shader, Snapshot = MaterialEditing.Clone(m.Shader), Label = label });
            if (undo.Count > MaxUndo) undo.RemoveAt(0);
            redo.Clear();
        }

        public void Undo() => Step(undo, redo);
        public void Redo() => Step(redo, undo);

        private void Step(List<MatUndo> from, List<MatUndo> to)
        {
            if (from.Count == 0) return;
            var e = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);

            if (e.Tex != null)
            {
                to.Add(new MatUndo
                {
                    Target = e.Target,
                    Tex = e.Tex,
                    TexUsage = e.Tex.UsageData,
                    TexExtra = e.Tex.ExtraFlags,
                    Label = e.Label,
                });
                e.Tex.UsageData = e.TexUsage;
                e.Tex.ExtraFlags = e.TexExtra;
                MaterialEditing.MarkDirty(Materials.FirstOrDefault(x => x.Shader == e.Target));
                Status = (from == undo ? "Undo: " : "Redo: ") + e.Label;
                return;
            }

            to.Add(new MatUndo
            {
                Target = e.Target,
                Snapshot = MaterialEditing.Clone(e.Target),
                Label = e.Label,
            });
            MaterialEditing.CopyInto(e.Snapshot, e.Target);
            MaterialEditing.Refresh(scene, Renderer, e.Target);
            var m = Materials.FirstOrDefault(x => x.Shader == e.Target);
            MaterialEditing.MarkDirty(m);
            Status = (from == undo ? "Undo: " : "Redo: ") + e.Label;
        }

        private void ApplyToSelection(Action<MaterialRef> edit)
        {
            foreach (var m in Selection)
            {
                edit(m);
                MaterialEditing.Refresh(scene, Renderer, m.Shader);
                MaterialEditing.MarkDirty(m);
            }
        }

        private void PushUndoSelection(string label)
        {
            foreach (var m in Selection) PushUndo(m, label);
        }

        private readonly Dictionary<GameTexture, IntPtr> texIds = new Dictionary<GameTexture, IntPtr>();

        private IntPtr TextureId(GameTexture tex)
        {
            if (tex?.Data?.FullData == null || Textures == null || Imgui == null) return IntPtr.Zero;
            if (texIds.TryGetValue(tex, out var id)) return id;
            var srv = Textures.GetSRV(tex);
            id = srv != null ? Imgui.RegisterTexture(srv) : IntPtr.Zero;
            texIds[tex] = id;
            return id;
        }

        public void InvalidateTextureCache() => texIds.Clear();

        private Vector4 AccentText()
        {
            return UiTheme.AccentBright;
        }

        private GameTexture FindTextureByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            uint hash = JenkHash.GenHash(name.ToLowerInvariant());
            foreach (var m in Materials)
            {
                var t = m.EmbeddedDict?.Lookup(hash);
                if (t?.Data?.FullData != null) return t;
            }
            foreach (var it in scene.ImportedTextures)
            {
                if (it != null && it.NameHash == hash && it.Data?.FullData != null) return it;
            }
            foreach (var ytd in scene.LoadedYtds)
            {
                var t = ytd?.TextureDict?.Lookup(hash);
                if (t?.Data?.FullData != null) return t;
            }
            return Game != null ? Game.FindTexture(hash, 0) : null;
        }

        private void DrawTexturePreviewTooltip(GameTexture tex, string source)
        {
            if (tex == null) return;
            ImGui.BeginTooltip();
            ImGui.Text(tex.Name ?? "");

            var id = TextureId(tex);
            if (id != IntPtr.Zero)
            {
                const float big = 256f;
                float tw = Math.Max((int)tex.Width, 1);
                float th = Math.Max((int)tex.Height, 1);
                float w = big, h = big;
                if (tw > th) h = big * th / tw;
                else if (th > tw) w = big * tw / th;
                ImGui.Image(id, new Vector2(w, h));
            }
            else
            {
                ImGui.TextColored(UiTheme.AccentBright, "cannot be uploaded - see the slot for why");
            }

            ImGui.Separator();
            ImGui.TextDisabled($"{tex.Width} x {tex.Height}");
            ImGui.TextDisabled($"{tex.Format}");
            ImGui.TextDisabled($"{tex.Levels} mip level(s)");
            int bytes = tex.Data?.FullData?.Length ?? 0;
            ImGui.TextDisabled(bytes >= 1024 ? $"{bytes / 1024} KB" : $"{bytes} bytes");
            if (!string.IsNullOrEmpty(source)) ImGui.TextDisabled($"from {source}");
            ImGui.EndTooltip();
        }

        public void DrawList()
        {
            Sync();

            if (!scene.HasModel)
            {
                ImGui.TextWrapped("Open a .ydr or .yft to edit its materials.");
                if (ImGui.Button("Add prop...")) RequestAddFile?.Invoke();
                return;
            }
            if (listedFile == null)
            {
                ImGui.TextWrapped("Select a prop in the Props tab to see its materials.");
                return;
            }

            ImGui.TextDisabled(listedFile.Name);
            ImGui.SameLine();
            ImGui.Text($"({Materials.Count})");
            if (listedFile.ReadOnly)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.AccentBright);
                ImGui.TextWrapped(Scene.HasWritableResource(listedFile)
                    ? "From the game archives. Edit it freely, then Save As to write a loose .ydr."
                    : "From the game archives, and nothing here can be written to a .ydr.");
                ImGui.PopStyleColor();
            }

            if (Materials.Count > 8)
            {
                ImGui.SetNextItemWidth(-56);
                ImGui.InputTextWithHint("##matfilter", "search materials...", ref filter, 64);
                ImGui.SameLine();
                if (ImGui.SmallButton("clear##mf")) filter = "";
            }

            ImGui.Checkbox("Isolate", ref IsolateSelected);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Draw only the geometry using the selected material,\n" +
                                 "so you can see exactly what you're editing.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Frame") && Selected != null) RequestFrameMaterial?.Invoke(Selected);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Point the camera at this material's geometry");

            ImGui.Separator();

            HoveredShader = null;

            if (ImGui.BeginChild("matlist", new Vector2(0, -22), ImGuiChildFlags.Borders))
            {
                foreach (var m in Materials)
                {
                    if (filter.Length > 0 &&
                        m.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                        m.Sps.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    DrawSwatch(m);
                    ImGui.SameLine();

                    string label = $"{m.Label}##mat{m.Index}";
                    if (ImGui.Selectable(label, IsSelected(m)))
                    {
                        var io = ImGui.GetIO();
                        if (io.KeyCtrl) { if (!Selection.Remove(m)) Selection.Add(m); }
                        else if (io.KeyShift && Selection.Count > 0)
                        {
                            int a = Materials.IndexOf(Selection[Selection.Count - 1]);
                            int b = Materials.IndexOf(m);
                            Selection.Clear();
                            for (int i = Math.Min(a, b); i <= Math.Max(a, b); i++) Selection.Add(Materials[i]);
                        }
                        else { Selection.Clear(); Selection.Add(m); }
                    }
                    if (scrollToSelected && IsSelected(m)) ImGui.SetScrollHereY(0.5f);
                    if (ImGui.IsItemHovered())
                    {
                        HoveredShader = m.Shader;
                        DrawMaterialTooltip(m);
                    }
                    DrawMaterialContextMenu(m);
                    if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        RequestFrameMaterial?.Invoke(m);

                    ImGui.SameLine();
                    ImGui.TextDisabled($"x{m.Meshes.Count}");
                }
                scrollToSelected = false;
            }
            ImGui.EndChild();

            ImGui.TextDisabled(Selection.Count > 1
                ? $"{Selection.Count} selected - edits apply to all"
                : "Ctrl/Shift+click: multi-select");
        }

        public ShaderFX HoveredShader { get; private set; }

        private void DrawMaterialContextMenu(MaterialRef m)
        {
            if (!ImGui.BeginPopupContextItem($"matctx{m.Index}")) return;

            ImGui.TextDisabled(m.Name);
            ImGui.Separator();

            var mine = TexturesOf(m);
            if (ImGui.MenuItem($"Export this material's textures ({mine.Count})", null, false, mine.Count > 0))
            {
                var dir = RequestExportTextures?.Invoke(mine);
                if (dir != null) Status = $"Exported {mine.Count} texture(s) to {dir}";
            }

            var all = new List<GameTexture>();
            var seen = new HashSet<uint>();
            foreach (var other in Materials)
            {
                foreach (var t in TexturesOf(other))
                {
                    if (seen.Add(t.NameHash)) all.Add(t);
                }
            }
            if (ImGui.MenuItem($"Export all textures in this prop ({all.Count})", null, false, all.Count > 0))
            {
                var dir = RequestExportTextures?.Invoke(all);
                if (dir != null) Status = $"Exported {all.Count} texture(s) to {dir}";
            }

            ImGui.Separator();
            if (ImGui.MenuItem("Copy this material"))
            {
                Clipboard_V27 = CopyMaterial_V27(m);
                Status = Clipboard_V27 != null ? "Copied " + Clipboard_V27.Summary : "Nothing to copy";
            }
            var clip = Clipboard_V27;
            string pasteLabel = clip == null
                ? "Paste material"
                : (Selection.Count > 1 ? $"Paste {clip.ShaderName} onto {Selection.Count} materials"
                                       : $"Paste {clip.ShaderName} here");
            if (ImGui.MenuItem(pasteLabel, null, false, clip != null && m.CanSave))
            {
                if (!IsSelected(m)) { Selection.Clear(); Selection.Add(m); }
                PushUndoSelection("paste material");
                var c = clip;
                ApplyToSelection(x => PasteMaterial_V27(c, x));
                listedGeomVersion = -1;
                Status = $"Pasted {c.ShaderName} onto {Selection.Count} material(s)";
            }

            ImGui.Separator();
            if (ImGui.MenuItem("Select this material")) { Selection.Clear(); Selection.Add(m); }
            if (ImGui.MenuItem("Frame its geometry", null, false, m.Meshes.Count > 0))
                RequestFrameMaterial?.Invoke(m);
            if (ImGui.MenuItem("Isolate it", null, IsolateSelected && Selected == m, true))
            {
                Selection.Clear();
                Selection.Add(m);
                IsolateSelected = !IsolateSelected;
            }

            ImGui.EndPopup();
        }

        public List<GameTexture> TexturesOfSelected() =>
            Selected != null ? TexturesOf(Selected) : new List<GameTexture>();

        public List<GameTexture> TexturesOfAll()
        {
            var all = new List<GameTexture>();
            var seen = new HashSet<uint>();
            foreach (var m in Materials)
            {
                foreach (var t in TexturesOf(m))
                {
                    if (seen.Add(t.NameHash)) all.Add(t);
                }
            }
            return all;
        }

        private List<GameTexture> TexturesOf(MaterialRef m)
        {
            var list = new List<GameTexture>();
            var seen = new HashSet<uint>();
            foreach (var h in MaterialEditing.ParamHashes(m.Shader).ToList())
            {
                var tb = MaterialEditing.GetTexture(m.Shader, h);
                if (tb == null) continue;
                var res = MaterialEditing.Resolve(scene, m, tb,
                    Game != null ? Game.FindTexture : (Func<uint, uint, GameTexture>)null, m.TxdContext, LocalTextures);
                if (res.Found && seen.Add(res.Texture.NameHash)) list.Add(res.Texture);
            }
            return list;
        }

        private void DrawMaterialTooltip(MaterialRef m)
        {
            ImGui.BeginTooltip();
            ImGui.Text(m.Name);
            ImGui.TextDisabled(m.Sps);
            ImGui.TextDisabled($"bucket {MaterialDefs.BucketName(m.Bucket)}  -  " +
                               $"{m.Meshes.Count} geometry piece(s)");
            if (m.Meshes.Count > 1)
                ImGui.TextColored(UiTheme.AccentBright, "Editing it changes all of them.");
            ImGui.Separator();

            const float thumb = 128f;
            bool any = false;
            foreach (var slot in MaterialEditing.ParamHashes(m.Shader)
                         .Where(h =>
                         {
                             int i = MaterialEditing.IndexOf(m.Shader, h);
                             return i >= 0 && m.Shader.ParametersList.Parameters[i].DataType == 0;
                         })
                         .Select(MaterialDefs.TexOrOther)
                         .OrderBy(t => t.Order))
            {
                var tb = MaterialEditing.GetTexture(m.Shader, slot.Hash);
                if (tb == null) continue;
                var res = MaterialEditing.Resolve(scene, m, tb,
                    Game != null ? Game.FindTexture : (Func<uint, uint, GameTexture>)null, m.TxdContext, LocalTextures);

                if (any) ImGui.SameLine();
                any = true;
                ImGui.BeginGroup();
                var id = res.Found ? TextureId(res.Texture) : IntPtr.Zero;
                if (id != IntPtr.Zero)
                {
                    ImGui.Image(id, new Vector2(thumb, thumb));
                }
                else
                {
                    var dl = ImGui.GetWindowDrawList();
                    var p = ImGui.GetCursorScreenPos();
                    var sz = new Vector2(thumb, thumb);
                    dl.AddRectFilled(p, p + sz, ImGui.ColorConvertFloat4ToU32(new Vector4(0.12f, 0.12f, 0.14f, 1)));
                    dl.AddRect(p, p + sz, ImGui.ColorConvertFloat4ToU32(new Vector4(0.5f, 0.3f, 0.3f, 1)));
                    ImGui.Dummy(sz);
                }
                ImGui.TextDisabled(slot.Label);
                ImGui.TextDisabled(tb.Name ?? "");
                if (res.Found)
                    ImGui.TextDisabled($"{res.Texture.Width}x{res.Texture.Height} {res.Texture.Format}");
                else
                    ImGui.TextColored(UiTheme.Danger, "MISSING");
                ImGui.EndGroup();
            }
            if (!any) ImGui.TextDisabled("no textures assigned");

            ImGui.EndTooltip();
        }

        private void DrawSwatch(MaterialRef m)
        {
            float h = ImGui.GetTextLineHeight();
            var size = new Vector2(h, h);

            var tb = MaterialEditing.GetTexture(m.Shader, (uint)ShaderParamNames.DiffuseSampler);
            var res = MaterialEditing.Resolve(scene, m, tb, Game != null ? Game.FindTexture : (Func<uint, uint, GameTexture>)null, m.TxdContext, LocalTextures);
            var id = res.Found ? TextureId(res.Texture) : IntPtr.Zero;
            if (id != IntPtr.Zero)
            {
                ImGui.Image(id, size);
                return;
            }

            var c = MaterialEditing.GetValue(m.Shader, (uint)ShaderParamNames.matDiffuseColor,
                new SDX.Vector4(0.6f, 0.6f, 0.6f, 1));
            var dl = ImGui.GetWindowDrawList();
            var p = ImGui.GetCursorScreenPos();
            dl.AddRectFilled(p, p + size, ImGui.ColorConvertFloat4ToU32(new Vector4(c.X, c.Y, c.Z, 1)));
            dl.AddRect(p, p + size, ImGui.ColorConvertFloat4ToU32(new Vector4(0, 0, 0, 0.6f)));
            ImGui.Dummy(size);
        }

        public void DrawEditor()
        {
            Sync();
            var m = Selected;
            if (m == null)
            {
                ImGui.TextDisabled(scene.HasModel
                    ? "Select a material in the Materials list, or right-click a surface in the viewport."
                    : "Open a .ydr or .yft to edit its materials.");
                return;
            }

            ImGui.Text(m.Name);
            ImGui.SameLine();
            ImGui.TextDisabled(m.Sps);
            if (m.Meshes.Count > 1)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.AccentBright);
                ImGui.TextWrapped($"Shared by {m.Meshes.Count} geometry pieces - edits affect all of them.");
                ImGui.PopStyleColor();
            }
            if (Selection.Count > 1)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, AccentText());
                ImGui.TextWrapped($"{Selection.Count} materials selected - every edit below applies to all of them.");
                ImGui.PopStyleColor();
            }

            if (!ImGui.BeginTabBar("mattabs")) return;

            var tabs = new (string Name, Action<MaterialRef> Draw)[]
            {
                ("Textures", DrawTextures),
                ("Parameters", DrawParameters),
            };
            for (int i = 0; i < tabs.Length; i++)
            {
                bool dummy = true;
                bool open = ForceTab == i
                    ? ImGui.BeginTabItem(tabs[i].Name, ref dummy, ImGuiTabItemFlags.SetSelected)
                    : ImGui.BeginTabItem(tabs[i].Name);
                if (open) { tabs[i].Draw(m); ImGui.EndTabItem(); }
            }

            ImGui.EndTabBar();
        }

        private void DrawTextures(MaterialRef m)
        {
            var slots = MaterialEditing.ParamHashes(m.Shader)
                .Where(h => MaterialEditing.IndexOf(m.Shader, h) >= 0 &&
                            m.Shader.ParametersList.Parameters[MaterialEditing.IndexOf(m.Shader, h)].DataType == 0)
                .Select(MaterialDefs.TexOrOther)
                .OrderBy(t => t.Order)
                .ToList();

            if (slots.Count == 0)
            {
                ImGui.TextWrapped("This shader declares no texture slots.");
            }

            foreach (var slot in slots) DrawTextureSlot(m, slot);

            ImGui.Separator();
            if (ImGui.SmallButton("Load YTD...")) RequestOpenYtd?.Invoke();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Load a texture dictionary so its textures become assignable here.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Import image..."))
            {
                var name = RequestImportTexture?.Invoke();
                if (!string.IsNullOrEmpty(name)) Status = $"Imported {name} - assign it from a slot's Browse list";
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Bring in a loose .dds / .png / .jpg. Embed it afterwards to make it\n" +
                                 "part of the saved .ydr - otherwise the game won't find it.");
        }

        private void DrawTextureSlot(MaterialRef m, MatTexInfo slot)
        {
            ImGui.PushID((int)slot.Hash);

            var tb = MaterialEditing.GetTexture(m.Shader, slot.Hash);
            var res = MaterialEditing.Resolve(scene, m, tb,
                Game != null ? Game.FindTexture : (Func<uint, uint, GameTexture>)null, m.TxdContext, LocalTextures);

            float preview = ImGui.GetTextLineHeightWithSpacing() * 3.2f;
            var id = res.Found ? TextureId(res.Texture) : IntPtr.Zero;
            if (id != IntPtr.Zero) ImGui.Image(id, new Vector2(preview, preview));
            else
            {
                var dl = ImGui.GetWindowDrawList();
                var p = ImGui.GetCursorScreenPos();
                var sz = new Vector2(preview, preview);
                dl.AddRectFilled(p, p + sz, ImGui.ColorConvertFloat4ToU32(new Vector4(0.12f, 0.12f, 0.14f, 1)));
                dl.AddRect(p, p + sz, ImGui.ColorConvertFloat4ToU32(new Vector4(0.4f, 0.4f, 0.45f, 1)));
                ImGui.Dummy(sz);
            }
            if (ImGui.IsItemHovered() && res.Found)
            {
                DrawTexturePreviewTooltip(res.Texture, res.SourceName);
            }

            ImGui.SameLine();
            ImGui.BeginGroup();

            ImGui.Text(slot.Label);
            if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(slot.Tooltip)) ImGui.SetTooltip(slot.Tooltip);

            if (tb == null)
            {
                ImGui.TextDisabled("(none)");
            }
            else if (res.Found && id != IntPtr.Zero)
            {
                ImGui.Text(tb.Name ?? "");
                var col = res.Source == MatTexSource.Embedded
                    ? new Vector4(0.45f, 0.9f, 0.55f, 1f)
                    : AccentText();
                ImGui.TextColored(col, res.SourceName);
            }
            else if (res.Found)
            {
                ImGui.Text(tb.Name ?? "");
                int have = res.Texture.Data?.FullData?.Length ?? 0;
                int need = TextureLoader.Mip0Size(res.Texture.Format, res.Texture.Width, res.Texture.Height);
                ImGui.TextColored(UiTheme.AccentBright, "BROKEN - cannot be uploaded");
                if (have < need)
                {
                    ImGui.TextColored(UiTheme.AccentBright,
                        $"{res.Texture.Format} {res.Texture.Width}x{res.Texture.Height} needs " +
                        $"{need} bytes, file has {have}");
                }
                else
                {
                    ImGui.TextColored(UiTheme.AccentBright,
                        $"unsupported format {res.Texture.Format}");
                }
            }
            else
            {
                ImGui.Text(tb.Name ?? "");
                ImGui.TextColored(UiTheme.Danger, "MISSING - not in any loaded dictionary");
            }

            if (ImGui.SmallButton("Browse...")) ImGui.OpenPopup("texbrowse");
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear"))
            {
                PushUndoSelection($"clear {slot.Label}");
                ApplyToSelection(x => MaterialEditing.SetTexture(x.Shader, slot.Hash, null));
            }
            if (res.Found && m.CanSave)
            {
                bool embedded = MaterialEditing.IsEmbedded(m, res.Texture.NameHash);
                ImGui.SameLine();
                if (!embedded)
                {
                    if (ImGui.SmallButton("Embed"))
                    {
                        PushUndo(m, $"embed {res.Texture.Name}");
                        MaterialEditing.EmbedTexture(m, res.Texture);
                        MaterialEditing.Refresh(scene, Renderer, m.Shader);
                        MaterialEditing.MarkDirty(m);
                        Status = $"Embedded {res.Texture.Name} into {m.File?.Name}";
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Copy this texture's pixels into the drawable itself, so the saved\n" +
                                         ".ydr carries it and doesn't depend on a YTD being loaded.");
                }
                else
                {
                    if (ImGui.SmallButton("Unembed"))
                    {
                        PushUndo(m, $"unembed {res.Texture.Name}");
                        if (MaterialEditing.UnembedTexture(Materials, m, res.Texture, out var why))
                        {
                            MaterialEditing.Refresh(scene, Renderer, m.Shader);
                            MaterialEditing.MarkDirty(m);
                            Status = $"Unembedded {res.Texture.Name} - now referenced by name";
                        }
                        else
                        {
                            Undo();
                            Status = "Can't unembed: " + why;
                        }
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Take this texture back out of the drawable and reference it by name\n" +
                                         "instead. Shrinks the .ydr when the texture already ships in a YTD\n" +
                                         "the game loads. Refused if another material still uses it.");
                }
            }

            if (ImGui.SmallButton("Remove slot")) RemoveTextureSlot_V27(slot);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Take this sampler out of the material entirely. Clear only empties it,\n" +
                                 "which leaves the material still declaring a slot with nothing in it.");

            DrawTextureBrowser(m, slot);

            DrawTextureProps_V27(m, slot, res);

            ImGui.EndGroup();
            ImGui.Separator();
            ImGui.PopID();
        }

        private string browseFilter = "";

        private void AssignTexture(MatTexInfo slot, string name)
        {
            PushUndoSelection($"set {slot.Label}");
            ApplyToSelection(x =>
            {
                if (!MaterialEditing.SetTexture(x.Shader, slot.Hash, MaterialEditing.MakeTextureRef(name)))
                    MaterialEditing.AddParam(x.Shader, slot.Hash, MaterialEditing.MakeTextureRef(name), true);
            });
            Status = $"{slot.Label} = {name}";
        }

        private void DrawTextureBrowser(MaterialRef m, MatTexInfo slot)
        {
            if (!ImGui.BeginPopup("texbrowse")) return;

            ImGui.TextDisabled($"Assign to {slot.Label}");

            if (ImGui.Button("Browse my PC..."))
            {
                var imported = RequestImportTexture?.Invoke();
                if (!string.IsNullOrEmpty(imported))
                {
                    AssignTexture(slot, imported);
                    ImGui.CloseCurrentPopup();
                }
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Pick a .dds / .png / .jpg from disk. It's assigned straight away;\n" +
                                 "use Embed afterwards to store it inside the .ydr, or the game\n" +
                                 "won't find it.");
            ImGui.SameLine();
            if (ImGui.Button("Load YTD...")) { RequestOpenYtd?.Invoke(); }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Load a texture dictionary; everything in it appears in the list below.");

            ImGui.Separator();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##texfilter", "search or type a texture name...", ref browseFilter, 128);

            if (browseFilter.Length > 0)
            {
                bool known = FindTextureByName(browseFilter) != null;
                if (ImGui.Button($"Use \"{browseFilter}\" as a name reference"))
                {
                    AssignTexture(slot, browseFilter);
                    ImGui.CloseCurrentPopup();
                }
                ImGui.SameLine();
                if (known) ImGui.TextColored(UiTheme.Ok, "found");
                else ImGui.TextColored(UiTheme.AccentBright, "not loaded here");
            }

            ImGui.Separator();
            if (ImGui.BeginChild("texlist", new Vector2(420, 340)))
            {
                float row = ImGui.GetTextLineHeightWithSpacing() * 2.4f;
                foreach (var (name, source) in AvailableTextures())
                {
                    if (browseFilter.Length > 0 &&
                        name.IndexOf(browseFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    ImGui.PushID(name);
                    var tex = FindTextureByName(name);
                    var id = tex != null ? TextureId(tex) : IntPtr.Zero;

                    bool clicked = ImGui.Selectable("##row", false, ImGuiSelectableFlags.None,
                        new Vector2(0, row));
                    bool hovered = ImGui.IsItemHovered();
                    ImGui.SameLine(0, 0);
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 2);

                    ImGui.BeginGroup();
                    if (id != IntPtr.Zero) ImGui.Image(id, new Vector2(row - 4, row - 4));
                    else
                    {
                        var dl = ImGui.GetWindowDrawList();
                        var p = ImGui.GetCursorScreenPos();
                        var sz = new Vector2(row - 4, row - 4);
                        dl.AddRectFilled(p, p + sz, ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.15f, 0.17f, 1)));
                        ImGui.Dummy(sz);
                    }
                    ImGui.EndGroup();
                    ImGui.SameLine();
                    ImGui.BeginGroup();
                    ImGui.Text(name);
                    ImGui.TextDisabled(tex != null ? $"{source}  -  {tex.Width}x{tex.Height} {tex.Format}" : source);
                    ImGui.EndGroup();

                    if (hovered && tex != null) DrawTexturePreviewTooltip(tex, source);
                    if (clicked)
                    {
                        AssignTexture(slot, name);
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.PopID();
                }
            }
            ImGui.EndChild();
            ImGui.EndPopup();
        }

        private IEnumerable<(string Name, string Source)> AvailableTextures()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var m in Materials)
            {
                var items = m.EmbeddedDict?.Textures?.data_items;
                if (items == null) continue;
                foreach (var t in items)
                {
                    if (t?.Name != null && seen.Add(t.Name)) yield return (t.Name, "embedded");
                }
            }
            foreach (var it in scene.ImportedTextures)
            {
                if (it?.Name != null && seen.Add(it.Name)) yield return (it.Name, "imported");
            }
            for (int i = 0; i < scene.LoadedYtds.Count; i++)
            {
                var items = scene.LoadedYtds[i]?.TextureDict?.Textures?.data_items;
                if (items == null) continue;
                var label = i < scene.LoadedYtdPaths.Count
                    ? System.IO.Path.GetFileNameWithoutExtension(scene.LoadedYtdPaths[i]) : "ytd";
                foreach (var t in items)
                {
                    if (t?.Name != null && seen.Add(t.Name)) yield return (t.Name, label);
                }
            }
        }

        private void DrawParameters(MaterialRef m)
        {
            float labelCol = Math.Max(ImGui.GetContentRegionAvail().X * 0.30f, 84f);

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Shader");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The RAGE shader this material uses. Changing it changes which\n" +
                                 "parameters and texture slots exist below.");
            ImGui.SameLine();
            ImGui.SetCursorPosX(labelCol);
            if (!m.CanSave) ImGui.BeginDisabled();
            bool openPreset = ImGui.Button($"{m.Name}   v##shaderbtn", new Vector2(-1, 0));
            presetAnchor = new Vector2(ImGui.GetItemRectMin().X, ImGui.GetItemRectMax().Y);
            if (!m.CanSave) ImGui.EndDisabled();
            if (ForcePresetPopup && !presetPopupForced) { openPreset = true; presetPopupForced = true; }
            if (openPreset) ImGui.OpenPopup("presetpick");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(m.CanSave
                    ? $"{m.Sps}\nClick to change preset. Parameters and textures the new preset\n" +
                      "shares with this one keep their values."
                    : "This prop came from the game archives, so preset changes would be\n" +
                      "preview-only - there's no file to write them to.");
            DrawPresetPicker(m);

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Draws in");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Which pass this material draws in: opaque writes depth, cutout\n" +
                                 "alpha-tests, decal and alpha blend without writing depth.");
            ImGui.SameLine();
            ImGui.SetCursorPosX(labelCol);
            int bucket = m.Bucket;
            ImGui.SetNextItemWidth(-1);
            if (ImGui.Combo("##bucket", ref bucket, MaterialDefs.BucketNames, MaterialDefs.BucketNames.Length))
            {
                PushUndoSelection("render bucket");
                byte b = (byte)bucket;
                ApplyToSelection(x => MaterialEditing.SetBucket(x.Shader, b));
                Status = "Render bucket = " + MaterialDefs.BucketName(b);
            }

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Used by");
            ImGui.SameLine();
            ImGui.SetCursorPosX(labelCol);
            ImGui.TextUnformatted($"{m.Meshes.Count} geometry piece(s)");
            bool canUnique = m.Meshes.Count > 1 && m.Group != null;
            if (canUnique)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton("Make unique")) MakeSelectedUnique(m);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Give the first geometry piece its own copy of this material, so\n" +
                                     "editing it stops changing the others.");
            }

            ImGui.Separator();
            ImGui.TextDisabled("PARAMETERS");

            var valueHashes = MaterialEditing.ParamHashes(m.Shader)
                .Where(h =>
                {
                    int i = MaterialEditing.IndexOf(m.Shader, h);
                    return i >= 0 && m.Shader.ParametersList.Parameters[i].DataType != 0;
                })
                .ToList();

            if (valueHashes.Count == 0) ImGui.TextWrapped("This shader declares no value parameters.");

            foreach (var h in valueHashes) DrawParam(m, h);

            ImGui.Separator();
            if (ImGui.SmallButton("Add parameter...")) ImGui.OpenPopup("addparam");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Give this material a parameter its preset left out. The game reads\n" +
                                 "parameters by name, so one it doesn't use is simply ignored.");
            DrawAddParamPopup(m);

            ImGui.SameLine();
            DrawClipboardButtons_V27(m);
        }

        private void MakeSelectedUnique(MaterialRef m)
        {
            PushUndo(m, "make unique");
            var made = MaterialEditing.MakeUnique(scene, m, m.Meshes.Take(1));
            if (made == null) return;
            listedGeomVersion = -1;
            Sync();
            Selection.Clear();
            var fresh = Materials.FirstOrDefault(x => x.Shader == made.Shader);
            if (fresh != null) Selection.Add(fresh);
            MaterialEditing.MarkDirty(m);
            Status = "Split off a private copy of " + m.Name;
        }

        private void DrawParam(MaterialRef m, uint hash)
        {
            var info = MaterialDefs.ParamOrRaw(hash);
            var v = MaterialEditing.GetValue(m.Shader, hash, info.Default);
            var n = new Vector4(v.X, v.Y, v.Z, v.W);
            bool changed = false;
            bool activated = false;
            bool buttonsDrawn = false;

            ImGui.PushID((int)hash);

            float rowStart = ImGui.GetCursorPosX();
            float nameCol = Math.Max(ImGui.GetContentRegionAvail().X * 0.42f, 96f);
            float buttonRoom = ImGui.CalcTextSize("reset").X + ImGui.CalcTextSize("x").X +
                               ImGui.GetStyle().FramePadding.X * 4 + ImGui.GetStyle().ItemSpacing.X * 3;

            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(info.Label);
            if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(info.Tooltip)) ImGui.SetTooltip(info.Tooltip);
            ImGui.SameLine();
            ImGui.SetCursorPosX(rowStart + nameCol);
            ImGui.SetNextItemWidth(-buttonRoom);

            switch (info.Kind)
            {
                case MatParamKind.Float:
                    float f = n.X;
                    changed = info.Log
                        ? UiSlider_U28.Float("##v", ref f, info.Min, info.Max, "%.2f",
                            ImGuiSliderFlags.Logarithmic)
                        : UiSlider_U28.Float("##v", ref f, info.Min, info.Max);
                    activated = ImGui.IsItemActivated();
                    n.X = f;
                    break;

                case MatParamKind.Float2:
                {
                    var v2 = new Vector2(n.X, n.Y);
                    changed = ImGui.DragFloat2("##v", ref v2, 0.01f, info.Min, info.Max);
                    activated = ImGui.IsItemActivated();
                    n.X = v2.X; n.Y = v2.Y;
                    break;
                }

                case MatParamKind.Colour:
                {
                    var c3 = new Vector3(n.X, n.Y, n.Z);
                    changed = ImGui.ColorEdit3("##v", ref c3);
                    activated = ImGui.IsItemActivated();
                    n.X = c3.X; n.Y = c3.Y; n.Z = c3.Z;
                    break;
                }

                case MatParamKind.Colour4:
                    changed = ImGui.ColorEdit4("##v", ref n);
                    activated = ImGui.IsItemActivated();
                    break;

                case MatParamKind.ChannelMask:
                {
                    int ch = n.Y > 0.5f ? 1 : n.Z > 0.5f ? 2 : 0;
                    if (ImGui.RadioButton("R", ref ch, 0)) changed = true;
                    ImGui.SameLine();
                    if (ImGui.RadioButton("G", ref ch, 1)) changed = true;
                    ImGui.SameLine();
                    if (ImGui.RadioButton("B", ref ch, 2)) changed = true;
                    if (changed)
                    {
                        activated = true;
                        n = new Vector4(ch == 0 ? 1 : 0, ch == 1 ? 1 : 0, ch == 2 ? 1 : 0, 0);
                    }
                    break;
                }

                case MatParamKind.Float4:
                {
                    buttonsDrawn = true;
                    DrawParamButtons(info, hash);
                    for (int i = 0; i < 4; i++)
                    {
                        string sub = info.SubLabels != null && i < info.SubLabels.Length
                            ? info.SubLabels[i] : "xyzw"[i].ToString();
                        if (sub == "unused") continue;
                        ImGui.AlignTextToFramePadding();
                        ImGui.SetCursorPosX(rowStart + ImGui.GetStyle().IndentSpacing);
                        ImGui.TextDisabled(sub);
                        ImGui.SameLine();
                        ImGui.SetCursorPosX(rowStart + nameCol);
                        ImGui.SetNextItemWidth(-buttonRoom);
                        float c = i == 0 ? n.X : i == 1 ? n.Y : i == 2 ? n.Z : n.W;
                        if (ImGui.DragFloat("##c" + i, ref c, 0.05f, info.Min, info.Max))
                        {
                            changed = true;
                            if (i == 0) n.X = c; else if (i == 1) n.Y = c; else if (i == 2) n.Z = c; else n.W = c;
                        }
                        if (ImGui.IsItemActivated()) activated = true;
                    }
                    break;
                }

                default:
                    changed = ImGui.DragFloat4("##v", ref n, 0.01f, info.Min, info.Max);
                    activated = ImGui.IsItemActivated();
                    break;
            }

            if (activated) PushUndoSelection(info.Label);

            if (changed)
            {
                var val = new SDX.Vector4(n.X, n.Y, n.Z, n.W);
                ApplyToSelection(x =>
                {
                    if (!MaterialEditing.SetValue(x.Shader, hash, val))
                        MaterialEditing.AddParam(x.Shader, hash, val, false);
                });
            }

            if (!buttonsDrawn)
            {
                ImGui.SameLine();
                DrawParamButtons(info, hash);
            }

            ImGui.PopID();
        }

        private readonly Dictionary<ShaderFX, Dictionary<uint, SharpDX.Vector4>> originals_V24 = new Dictionary<ShaderFX, Dictionary<uint, SharpDX.Vector4>>();

        private void RememberOriginals_V24(ShaderFX s)
        {
            if (s == null || originals_V24.ContainsKey(s)) return;
            var d = new Dictionary<uint, SharpDX.Vector4>();
            foreach (var h in MaterialEditing.ParamHashes(s))
            {
                int i = MaterialEditing.IndexOf(s, h);
                if (i < 0 || s.ParametersList.Parameters[i].DataType == 0) continue;
                d[h] = MaterialEditing.GetValue(s, h, SharpDX.Vector4.Zero);
            }
            originals_V24[s] = d;
        }

        public void ResetParam_V24(MatParamInfo info, uint hash)
        {
            PushUndoSelection("reset " + info.Label);
            var d = new SharpDX.Vector4(info.Default.X, info.Default.Y, info.Default.Z, info.Default.W);
            ApplyToSelection(x => MaterialEditing.SetValue(x.Shader, hash,
                TryOriginal_V24(x.Shader, hash, out var o) ? o : d));
        }

        public static int ResetSelfTest_V24(Action<string, bool, string> check, Scene scene, MaterialPanel panel)
        {
            int fails = 0;
            panel.Sync();
            var m = panel.Materials.FirstOrDefault(x => MaterialEditing.Has(x.Shader, (uint)ShaderParamNames.specularFresnel))
                 ?? panel.Materials.FirstOrDefault(x => MaterialEditing.ParamHashes(x.Shader).Any(h => MaterialEditing.IndexOf(x.Shader, h) >= 0 && x.Shader.ParametersList.Parameters[MaterialEditing.IndexOf(x.Shader, h)].DataType != 0));
            if (m == null) { check("v24 reset: a material with a value parameter to test on", false, "none listed"); return 1; }
            uint hash = MaterialEditing.Has(m.Shader, (uint)ShaderParamNames.specularFresnel)
                ? (uint)ShaderParamNames.specularFresnel
                : MaterialEditing.ParamHashes(m.Shader).First(h => MaterialEditing.IndexOf(m.Shader, h) >= 0 && m.Shader.ParametersList.Parameters[MaterialEditing.IndexOf(m.Shader, h)].DataType != 0);
            var original = MaterialEditing.GetValue(m.Shader, hash, SharpDX.Vector4.Zero);
            panel.Selection.Clear(); panel.Selection.Add(m);
            MaterialEditing.SetValue(m.Shader, hash, original + new SharpDX.Vector4(0.31f, 0, 0, 0));
            var edited = MaterialEditing.GetValue(m.Shader, hash, SharpDX.Vector4.Zero);
            var info = MaterialDefs.ParamOrRaw(hash);
            panel.ResetParam_V24(info, hash);
            var back = MaterialEditing.GetValue(m.Shader, hash, SharpDX.Vector4.Zero);
            bool ok = (back - original).Length() < 1e-5f && (edited - original).Length() > 0.3f;
            if (!ok) fails++;
            check($"v24 reset: '{info.Label}' goes back to the FILE's value, not the preset's", ok,
                  $"file {original.X:0.###}, edited {edited.X:0.###}, reset {back.X:0.###} (preset default {info.Default.X:0.###})");
            return fails;
        }

        private bool TryOriginal_V24(ShaderFX s, uint hash, out SharpDX.Vector4 v)
        {
            v = default;
            return s != null && originals_V24.TryGetValue(s, out var d) && d.TryGetValue(hash, out v);
        }

        private void DrawParamButtons(MatParamInfo info, uint hash)
        {
            bool haveOrig = false; SharpDX.Vector4 orig = default;
            foreach (var sel in Selection)
                if (TryOriginal_V24(sel.Shader, hash, out orig)) { haveOrig = true; break; }
            if (ImGui.SmallButton("reset")) ResetParam_V24(info, hash);
            if (ImGui.IsItemHovered())
            {
                var back = haveOrig ? orig : new SharpDX.Vector4(info.Default.X, info.Default.Y, info.Default.Z, info.Default.W);
                ImGui.SetTooltip((haveOrig ? "Back to the file's value " : "The file never had this - back to the preset default ") +
                                 $"{back.X:0.###}" +
                                 (info.Kind == MatParamKind.Float ? "" : $", {back.Y:0.###}, {back.Z:0.###}, {back.W:0.###}"));
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("x"))
            {
                PushUndoSelection("remove " + info.Label);
                ApplyToSelection(x => MaterialEditing.RemoveParam(x.Shader, hash));
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Drop this parameter entirely - the shader falls back to its own default.");
        }

        private string addParamFilter = "";

        private void DrawAddParamPopup(MaterialRef m)
        {
            if (!ImGui.BeginPopup("addparam")) return;
            ImGui.SetNextItemWidth(260);
            ImGui.InputTextWithHint("##addpf", "search parameters...", ref addParamFilter, 64);
            if (ImGui.BeginChild("addplist", new Vector2(300, 220)))
            {
                foreach (var p in MaterialDefs.KnownParams)
                {
                    if (MaterialEditing.Has(m.Shader, p.Hash)) continue;
                    if (addParamFilter.Length > 0 &&
                        p.Label.IndexOf(addParamFilter, StringComparison.OrdinalIgnoreCase) < 0 &&
                        p.Name.IndexOf(addParamFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    if (ImGui.Selectable($"{p.Label}  ({p.Name})"))
                    {
                        PushUndoSelection("add " + p.Label);
                        var def = p.Default;
                        var hash = p.Hash;
                        ApplyToSelection(x => MaterialEditing.AddParam(x.Shader, hash, def, false));
                        Status = "Added " + p.Label;
                        ImGui.CloseCurrentPopup();
                    }
                    if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(p.Tooltip)) ImGui.SetTooltip(p.Tooltip);
                }
                foreach (var t in MaterialDefs.KnownTextures)
                {
                    if (MaterialEditing.Has(m.Shader, t.Hash)) continue;
                    if (addParamFilter.Length > 0 &&
                        t.Label.IndexOf(addParamFilter, StringComparison.OrdinalIgnoreCase) < 0 &&
                        t.Name.IndexOf(addParamFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    if (ImGui.Selectable($"{t.Label}  ({t.Name})  [texture]"))
                    {
                        PushUndoSelection("add " + t.Label);
                        var hash = t.Hash;
                        ApplyToSelection(x => MaterialEditing.AddParam(x.Shader, hash, null, true));
                        Status = "Added texture slot " + t.Label;
                        ImGui.CloseCurrentPopup();
                    }
                }

                DrawMoreSamplers_V27(m, addParamFilter);
            }
            ImGui.EndChild();
            ImGui.EndPopup();
        }

        private string presetFilter = "";
        private Vector2 presetAnchor;

        private void DrawPresetPicker(MaterialRef m)
        {
            if (presetAnchor.X > 0)
            {
                var io2 = ImGui.GetIO();
                var pos = presetAnchor;
                pos.X = Math.Min(pos.X, Math.Max(io2.DisplaySize.X - 440, 0));
                pos.Y = Math.Min(pos.Y, Math.Max(io2.DisplaySize.Y - 440, 0));
                ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
            }
            ImGui.SetNextWindowSize(new Vector2(430, 430), ImGuiCond.Always);
            if (!ImGui.BeginPopup("presetpick")) return;

            ImGui.TextDisabled($"Current: {m.Name}");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
            ImGui.InputTextWithHint("##presetf", "type to search all shaders...", ref presetFilter, 64);

            var rows = new List<(string Name, string Cat, bool Exact)>();
            foreach (var (cat, names) in ShaderPresets.Categories)
            {
                foreach (var name in names)
                {
                    if (presetFilter.Length > 0 &&
                        name.IndexOf(presetFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    rows.Add((name, cat, ShaderPresets.Template(name).Harvested));
                }
            }

            ImGui.TextDisabled($"{rows.Count} shader(s)  -  green = the editor has seen a real material using it");

            if (ImGui.BeginChild("presetlist", new Vector2(0, -1)))
            {
                foreach (var (name, cat, exact) in rows)
                {
                    bool current = string.Equals(name, m.Name, StringComparison.OrdinalIgnoreCase);
                    if (exact) ImGui.PushStyleColor(ImGuiCol.Text, UiTheme.Ok);
                    bool clicked = ImGui.Selectable($"{name}##ps{name}", current);
                    if (exact) ImGui.PopStyleColor();

                    ImGui.SameLine();
                    ImGui.TextDisabled(cat);

                    if (ImGui.IsItemHovered() || ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenOverlapped))
                    {
                        var t = ShaderPresets.Template(name);
                        var warn = ShaderPresets.CompatibilityWarning(name,
                            m.Meshes.Count > 0 ? m.Meshes[0].Geometry : null);
                        ImGui.SetTooltip($"{t.Sps}\nbucket {MaterialDefs.BucketName(t.Bucket)}, " +
                            $"{t.Params.Count} parameters, {t.Params.Count(p => p.IsTexture)} textures\n" +
                            (exact ? "exact - copied from a real material" : "derived from the preset name") +
                            (warn != null ? "\n\nWARNING: " + warn : ""));
                    }

                    if (clicked)
                    {
                        PushUndoSelection("change preset");
                        var preset = name;
                        ApplyToSelection(x => ShaderPresets.Apply(x.Shader, preset));
                        listedGeomVersion = -1;
                        Status = "Shader = " + preset;
                        ImGui.CloseCurrentPopup();
                    }
                }
            }
            ImGui.EndChild();
            ImGui.EndPopup();
        }
    }
}

