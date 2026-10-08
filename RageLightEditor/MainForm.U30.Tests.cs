using System;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        partial void SeqTest_U30(Action<string, bool, string> check)
        {
            CollisionModeTest_U30(check);
            InteriorProjectPropTest_U30(check);
            MloDupArchetypeTest_U31(check);
            CinematicEverywhereTest_U31(check);
        }

        private void CinematicEverywhereTest_U31(Action<string, bool, string> check)
        {
            var wasSpace = panel.Workspace;
            try
            {
                var missing = new System.Collections.Generic.List<string>();
                foreach (LightPanel.Space s in Enum.GetValues(typeof(LightPanel.Space)))
                {
                    panel.SwitchWorkspace(s);
                    if (Array.IndexOf(panel.ShadingChoices_U28().modes, 7) < 0) missing.Add(s.ToString());
                }
                check("cinematic: offered in the shading list on every page", missing.Count == 0, missing.Count == 0 ? "all pages" : "missing on " + string.Join(", ", missing));
                foreach (var s in new[] { LightPanel.Space.Light, LightPanel.Space.Material, LightPanel.Space.World })
                {
                    panel.SwitchWorkspace(s);
                    panel.RenderMode = 7;
                    panel.SwitchWorkspace(LightPanel.Space.Cinematic);
                    panel.SwitchWorkspace(LightPanel.Space.Particles);
                    panel.SwitchWorkspace(s);
                    check($"cinematic: {s} keeps Cinematic after visiting other pages", panel.RenderMode == 7, "render mode " + panel.RenderMode);
                    panel.RenderMode = 0;
                }
            }
            catch (Exception ex) { check("cinematic: the test ran", false, ex.ToString()); }
            finally { panel.SwitchWorkspace(wasSpace); }
        }

        private void MloDupArchetypeTest_U31(Action<string, bool, string> check)
        {
            var projWas = ProjWin?.Project;
            var findWas = ProjWin?.FindMloInstance;
            try
            {
                var ytyp = new YtypFile { Name = "rle_u31_int.ytyp" };
                var mlo = new MloArchetype();
                var mdef = new CMloArchetypeDef();
                uint intHash = JenkHash.GenHash("rle_u31_int");
                mdef._BaseArchetypeDef.name = intHash; mdef._BaseArchetypeDef.assetName = intHash;
                mdef._BaseArchetypeDef.assetType = rage__fwArchetypeDef__eAssetType.ASSET_TYPE_ASSETLESS;
                mdef._BaseArchetypeDef.bbMin = new Vector3(-4, -4, -1); mdef._BaseArchetypeDef.bbMax = new Vector3(4, 4, 4);
                mlo.Init(ytyp, ref mdef);
                ytyp.AllArchetypes = new Archetype[] { mlo };
                MloEditor.AddRoom(mlo, "limbo");
                MloEditor.AddRoom(mlo, "shop");
                var shell = new YmapEntityDef();
                var sdef = new CEntityDef { archetypeName = intHash, position = new Vector3(50, 60, 20), rotation = new Vector4(0, 0, 0, 1), scaleXY = 1, scaleZ = 1, lodDist = 200 };
                shell.CEntityDef = sdef; shell.Position = sdef.position; shell.Orientation = Quaternion.Identity; shell.Scale = Vector3.One; shell.IsMlo = true;
                shell.SetArchetype(mlo);
                var props = ArchetypeBuilder.BuildYtyp("rle_u31_props", new[]
                {
                    new ArchetypeDef { Name = "rle_u31_bench", TextureDict = "rle_u31_bench", BbMin = new Vector3(-0.5f), BbMax = new Vector3(0.5f), BsRadius = 0.9f },
                    new ArchetypeDef { Name = "rle_u31_chair", TextureDict = "rle_u31_chair", BbMin = new Vector3(-0.5f), BbMax = new Vector3(0.5f), BsRadius = 0.9f },
                });
                ProjWin.Project = new CwProject { Name = "u31" };
                ProjWin.Project.AddYtypFile(props);
                ProjWin.FindMloInstance = m => ReferenceEquals(m, mlo) ? shell.MloInstance : null;

                uint bench = JenkHash.GenHash("rle_u31_bench"), chair = JenkHash.GenHash("rle_u31_chair");
                var t = new MloTarget_U21 { Owner = shell, Room = 1, Portal = -1, EntSet = -1, Why = "test" };
                var src = AddMloEntity_U21(t, bench, null, new Vector3(51, 61, 21));
                int before = mlo.entities?.Length ?? 0;
                WorldEdit.Select(src);
                WorldDuplicateSelected();
                var dup = WorldEdit.Selected;
                var dupDef = shell.MloInstance.TryGetArchetypeEntity(dup);
                check("interior duplicate: Duplicate copies a prop inside a room",
                      dup != null && !ReferenceEquals(dup, src) && dup.MloParent == shell && (mlo.entities?.Length ?? 0) == before + 1 && mlo.GetEntityRoom(dupDef)?.Index == 1,
                      $"{before} -> {mlo.entities?.Length ?? 0} entities, room {mlo.GetEntityRoom(dupDef)?.Index}, {WorldEdit.LastStatus}");

                ProjWin.SetEntityArchetype_U31(dup, chair);
                var srcDef = shell.MloInstance.TryGetArchetypeEntity(src);
                check("interior duplicate: changing the copy's archetype changes the drawn prop",
                      dup.Archetype?.Name == "rle_u31_chair", dup.Archetype?.Name ?? "no archetype");
                check("interior duplicate: ...and the interior's own entry, so it is saved",
                      dupDef?._Data.archetypeName.Hash == chair && srcDef?._Data.archetypeName.Hash == bench && src.Archetype?.Name == "rle_u31_bench",
                      $"copy {dupDef?._Data.archetypeName}, original {srcDef?._Data.archetypeName}");

                var back = new YtypFile();
                back.Load(ytyp.Save());
                var saved = (back.AllArchetypes?[0] as MloArchetype)?.entities;
                check("interior duplicate: the saved .ytyp has the original AND the changed copy",
                      saved != null && saved.Length == before + 1 && saved[before - 1]._Data.archetypeName.Hash == bench && saved[before]._Data.archetypeName.Hash == chair,
                      saved == null ? "no entities" : string.Join(", ", Array.ConvertAll(saved, s => s._Data.archetypeName.ToString())));

                var live = ProjWin.SetMloDefArchetype_U31(dupDef, bench);
                check("interior duplicate: changing it from the interior's entity page changes the drawn prop too",
                      ReferenceEquals(live, dup) && dup.Archetype?.Name == "rle_u31_bench" && dup._CEntityDef.archetypeName.Hash == bench,
                      dup.Archetype?.Name ?? "no archetype");

                WorldEdit.Select(dup);
                TryWorldUndo();
                check("interior duplicate: undo removes the copy",
                      (mlo.entities?.Length ?? 0) == before && shell.MloInstance.TryGetArchetypeEntity(dup) == null,
                      $"{mlo.entities?.Length ?? 0} entities");
            }
            catch (Exception ex) { check("interior duplicate: the test ran", false, ex.ToString()); }
            finally { if (ProjWin != null) { ProjWin.Project = projWas; ProjWin.FindMloInstance = findWas; } WorldEdit.Deselect(); }
        }

        private void InteriorProjectPropTest_U30(Action<string, bool, string> check)
        {
            var projWas = ProjWin?.Project;
            try
            {
                var ytyp = new YtypFile { Name = "rle_u30_int.ytyp" };
                var mlo = new MloArchetype();
                var mdef = new CMloArchetypeDef();
                uint intHash = JenkHash.GenHash("rle_u30_int");
                mdef._BaseArchetypeDef.name = intHash; mdef._BaseArchetypeDef.assetName = intHash;
                mdef._BaseArchetypeDef.assetType = rage__fwArchetypeDef__eAssetType.ASSET_TYPE_ASSETLESS;
                mdef._BaseArchetypeDef.bbMin = new Vector3(-4, -4, -1); mdef._BaseArchetypeDef.bbMax = new Vector3(4, 4, 4);
                mlo.Init(ytyp, ref mdef);
                MloEditor.AddRoom(mlo, "limbo");
                MloEditor.AddRoom(mlo, "shop");
                var shell = new YmapEntityDef();
                var sdef = new CEntityDef { archetypeName = intHash, position = new Vector3(50, 60, 20), rotation = new Vector4(0, 0, 0, 1), scaleXY = 1, scaleZ = 1, lodDist = 200 };
                shell.CEntityDef = sdef; shell.Position = sdef.position; shell.Orientation = Quaternion.Identity; shell.Scale = Vector3.One; shell.IsMlo = true;
                shell.SetArchetype(mlo);

                var custom = ArchetypeBuilder.BuildYtyp("rle_u30_props", new[] { new ArchetypeDef { Name = "rle_u30_my_prop", TextureDict = "rle_u30_my_prop", BbMin = new Vector3(-0.5f), BbMax = new Vector3(0.5f), BsRadius = 0.9f } });
                ProjWin.Project = new CwProject { Name = "u30" };
                ProjWin.Project.AddYtypFile(custom);
                var t = new MloTarget_U21 { Owner = shell, Room = 1, Portal = -1, EntSet = -1, Why = "test" };
                var e = AddMloEntity_U21(t, JenkHash.GenHash("rle_u30_my_prop"), null, new Vector3(51, 61, 21));
                check("interior add: a prop from a project .ytyp resolves, so it can be drawn",
                      e?.Archetype != null && e.Archetype.Name == "rle_u30_my_prop" && LastMloAddWarning_U30 == null, e?.Archetype?.Name ?? "no archetype");
                var lost = AddMloEntity_U21(t, JenkHash.GenHash("rle_u30_nowhere"), null, new Vector3(52, 61, 21));
                check("interior add: a prop that exists nowhere says so instead of vanishing quietly",
                      LastMloAddWarning_U30 != null && LastMloAddWarning_U30.Contains("can't be drawn"), LastMloAddWarning_U30 ?? "no warning");
            }
            catch (Exception ex) { check("interior add: the test ran", false, ex.ToString()); }
            finally { if (ProjWin != null) ProjWin.Project = projWas; WorldEdit.Deselect(); }
        }

        private void CollisionModeTest_U30(Action<string, bool, string> check)
        {
            var wasSpace = panel.Workspace;
            int wasMode = panel.SelectionMode;
            bool wasShow = panel.WorldShowCollision;
            try
            {
                panel.SwitchWorkspace(LightPanel.Space.World);
                panel.WorldShowCollision = false;
                panel.SelectionMode = LightPanel.IndexOfMode(WorldSelectionMode.Entity);
                void Frames(int n) { for (int i = 0; i < n; i++) { OnWorldTick_Selection(); panel.ShellToolbarLogic_U27(); } }
                Frames(3);
                panel.SelectionMode = LightPanel.IndexOfMode(WorldSelectionMode.Collision);
                Frames(3);
                check("collision picks: choosing Collision shows the collision", panel.WorldShowCollision, "");
                panel.SelectionMode = LightPanel.IndexOfMode(WorldSelectionMode.Entity);
                Frames(4);
                check("collision picks: going back to Entity hides it again",
                      !panel.WorldShowCollision && panel.SelectionModeEnum == WorldSelectionMode.Entity,
                      $"overlay {panel.WorldShowCollision}, picks {panel.SelectionModeName}");
                panel.SelectionMode = LightPanel.IndexOfMode(WorldSelectionMode.Collision);
                Frames(3);
                panel.SelectionMode = LightPanel.IndexOfMode(WorldSelectionMode.MloInstance);
                Frames(4);
                check("collision picks: ...and from Collision to any other mode too",
                      !panel.WorldShowCollision && panel.SelectionModeEnum == WorldSelectionMode.MloInstance,
                      $"overlay {panel.WorldShowCollision}, picks {panel.SelectionModeName}");
                panel.WorldShowCollision = true;
                Frames(3);
                check("collision picks: the Options checkbox switches the picks to Collision", panel.SelectionModeEnum == WorldSelectionMode.Collision, panel.SelectionModeName);
                panel.SelectionMode = LightPanel.IndexOfMode(WorldSelectionMode.Entity);
                Frames(4);
                check("collision picks: ...and leaving Collision hides it either way",
                      !panel.WorldShowCollision && panel.SelectionModeEnum == WorldSelectionMode.Entity,
                      $"overlay {panel.WorldShowCollision}, picks {panel.SelectionModeName}");
            }
            catch (Exception ex) { check("collision picks: the test ran", false, ex.ToString()); }
            finally
            {
                panel.SelectionMode = wasMode;
                panel.WorldShowCollision = wasShow;
                panel.SwitchWorkspace(wasSpace);
            }
        }
    }
}
