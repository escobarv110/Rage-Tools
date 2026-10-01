using System;
using System.Linq;
using System.Numerics;
using CodeWalker.GameFiles;
using ImGuiNET;

namespace RageLightEditor.Editor
{
    public partial class LightPanel
    {
        private int extSel_V62 = -1;
        private int extAddType_V62;
        private string extAddName_V62 = "";
        private string extFilter_V62 = "";

        private static Vector4 ExtTypeColour_V69(MetaWrapper w)
        {
            var c = ExtensionHelpers.MarkerColour(w);
            return new Vector4(c.X, c.Y, c.Z, 1.0f);
        }

        private static Vector4 ExtTypeColourByLabel_V69(string wrapper)
        {
            switch (wrapper)
            {
                case "MCExtensionDefParticleEffect": return new Vector4(1.0f, 0.55f, 0.15f, 1f);
                case "MCExtensionDefSpawnPoint":
                case "MCExtensionDefSpawnPointOverride": return new Vector4(0.3f, 1.0f, 0.4f, 1f);
                case "MCExtensionDefAudioEmitter":
                case "MCExtensionDefAudioCollisionSettings": return new Vector4(0.3f, 0.75f, 1.0f, 1f);
                case "MCExtensionDefDoor": return new Vector4(0.9f, 0.85f, 0.3f, 1f);
                case "MCExtensionDefLadder": return new Vector4(0.8f, 0.5f, 1.0f, 1f);
                case "MCExtensionDefBuoyancy": return new Vector4(0.2f, 0.9f, 0.9f, 1f);
                case "MCExtensionDefProcObject": return new Vector4(0.6f, 0.9f, 0.3f, 1f);
                case "MCExtensionDefExplosionEffect": return new Vector4(1.0f, 0.3f, 0.2f, 1f);
                case "MCExtensionDefExpression": return new Vector4(0.9f, 0.9f, 0.9f, 1f);
                case "MCExtensionDefWindDisturbance": return new Vector4(0.7f, 0.8f, 1.0f, 1f);
                case "MCExtensionDefLightShaft": return new Vector4(1.0f, 0.93f, 0.55f, 1f);
                case "MCExtensionDefLightEffect": return new Vector4(1.0f, 0.85f, 0.35f, 1f);
                default: return new Vector4(0.82f, 0.82f, 0.86f, 1f);
            }
        }

