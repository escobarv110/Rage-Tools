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
