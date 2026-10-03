using System;
using System.IO;
using System.Linq;
using RageLightEditor.Editor;
using RageLightEditor.Rendering;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private Scene lightScene;
        private Scene mloScene;

        private Scene CurrentScene => panel == null ? lightScene : SceneFor_L3(panel.Workspace);
        public Scene LightScene => lightScene;
        public Scene MloScene => mloScene;

        private void CreateMloScene_L3()
        {
            mloScene = new Scene(modelRenderer, null);
            if (modelRenderer != null && lightScene != null) modelRenderer.ImportedTextures = lightScene.ImportedTextures;
        }

        private void DisposeMloScene_L3()
        {
            mloScene?.Dispose();
            DisposeSectionScenes_S1();
            lightScene?.Dispose();
        }

        private void BindActiveScene_L3(Scene target)
        {
            if (target == null) return;
            if (gizmo != null) gizmo.Scene = target;
            BindMatPanel_R2(target);
            if (modelRenderer != null) modelRenderer.ImportedTextures = target.ImportedTextures;
            hoverProp = null;
            shadowGeomVersion = -1; sunMapDirty = true; shadowPickDirty = true; lastShadowSelection = -2;
            sceneOccludersVersion = -1;
        }

        private Scene SceneFor_L3(LightPanel.Space space) => SectionScene_S1(space) ?? (space == LightPanel.Space.Mlo && mloScene != null ? mloScene : lightScene);

        private void UpdateMloLightGizmoEnabled_L3(MloCreatorPanel ui)
        {
            if (gizmo == null || ui == null) return;
            gizmo.Enabled = mloGizmoWasEnabled && (ui.LightEditingActive || gizmo.Dragging);
        }

        private void ClearImporterCacheIfUnused_L3()
        {
            var active = scene;
            foreach (var other in AllScenes_S1())
            {
                if (other == null || ReferenceEquals(other, active)) continue;
                if (other.MloModel != null || other.Files.Any(f => f != null && f.FromMlo)) return;
            }
            mloImporter?.ClearCache();
        }

        partial void MloSceneTest_L3(Action<string, bool, string> check)
        {
            try
            {
                check("mloscene: two scenes exist", lightScene != null && mloScene != null && !ReferenceEquals(lightScene, mloScene), $"light {lightScene != null} mlo {mloScene != null}");
                panel.SwitchWorkspace(LightPanel.Space.Light);
                check("mloscene: Lights draws the light scene", ReferenceEquals(scene, lightScene) && ReferenceEquals(gizmo.Scene, lightScene) && ReferenceEquals(panel.ActiveScene, lightScene), $"scene {(ReferenceEquals(scene, lightScene) ? "light" : "mlo")}");
                int lightFiles = lightScene.Files.Count, lightLights = lightScene.Lights.Count;
                bool lightHadMlo = lightScene.MloModel != null;

                panel.SwitchWorkspace(LightPanel.Space.Mlo);
                check("mloscene: MLO draws its own scene", ReferenceEquals(scene, mloScene) && ReferenceEquals(gizmo.Scene, mloScene) && ReferenceEquals(panel.ActiveScene, mloScene), $"scene {(ReferenceEquals(scene, mloScene) ? "mlo" : "light")}");
                string dir = Path.Combine(Path.GetTempPath(), "rle_mlocreator");
                Directory.CreateDirectory(dir);
                string ydr = Path.Combine(dir, "light_test_scene.ydr");
                if (!File.Exists(ydr)) TestSceneGenerator.Run(ydr);
                string ydr3 = Path.Combine(dir, "prop_rle_mloscene.ydr");
                File.Copy(ydr, ydr3, true);
                int mloFiles = mloScene.Files.Count, mloLights = mloScene.Lights.Count;
                LoadFile(ydr3);
                check("mloscene: the load landed in the MLO scene", mloScene.Files.Count == mloFiles + 1 && mloScene.Files.Any(f => f.Path == ydr3), $"{mloFiles} -> {mloScene.Files.Count} files");
                check("mloscene: its lights joined the MLO scene", mloScene.Lights.Count > mloLights, $"{mloLights} -> {mloScene.Lights.Count} lights");
                check("mloscene: the light scene did not move", lightScene.Files.Count == lightFiles && lightScene.Lights.Count == lightLights && (lightScene.MloModel != null) == lightHadMlo && !lightScene.Files.Any(f => f.Path == ydr3),
                      $"light scene {lightScene.Files.Count} files / {lightScene.Lights.Count} lights (was {lightFiles} / {lightLights})");
                var ui = Creator;
                if (ui.Session != null)
                {
                    var f3 = mloScene.Files.First(f => f.Path == ydr3);
                    ui.Session.AddEntityFromFile(f3); ui.Session.AutoAssignRooms();
                    check("mloscene: the prop is an entity of the interior", ui.Session.Entities.Any(e => e.SourceFile == f3), $"{ui.Session.Entities.Count} entities");
                }

                panel.SwitchWorkspace(LightPanel.Space.Light);
                check("mloscene: back in Lights the light scene shows", ReferenceEquals(scene, lightScene) && ReferenceEquals(gizmo.Scene, lightScene) && ReferenceEquals(panel.ActiveScene, lightScene) && lightScene.Files.Count == lightFiles && !scene.Files.Any(f => f.Path == ydr3),
                      $"{scene.Files.Count} files, ydr3 present {scene.Files.Any(f => f.Path == ydr3)}");
                check("mloscene: the light scene has no MLO scene light", !lightScene.Lights.Any(l => mloScene.Lights.Contains(l)), $"{lightScene.Lights.Count} vs {mloScene.Lights.Count}");
                if (lightScene.HasModel)
                {
                    int n0 = mloScene.Lights.Count;
                    var added = lightScene.AddLight(1);
                    check("mloscene: a light added in Lights stays in the light scene", added != null && lightScene.Lights.Contains(added) && mloScene.Lights.Count == n0 && !mloScene.Lights.Contains(added), $"mlo lights {n0} -> {mloScene.Lights.Count}");
                    lightScene.SelectedIndex = lightScene.Lights.IndexOf(added); lightScene.DeleteSelected();
                }
                panel.SwitchWorkspace(LightPanel.Space.Mlo);
                check("mloscene: back in MLO the prop is still there", ReferenceEquals(scene, mloScene) && mloScene.Files.Any(f => f.Path == ydr3), $"{mloScene.Files.Count} files");
                var dto = MloCreatorProject.ToDto(ui.Session ?? new MloCreatorSession(), mloScene);
                check("mloscene: the .mloproj lists the MLO scene's files", dto.SceneFiles.Contains(ydr3) && !dto.SceneFiles.Any(p => lightScene.Files.Any(f => f.Path == p) && !mloScene.Files.Any(f => f.Path == p)), $"{dto.SceneFiles.Count} scene files");
                ui.ShowPage(MloCreatorPanel.PageKind.Lights);
                UpdateMloLightGizmoEnabled_L3(ui);
                check("mloscene: the light gizmo is live on the Lights page", gizmo.Enabled && ReferenceEquals(gizmo.Scene, mloScene), $"enabled {gizmo.Enabled}");
                if (mloScene.Lights.Count > 0)
                {
                    mloScene.SelectedIndex = mloScene.Lights.Count - 1;
                    var l = mloScene.SelectedLight;
                    var before = l.Intensity;
                    mloScene.PushUndo(); l.Intensity = before + 1.5f; mloScene.Dirty = true;
                    check("mloscene: a light of the MLO scene edits and undoes on its own stack", Math.Abs(l.Intensity - before - 1.5f) < 1e-4f && mloScene.CanUndo, $"{before} -> {l.Intensity}");
                    mloScene.Undo();
                    check("mloscene: undo restored it", Math.Abs(mloScene.Lights[mloScene.Lights.Count - 1].Intensity - before) < 1e-4f, $"{mloScene.Lights[mloScene.Lights.Count - 1].Intensity}");
                }
                ui.ShowPage(MloCreatorPanel.PageKind.Room);
                UpdateMloLightGizmoEnabled_L3(ui);
                check("mloscene: off the Lights page the light gizmo yields to the room gizmo", !gizmo.Enabled, $"enabled {gizmo.Enabled}");
                MloAssetsTest_L3(check);
                MloEditTest_N3(check);
                if (screenshotPath == null) panel.SwitchWorkspace(LightPanel.Space.Light);
            }
            catch (Exception ex)
            {
                check("mloscene: no exception", false, ex.ToString());
            }
        }
    }
}