        private void DrawArchetypeExtensions_V62(Archetype arch)
        {
            if (arch == null) return;
            var list = ArchetypeExtensions_V62.Get(arch);

            ImGui.Spacing();
            ImGui.TextDisabled("EXTENSIONS");
            ImGui.SameLine();
            ImGui.TextDisabled("(" + list.Length + ")");

            if (list.Length > 0)
            {
                float h = Math.Min(list.Length * ImGui.GetTextLineHeightWithSpacing() + 10, 150);
                if (ImGui.BeginChild("##extlist", new Vector2(0, h), ImGuiChildFlags.Borders))
                {
                    for (int i = 0; i < list.Length; i++)
                    {
                        var w = list[i];
                        var col = ExtTypeColour_V69(w);
                        var nm = ArchetypeExtensions_V62.NameOf(w);
                        ImGui.PushStyleColor(ImGuiCol.Text, col);
                        bool hit = ImGui.Selectable("##extrow" + i, extSel_V62 == i);
                        ImGui.PopStyleColor();
                        if (hit) extSel_V62 = i;
                        ImGui.SameLine(6);
                        ImGui.TextColored(col, ArchetypeExtensions_V62.TypeLabel(w));
                        if (!string.IsNullOrEmpty(nm))
                        {
                            ImGui.SameLine();
                            ImGui.TextDisabled(nm);
                        }
                    }
                }
                ImGui.EndChild();
            }
            else ImGui.TextDisabled("None yet - pick a type below and press Add.");

            ImGui.Spacing();
            var types = ArchetypeExtensions_V62.Types;
            if (extAddType_V62 < 0 || extAddType_V62 >= types.Count) extAddType_V62 = 0;
            var pick = types[extAddType_V62];
            var pickCol = ExtTypeColourByLabel_V69(pick.Wrapper);

            ImGui.TextDisabled("ADD");
            ImGui.SetNextItemWidth(-1);
            ImGui.PushStyleColor(ImGuiCol.Text, pickCol);
            bool open = ImGui.BeginCombo("##exttype", pick.Label);
            ImGui.PopStyleColor();
            if (open)
            {
                for (int i = 0; i < types.Count; i++)
                {
                    var c = ExtTypeColourByLabel_V69(types[i].Wrapper);
                    ImGui.PushStyleColor(ImGuiCol.Text, c);
                    if (ImGui.Selectable(types[i].Label + "##et" + i, i == extAddType_V62)) extAddType_V62 = i;
                    ImGui.PopStyleColor();
                    if (ImGui.IsItemHovered() && !string.IsNullOrEmpty(types[i].Hint))
                        ImGui.SetTooltip(types[i].Hint);
                }
                ImGui.EndCombo();
            }
            if (!string.IsNullOrEmpty(pick.Hint)) ImGui.TextWrapped(pick.Hint);

            float addW = 64;
            ImGui.SetNextItemWidth(-(addW + 8));
            ImGui.InputTextWithHint("##extname", "name (optional)", ref extAddName_V62, 64);
            ImGui.SameLine(0, 8);
            if (ImGui.Button("Add##ext", new Vector2(-1, 0)))
            {
                var nm = string.IsNullOrWhiteSpace(extAddName_V62)
                    ? (arch.Name ?? "ext") + "_" + pick.Wrapper.Substring("MCExtensionDef".Length).ToLowerInvariant()
                    : extAddName_V62.Trim();
                Ext.PushUndo_V70("Add " + pick.Label);
                var made = ArchetypeExtensions_V62.Create(pick, nm);
                ArchetypeExtensions_V62.Add(arch, made);
                extSel_V62 = ArchetypeExtensions_V62.Get(arch).Length - 1;
                extAddName_V62 = "";
                ArchEdited = true;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Adds it to this archetype. Save the .ytyp to keep it.");

            list = ArchetypeExtensions_V62.Get(arch);
            if (extSel_V62 < 0 || extSel_V62 >= list.Length) return;
            var sel = list[extSel_V62];
            var selCol = ExtTypeColour_V69(sel);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextColored(selCol, ArchetypeExtensions_V62.TypeLabel(sel).ToUpperInvariant());
            ImGui.SameLine();
            if (ImGui.SmallButton("Duplicate##ext"))
            {
                Ext.PushUndo_V70("Duplicate " + ArchetypeExtensions_V62.TypeLabel(sel));
                Ext.DuplicateExtension_V69(arch, extSel_V62);
                extSel_V62 = Ext.SelectedExtension;
                ArchEdited = true;
                return;
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("A copy on top of this one, ready to drag with the gizmo.\n" +
                                 "Shift and drag the gizmo does the same thing.");
            ImGui.SameLine();
            if (ImGui.SmallButton("Remove##ext"))
            {
                Ext.PushUndo_V70("Remove " + ArchetypeExtensions_V62.TypeLabel(sel));
                ArchetypeExtensions_V62.Remove(arch, extSel_V62);
                extSel_V62 = -1;
                ArchEdited = true;
                return;
            }
            DrawSnapControls_V68(sel, extSel_V62);
            DrawGizmoPointPicker_V69(sel);
            DrawShaftPresets_V69(sel);
            ImGui.Spacing();

            foreach (var f in ArchetypeExtensions_V62.Fields(sel))
            {
                var val = ArchetypeExtensions_V62.GetValue(sel, f);
                if (val == null) continue;
                ImGui.PushID(f.Prop.Name);
                DrawExtensionField_V62(arch, sel, f, val);
                ImGui.PopID();
            }
        }

        private void DrawGizmoPointPicker_V69(MetaWrapper sel)
        {
            if (!ExtensionMode || sel == null) return;
            var fields = ExtensionWorkspace_V68.MovableFields_V69(sel);
            if (fields.Count == 0) return;
            ImGui.TextDisabled("GIZMO MOVES");
            bool whole = string.IsNullOrEmpty(Ext.SelectedPointField);
            if (whole) ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.Accent);
            if (ImGui.SmallButton("the whole thing##gizall")) Ext.SelectedPointField = null;
            if (whole) ImGui.PopStyleColor();
            foreach (var f in fields)
            {
                ImGui.SameLine(0, 4);
                bool on = Ext.SelectedPointField == f.Prop.Name;
                if (on) ImGui.PushStyleColor(ImGuiCol.Button, UiTheme.Accent);
                if (ImGui.SmallButton(f.Label + "##giz" + f.Prop.Name)) Ext.SelectedPointField = f.Prop.Name;
                if (on) ImGui.PopStyleColor();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Or click a handle in the viewport. Click empty space to go back to the whole thing.");
        }

        private void DrawShaftPresets_V69(MetaWrapper sel)
        {
            if (sel == null) return;
            var presets = ExtensionPresets_V70.For(sel).ToArray();
            if (presets.Length == 0) return;
            ImGui.TextDisabled("PRESETS");
            var col = ExtTypeColourByLabel_V69(sel.GetType().Name);
            float w = (ImGui.GetContentRegionAvail().X - (presets.Length - 1) * 4) / presets.Length;
            for (int i = 0; i < presets.Length; i++)
            {
                if (i > 0) ImGui.SameLine(0, 4);
                ImGui.PushStyleColor(ImGuiCol.Text, col);
                bool hit = ImGui.Button(presets[i].Name + "##pre" + i,
                                        new Vector2(i == presets.Length - 1 ? -1 : w, 0));
                ImGui.PopStyleColor();
                if (hit)
                {
                    Ext.PushUndo_V70("Preset " + presets[i].Name);
                    ExtensionPresets_V70.Apply(sel, presets[i]);
                    ArchEdited = true;
                }
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(presets[i].What + "\n\nIt sets how this one BEHAVES - " +
                                     "the points you placed stay where they are.");
            }
        }

        private bool extFxPickOpen_V71;
        private string extFxFilter_V71 = "";

        private void DrawFxPicker_V71(MetaWrapper sel, ArchetypeExtensions_V62.Field f)
        {
            ImGui.SetNextWindowSize(new Vector2(320 * UiScale_V17.Scale, 380 * UiScale_V17.Scale));
            if (!ImGui.BeginPopup("##fxpop")) return;
            ImGui.TextDisabled("AMBIENT EFFECTS");
            ImGui.SetNextItemWidth(-1);
            if (ImGui.IsWindowAppearing()) ImGui.SetKeyboardFocusHere();
            ImGui.InputTextWithHint("##fxfind", "fire, smoke, water, leaves...", ref extFxFilter_V71, 64);
            var hits = ParticleEffectNames_V71.Find(extFxFilter_V71, 400).ToList();
            ImGui.TextDisabled(hits.Count + " of " + ParticleEffectNames_V71.Ambient.Length);
            if (ImGui.BeginChild("##fxlist", new Vector2(0, 0), ImGuiChildFlags.Borders))
            {
                var cur = ArchetypeExtensions_V62.GetValue(sel, f) as string ?? "";
                foreach (var n in hits)
                {
                    if (!ImGui.Selectable(n, string.Equals(n, cur, StringComparison.OrdinalIgnoreCase))) continue;
                    Ext.PushUndo_V70("Set effect " + n);
                    ArchetypeExtensions_V62.SetValue(sel, f, n);
                    ArchEdited = true;
                    ImGui.CloseCurrentPopup();
                }
            }
            ImGui.EndChild();
            ImGui.EndPopup();
        }

        private const float ExtLabelCol_V69 = 132.0f;

        private bool ExtFieldLabel_V69(ArchetypeExtensions_V62.Field f, out float widgetWidth)
        {
            float labelCol = ExtLabelCol_V69 * UiScale_V17.Scale;
            ImGui.TextUnformatted(f.Label);
            if (ImGui.CalcTextSize(f.Label).X + 10 < labelCol) ImGui.SameLine(labelCol);
            else ImGui.Indent(10);
            widgetWidth = Math.Max(60, ImGui.GetContentRegionAvail().X);
            bool sameLine = ImGui.CalcTextSize(f.Label).X + 10 < labelCol;
            if (!sameLine) ImGui.Unindent(10);
            return sameLine;
        }

        private void ExtFieldUndo_V70(ArchetypeExtensions_V62.Field f)
        {
            if (!ExtensionMode) return;
            if (ImGui.IsItemActivated()) Ext.PushUndo_V70("Edit " + f.Label);
        }

        private void DrawExtensionField_V62(Archetype arch, MetaWrapper sel,
                                            ArchetypeExtensions_V62.Field f, object val)
        {
            bool snappable = ExtensionMode && ExtensionWorkspace_V68.IsPointField_V68(f.Prop);
            var lim = ExtensionLimits_V72.For(sel, f.Prop.Name);
            ExtFieldLabel_V69(f, out float w);
            float snapW = snappable ? 52.0f * UiScale_V17.Scale + 6 : 0;
            ImGui.SetNextItemWidth(Math.Max(60, w - snapW));

            if (lim != null && lim.Kind == ExtFieldKind_V72.ColourArgb && val is uint packed)
            {
                ExtensionLimits_V72.UnpackArgb_V72(packed, out float cr, out float cg, out float cb, out float ca);
                var rgba = new Vector4(cr, cg, cb, ca);
                if (ImGui.ColorEdit4("##argb", ref rgba,
                        ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreviewHalf))
                {
                    ArchetypeExtensions_V62.SetValue(sel, f,
                        ExtensionLimits_V72.PackArgb_V72(rgba.X, rgba.Y, rgba.Z, rgba.W));
                    ArchEdited = true;
                }
                ExtFieldUndo_V70(f);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Alpha, red, green and blue, each 0 to 255 - stored as one number.");
                return;
            }

            if (lim != null && lim.Kind == ExtFieldKind_V72.TimeOfDay && val is float hours)
            {
                int minutes = ExtensionLimits_V72.HoursToMinutes_V72(hours);
                if (ImGui.DragInt("##tod", ref minutes, 1.0f, 0, 1439,
                                  ExtensionLimits_V72.TimeText_V72(hours)))
                {
                    ArchetypeExtensions_V62.SetValue(sel, f, ExtensionLimits_V72.MinutesToHours_V72(minutes));
                    ArchEdited = true;
                }
                ExtFieldUndo_V70(f);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Time of day, 00:00 to 23:59, a minute at a time. The game keeps it as hours - 06:30 is 6.5.");
                return;
            }

            switch (val)
            {
                case SharpDX.Vector3 v3:
                {
                    var v = new Vector3(v3.X, v3.Y, v3.Z);
                    if (ImGui.DragFloat3("##v3", ref v, 0.01f))
                    ExtFieldUndo_V70(f);
                    {
                        ArchetypeExtensions_V62.SetValue(sel, f, new SharpDX.Vector3(v.X, v.Y, v.Z));
                        ArchEdited = true;
                    }
                    if (snappable) DrawSnapFieldButton_V68(f, extSel_V62);
                    break;
                }
                case float fv:
                {
                    float x = fv;
                    float step = lim?.Step ?? 0.01f;
                    float mn = lim?.Min ?? 0.0f, mx = lim?.Max ?? 0.0f;
                    if (ImGui.DragFloat("##f", ref x, step, mn, mx))
                    {
                        if (lim != null) x = Math.Min(mx, Math.Max(mn, x));
                        ArchetypeExtensions_V62.SetValue(sel, f, x);
                        ArchEdited = true;
                    }
                    ExtFieldUndo_V70(f);
                    if (lim != null && ImGui.IsItemHovered())
                        ImGui.SetTooltip($"{lim.Min:0.##} to {lim.Max:0.##}" +
                                         (string.IsNullOrEmpty(lim.Note) ? "" : " " + lim.Note) +
                                         $", steps of {lim.Step:0.#####}");
                    break;
                }
                case MetaHash mh:
                {
                    var str = mh.ToString();
                    if (ImGui.InputText("##mh", ref str, 96))
                    {
                        JenkIndex.Ensure(str);
                        ArchetypeExtensions_V62.SetValue(sel, f, new MetaHash(JenkHash.GenHash(str)));
                        ArchEdited = true;
                    }
                    ExtFieldUndo_V70(f);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Stored as a hash of this text, which is how the game names things.");
                    break;
                }
                case bool b:
                {
                    bool x = b;
                    if (ImGui.Checkbox("##b", ref x))
                    { ArchetypeExtensions_V62.SetValue(sel, f, x); ArchEdited = true; }
                    ExtFieldUndo_V70(f);
                    break;
                }
                case byte by:
                {
                    int x = by;
                    int bmn = lim != null ? (int)lim.Min : 0, bmx = lim != null ? (int)lim.Max : 255;
                    if (ImGui.DragInt("##by", ref x, 1, bmn, bmx))
                    { ArchetypeExtensions_V62.SetValue(sel, f, (byte)Math.Clamp(x, bmn, bmx)); ArchEdited = true; }
                    ExtFieldUndo_V70(f);
                    if (lim != null && ImGui.IsItemHovered()) ImGui.SetTooltip(bmn + " to " + bmx);
                    break;
                }
                case ushort us:
                {
                    int x = us;
                    if (ImGui.DragInt("##us", ref x, 1, 0, 65535))
                    { ArchetypeExtensions_V62.SetValue(sel, f, (ushort)Math.Clamp(x, 0, 65535)); ArchEdited = true; }
                    ExtFieldUndo_V70(f);
                    break;
                }
                case int iv:
                {
                    int x = iv;
                    if (ImGui.DragInt("##i", ref x))
                    { ArchetypeExtensions_V62.SetValue(sel, f, x); ArchEdited = true; }
                    ExtFieldUndo_V70(f);
                    break;
                }
                case uint uv:
                {
                    int x = unchecked((int)uv);
                    bool bounded = lim != null && lim.Kind == ExtFieldKind_V72.Number;
                    int umn = bounded ? (int)lim.Min : 0, umx = bounded ? (int)lim.Max : 0;
                    if (bounded ? ImGui.DragInt("##u", ref x, 1, umn, umx) : ImGui.DragInt("##u", ref x))
                    {
                        if (bounded) x = Math.Clamp(x, umn, umx);
                        ArchetypeExtensions_V62.SetValue(sel, f, unchecked((uint)x));
                        ArchEdited = true;
                    }
                    ExtFieldUndo_V70(f);
                    if (bounded && ImGui.IsItemHovered())
                        ImGui.SetTooltip(umn + " to " + umx + (string.IsNullOrEmpty(lim.Note) ? "" : " " + lim.Note));
                    break;
                }
                case string sval:
                {
                    var text = sval ?? "";
                    bool isFx = f.Prop.Name == "fxName";
                    if (isFx) ImGui.SetNextItemWidth(Math.Max(60, w - 74 * UiScale_V17.Scale));
                    if (ImGui.InputText("##str", ref text, 128))
                    { ArchetypeExtensions_V62.SetValue(sel, f, text); ArchEdited = true; }
                    ExtFieldUndo_V70(f);
                    if (isFx)
                    {
                        ImGui.SameLine(0, 4);
                        if (ImGui.SmallButton("pick##fx")) { extFxFilter_V71 = ""; ImGui.OpenPopup("##fxpop"); }
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("The game's ambient effects, by name.");
                        DrawFxPicker_V71(sel, f);
                    }
                    break;
                }
                default:
                {
                    var t = val.GetType();
                    if (t.IsEnum)
                    {
                        var names = Enum.GetNames(t);
                        int cur = Math.Max(0, Array.IndexOf(names, val.ToString()));
                        if (ImGui.Combo("##e", ref cur, names, names.Length))
                        ExtFieldUndo_V70(f);
                        {
                            ArchetypeExtensions_V62.SetValue(sel, f, Enum.Parse(t, names[cur]));
                            ArchEdited = true;
                        }
                    }
                    else
                    {
                        ImGui.TextDisabled(val.ToString());
                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip("This one is not editable here yet - " + t.Name);
                    }
                    break;
                }
            }
        }
    }
}
