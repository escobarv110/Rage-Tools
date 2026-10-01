using System;
using System.Linq;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private void SeqTest_MloAdd_U21(Action<string, bool, string> check)
        {
            var projWas = ProjWin?.Project;
            YtypFile ytyp = null;
            try
            {
                ytyp = new YtypFile { Name = "rle_u21_int.ytyp" };
                var mlo = new MloArchetype();
                var mdef = new CMloArchetypeDef();
                uint intHash = JenkHash.GenHash("rle_u21_int");
                mdef._BaseArchetypeDef.name = intHash; mdef._BaseArchetypeDef.assetName = intHash;
                mdef._BaseArchetypeDef.assetType = rage__fwArchetypeDef__eAssetType.ASSET_TYPE_ASSETLESS;
                mdef._BaseArchetypeDef.bbMin = new Vector3(-4, -4, -1); mdef._BaseArchetypeDef.bbMax = new Vector3(4, 4, 4);
                mlo.Init(ytyp, ref mdef);
                MloEditor.AddRoom(mlo, "limbo");
                var room = MloEditor.AddRoom(mlo, "kitchen");
                room._Data.bbMin = new Vector3(-4, -4, -1); room._Data.bbMax = new Vector3(4, 4, 4);

                var shell = new YmapEntityDef();
                var sdef = new CEntityDef { archetypeName = intHash, position = new Vector3(100, 200, 30), rotation = new Vector4(0, 0, 0, 1), scaleXY = 1, scaleZ = 1, lodDist = 200, lodLevel = rage__eLodType.LODTYPES_DEPTH_ORPHANHD };
                shell.CEntityDef = sdef;
                shell.Position = sdef.position;
                shell.Orientation = Quaternion.RotationAxis(Vector3.UnitZ, MathUtil.PiOverTwo);
                shell.Scale = Vector3.One;
                shell.IsMlo = true;
                shell.SetArchetype(mlo);
                check("u21 interior add: the test interior has a live instance", shell.MloInstance != null, "");

                var t = new MloTarget_U21 { Owner = shell, Room = 1, Portal = -1, EntSet = -1, Why = "test" };
                var world = new Vector3(101, 202, 31);
                var e = AddMloEntity_U21(t, JenkHash.GenHash("prop_chair_01a"), null, world);
                check("u21 interior add: the prop goes into the interior, not a ymap",
                      e != null && e.Ymap == null && ReferenceEquals(e.MloParent, shell) && mlo.entities?.Length == 1, e == null ? "null" : $"{mlo.entities?.Length} entities");
                check("u21 interior add: ...attached to the chosen room",
                      room.AttachedObjects != null && room.AttachedObjects.Contains(0u), string.Join(",", room.AttachedObjects ?? Array.Empty<uint>()));
                var local = mlo.entities[0]._Data.position;
                var expectLocal = WorldToInterior_U21(shell, world);
                check("u21 interior add: stored in interior space, turned with the interior",
                      Vector3.Distance(local, expectLocal) < 1e-3f && Math.Abs(local.X - 2f) < 1e-3f && Math.Abs(local.Y + 1f) < 1e-3f,
                      local.ToString());
                check("u21 interior add: drawn where it was placed", Vector3.Distance(e.Position, world) < 1e-3f, e.Position.ToString());
                check("u21 interior add: the interior's .ytyp is marked changed", ytyp.HasChanged, "");

                WorldHistory.Undo();
                check("u21 interior add: undo takes it back out", (mlo.entities?.Length ?? 0) == 0 && !(room.AttachedObjects?.Contains(0u) ?? false), $"{mlo.entities?.Length ?? 0}");
                WorldHistory.Redo();
                check("u21 interior add: redo puts it back in the same room", mlo.entities?.Length == 1 && room.AttachedObjects != null && room.AttachedObjects.Contains(0u), "");

                var none = new MloTarget_U21 { Room = -1, Portal = -1, EntSet = -1 };
                check("u21 interior add: no interior target means the ymap path is used", !none.Valid, "");
                check("u21 interior add: the target reads as a place", MloTargetText_U21(t).Contains("kitchen"), MloTargetText_U21(t));
            }
            catch (Exception ex) { check("u21 interior add: no exception", false, ex.ToString()); }
            finally
            {
                try
                {
                    if (ProjWin != null)
                    {
                        if (projWas == null) ProjWin.Project = null;
                        else if (ytyp != null && projWas.YtypFiles.Contains(ytyp)) projWas.RemoveYtypFile(ytyp);
                    }
                    WorldEdit.Deselect();
                }
                catch { }
            }
        }

        private void SeqTest_FurFallback_U21(Action<string, bool, string> check)
        {
            try
            {
                int strands = 0, n = ModelRenderer.FallbackFurSize_U21;
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) if (ModelRenderer.FallbackStrandTop_U21(x, y) > 0) strands++;
                float cover = strands / (float)(n * n);
                check("u21 fur preview: the stand-in grass covers part of the ground, not all of it", cover > 0.3f && cover < 0.55f, cover.ToString("0.00"));
                int l0 = 0, l7 = 0;
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { if (ModelRenderer.FallbackComboValue_U21(x, y, 0) > 0) l0++; if (ModelRenderer.FallbackComboValue_U21(x, y, 7) > 0) l7++; }
                check("u21 fur preview: higher shells keep fewer blades, so the grass tapers", l7 < l0 && l7 > 0, $"{l0} -> {l7}");
                check("u21 fur preview: the pattern tiles", ModelRenderer.FallbackStrandTop_U21(3, 5) == ModelRenderer.FallbackStrandTop_U21(3 + n, 5 - n), "");
                var t = ShaderPresets.Template("grass_fur");
                var tex = ShaderPresets.GameFurTexture_U21("grass_fur", (uint)ShaderParamNames.ComboHeightSamplerFur45);
                check("u21 fur: switching a material to grass_fur points it at the game's own fur height maps",
                      tex != null && tex.Name == "fur_grass_rgba4_2" && ShaderPresets.GameFurTexture_U21("default", (uint)ShaderParamNames.ComboHeightSamplerFur01) == null, tex?.Name ?? "null");
                var sh = new ShaderFX { ParametersList = new ShaderParametersBlock { Parameters = Array.Empty<ShaderParameter>(), Hashes = Array.Empty<MetaName>() } };
                ShaderPresets.Apply(sh, "grass_fur");
                var combo = MaterialEditing.GetTexture(sh, (uint)ShaderParamNames.ComboHeightSamplerFur01);
                check("u21 fur: a fresh grass_fur material has its height maps filled in", combo?.Name == "fur_grass_rgba4_0" && t != null, combo?.Name ?? "null");
            }
            catch (Exception ex) { check("u21 fur fallback: no exception", false, ex.ToString()); }
        }
    }
}
