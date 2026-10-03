using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        partial void SeqTest_R2(Action<string, bool, string> check)
        {
            try { SelectMoveTest_R2(check); } catch (Exception ex) { check("R2 select/move test ran", false, ex.Message); }
            try { MatSceneTest_R2(check); } catch (Exception ex) { check("R2 materials scene test ran", false, ex.Message); }
            try { AreaGrassTest_R2(check); } catch (Exception ex) { check("R2 area grass test ran", false, ex.Message); }
        }

        private static YmapEntityDef MakeTestEntity_R2(YmapFile ymap, Vector3 at, string name, float half)
        {
            var cad = new CBaseArchetypeDef
            {
                name = new MetaHash(JenkHash.GenHash(name)),
                assetName = new MetaHash(JenkHash.GenHash(name)),
                bbMin = new Vector3(-half),
                bbMax = new Vector3(half),
                bsCentre = Vector3.Zero,
                bsRadius = half * 1.8f,
                lodDist = 500.0f,
            };
            var arch = new Archetype();
            arch.Init(null, ref cad);

            var cent = new CEntityDef
            {
                archetypeName = cad.name,
                position = at,
                rotation = new Vector4(0, 0, 0, 1),
                scaleXY = 1.0f,
                scaleZ = 1.0f,
                flags = 32,
                parentIndex = -1,
                lodDist = 500.0f,
                lodLevel = rage__eLodType.LODTYPES_DEPTH_ORPHANHD,
                priorityLevel = rage__ePriorityLevel.PRI_REQUIRED,
            };
            var e = new YmapEntityDef(ymap, 0, ref cent);
            e.SetArchetype(arch);
            return e;
        }

        private void SelectMoveTest_R2(Action<string, bool, string> check)
        {
            if (deviceResources == null || camera == null) { check("select/move: a device to click on", false, "no device"); return; }

            var wasSpace = panel.Workspace;
            bool wasBuilt = worldBuilt;
            var wasVisible = World.Visible.ToArray();
            var wasFade = World.Fade.ToArray();
            var wasSel = WorldEdit.Selection;
            bool hadProject = ProjWin.Project != null;
            var wasMode = worldGizmo.Mode;
            bool wasMouseSel = panel.MouseSelectEnabled;

            YmapFile ymap = null;
            try
            {
                panel.SwitchWorkspace(LightPanel.Space.World);
                panel.MouseSelectEnabled = true;
                worldGizmo.Mode = WorldGizmoMode.Translate;
                worldGizmo.Enabled = true;
                int mi = LightPanel.IndexOfMode(WorldSelectionMode.Entity);
                if (mi >= 0) panel.SelectionMode = mi;

                CameraSequence.ApplyToCamera(camera, new Vector3(0, 0, 0), 1.4f, 0.0f, 60.0f);
                camera.SnapSmoothing();
                camera.Update();
                var at = camera.Position + camera.GetForward() * 12.0f;

                ymap = new YmapFile { Name = "rle_r2_test.ymap" };
                var ent = MakeTestEntity_R2(ymap, at, "rle_r2_test_prop", 1.5f);
                var decoy = MakeTestEntity_R2(ymap, at + camera.GetRight() * 6.0f, "rle_r2_test_decoy", 1.0f);

                worldBuilt = true;
                modelsReleasedAtStart_R2 = worldRender.ModelsReleased;
                World.Visible.Clear(); World.Fade.Clear();
                World.Visible.Add(ent); World.Fade.Add(1.0f);
                World.Visible.Add(decoy); World.Fade.Add(1.0f);
                WorldEdit.Deselect();

                check("select/move: the test entity projects into the viewport",
                      Project_R2(ent.Position, out int px0, out int py0), $"{ent.Position}");

                const int Rounds = 4;
                int movedRounds = 0, selectedRounds = 0;
                int undoBefore = WorldHistory.Count;
                var startPos = ent.Position;
                string detail = "";
                for (int r = 1; r <= Rounds; r++)
                {
                    WorldEdit.Deselect();
                    if (!FindPixelFor_R2(ent, out int px, out int py))
                    {
                        detail += $"[round {r}: no pixel picks it]";
                        break;
                    }
                    MouseDown_R2(px, py);
                    MouseUp_R2(px, py);
                    if (WorldEdit.Selection.HasValue) { detail += $"[round {r}: the LEFT button selected {WorldEdit.Selection.GetNameString("?")}]"; break; }
                    MouseRightClick_R2(px, py);
                    bool got = ReferenceEquals(WorldEdit.Selection.EntityDef, ent);
                    if (got) selectedRounds++;
                    else { detail += $"[round {r}: click selected {WorldEdit.Selection.GetNameString("nothing")}]"; break; }

                    var before = ent.Position;
                    Project_R2(before, out int gx, out int gy);
                    MouseDown_R2(gx, gy);
                    bool grabbed = worldGizmo.Dragging;
                    MouseMove_R2(gx + 45, gy - 25);
                    MouseUp_R2(gx + 45, gy - 25);
                    float moved = (ent.Position - before).Length();
                    if (grabbed && moved > 0.01f) movedRounds++;
                    else { detail += $"[round {r}: grabbed={grabbed} moved={moved:0.###}]"; break; }
                }

                check($"select/move: the entity is re-selected on all {Rounds} rounds", selectedRounds == Rounds, $"{selectedRounds}/{Rounds} {detail}");
                check($"select/move: the gizmo moves it on all {Rounds} rounds", movedRounds == Rounds, $"{movedRounds}/{Rounds}, total travel {(ent.Position - startPos).Length():0.00} m {detail}");
                check("select/move: each drag is its own undo step", WorldHistory.Count - undoBefore == movedRounds,
                      $"{WorldHistory.Count - undoBefore} steps for {movedRounds} drags");

                if (movedRounds == Rounds)
                {
                    var afterAll = ent.Position;
                    TryWorldUndo();
                    bool onlyOne = (ent.Position - afterAll).Length() > 0.01f && (ent.Position - startPos).Length() > 0.01f;
                    check("select/move: one undo walks back one move, not all of them", onlyOne,
                          $"at {(ent.Position - startPos).Length():0.00} m from the start (was {(afterAll - startPos).Length():0.00})");
                }
                int guard = 0;
                while (WorldHistory.Count > undoBefore && guard++ < 32) TryWorldUndo();
                check("select/move: undoing every step puts it back", (ent.Position - startPos).Length() < 0.01f,
                      $"{(ent.Position - startPos).Length():0.###} m from the start");

                check("select/move: editing a game entity does not release the world's models",
                      worldRender.ModelsReleased == modelsReleasedAtStart_R2,
                      $"{worldRender.ModelsReleased} released, was {modelsReleasedAtStart_R2} before the edit (the first edit used to release the whole city)");
            }
            finally
            {
                WorldEdit.Deselect();
                if (ymap != null)
                {
                    ProjWin.Project?.RemoveYmapFile(ymap);
                    if (!hadProject) { ProjWin.Project = null; ProjWin.Select(null); ProjWin.Visible = false; }
                    RebuildProjectOverrides();
                }
                World.Visible.Clear(); World.Visible.AddRange(wasVisible);
                World.Fade.Clear(); World.Fade.AddRange(wasFade);
                worldBuilt = wasBuilt;
                worldGizmo.Mode = wasMode;
                panel.MouseSelectEnabled = wasMouseSel;
                WorldEdit.Select(wasSel);
                panel.SwitchWorkspace(wasSpace);
            }
        }

        private long modelsReleasedAtStart_R2;

        private void MatSceneTest_R2(Action<string, bool, string> check)
        {
            var wasSpace = panel.Workspace;
            panel.SwitchWorkspace(LightPanel.Space.Light);
            var wasCam = camera.Capture();
            float wasHour = panel.PreviewHour;
            int wasRender = panel.RenderMode;
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "rle_r2_matscene");
                Directory.CreateDirectory(dir);
                string src = Path.Combine(dir, "r2_base.ydr");
                if (!File.Exists(src)) TestSceneGenerator.Run(src);
                string forLight = Path.Combine(dir, "prop_u28_shared.ydr");
                File.Copy(src, forLight, true);

                panel.SwitchWorkspace(LightPanel.Space.Light);
                LoadFile(forLight);
                var mine = lightScene.Files.FirstOrDefault(f => f.Path == forLight);
                check("shared view: a prop opened in Lights", mine != null, $"{lightScene.Files.Count} files");
                camera.Target = new Vector3(4, -3, 1); camera.Distance = 6; camera.Yaw = 0.9f; camera.Pitch = 0.25f;
                camera.SnapSmoothing(); camera.Update();
                var lightsCam = camera.Capture();
                panel.PreviewHour = 17.5f;
                panel.RenderMode = 1;

                panel.SwitchWorkspace(LightPanel.Space.Material);
                check("shared view: Materials draws the same scene as Lights",
                      ReferenceEquals(scene, lightScene) && ReferenceEquals(panel.ActiveScene, lightScene) &&
                      ReferenceEquals(gizmo.Scene, lightScene) && ReferenceEquals(materialPanel.Scene, lightScene), "");
                check("shared view: ...so the prop is still there", lightScene.Files.Any(f => f.Path == forLight), "");
                check("shared view: the camera did not move", camera.Capture().SameAs(lightsCam), $"{camera.Position}");
                check("shared view: the clock did not change", Math.Abs(panel.PreviewHour - 17.5f) < 0.001f, $"{panel.PreviewHour:0.00}");
                check("shared view: the render mode did not change", panel.RenderMode == 1, panel.RenderMode.ToString());

                camera.Target = new Vector3(-2, 5, 2); camera.Distance = 3; camera.Yaw = 2.1f; camera.Pitch = 0.3f;
                camera.SnapSmoothing(); camera.Update();
                var matCam = camera.Capture();
                panel.PreviewHour = 6.0f;
                panel.SwitchWorkspace(LightPanel.Space.World);
                panel.SwitchWorkspace(LightPanel.Space.Light);
                check("shared view: Lights picks up where Materials left the camera", camera.Capture().SameAs(matCam), $"{camera.Position}");
                check("shared view: ...and the clock", Math.Abs(panel.PreviewHour - 6.0f) < 0.001f, $"{panel.PreviewHour:0.00}");
                panel.RenderMode = 0;

                if (mine != null)
                {
                    scene.SelectedIndex = -1; scene.ActiveFile = null; scene.SelectedFiles.Clear();
                    panel.EntityPicking = false;
                    camera.Update();
                    var b = mine.Model.Bounds;
                    var c = (b.Minimum + b.Maximum) * 0.5f;
                    var sp = Vector3.Project(c, 0, 0, deviceResources.Width, deviceResources.Height, 0, 1, camera.ViewProjMatrix);
                    bool ok = MagnetAt_U28((int)sp.X, (int)sp.Y);
                    camera.SnapSmoothing(); camera.Update();
                    check("magnet: the key picks what is under the mouse", ok && ReferenceEquals(scene.ActiveFile, mine),
                          $"ok {ok} active {scene.ActiveFile?.Name ?? "-"} at {sp.X:0},{sp.Y:0}");
                    var look = camera.GetPickRay(deviceResources.Width / 2, deviceResources.Height / 2, deviceResources.Width, deviceResources.Height);
                    float off = Vector3.Cross(c - look.Position, look.Direction).Length();
                    check("magnet: ...and flies to it", off < 0.5f && Vector3.Distance(camera.Position, c) < 40f,
                          $"centre {c} is {off:0.##} m off the view line, {Vector3.Distance(camera.Position, c):0.#} m away");
                    bool miss = MagnetAt_U28(2, 2);
                    check("magnet: empty sky does nothing", !miss, "");
                }
                check("magnet: B is the default key", AppSettings.Actions.Any(a => a.Id == "Magnet" && a.Default == System.Windows.Forms.Keys.B), "");
            }
            finally
            {
                panel.SwitchWorkspace(LightPanel.Space.Light);
                camera.Restore(wasCam);
                panel.PreviewHour = wasHour;
                panel.RenderMode = wasRender;
                panel.SwitchWorkspace(wasSpace);
            }
        }

        private static YmapGrassInstanceBatch MakeTestGrass_R2(YmapFile ymap, Vector3 min, Vector3 max, int n)
        {
            var size = max - min;
            var inst = new rage__fwGrassInstanceListDef__InstanceData[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float fx = (x + 0.5f) / n, fy = (y + 0.5f) / n;
                    inst[y * n + x] = new rage__fwGrassInstanceListDef__InstanceData
                    {
                        Position = new ArrayOfUshorts3
                        {
                            u0 = (ushort)(fx * 65535.0f),
                            u1 = (ushort)(fy * 65535.0f),
                            u2 = (ushort)(0.5f * 65535.0f),
                        },
                        Scale = 128,
                        Ao = 255,
                    };
                }
            var def = new rage__fwGrassInstanceListDef
            {
                BatchAABB = new rage__spdAABB { min = new Vector4(min, 0), max = new Vector4(max, 0) },
                lodDist = 200,
                ScaleRange = new Vector3(1, 1, 1),
            };
            var b = new YmapGrassInstanceBatch
            {
                Ymap = ymap,
                Batch = def,
                Instances = inst,
                AABBMin = min,
                AABBMax = max,
                Position = (min + max) * 0.5f,
                Radius = (max - min).Length() * 0.5f,
            };
            b.UpdateInstanceCount();
            return b;
        }

        private void AreaGrassTest_R2(Action<string, bool, string> check)
        {
            var st = AreaTool;
            var wasAreas = st.Areas.ToList();
            int wasSelected = st.Selected;
            bool hadProject = ProjWin.Project != null;
            bool wasBuilt = worldBuilt;
            var wasSpace = panel.Workspace;
            YmapFile ymap = null;
            try
            {
                panel.SwitchWorkspace(LightPanel.Space.World);
                worldBuilt = true;
                ymap = new YmapFile { Name = "rle_r2_grass.ymap" };
                var batch = MakeTestGrass_R2(ymap, new Vector3(0, 0, 0), new Vector3(20, 20, 2), 20);
                ymap.GrassInstanceBatches = new[] { batch };
                areaGrassYmapsOverride_R2 = new List<YmapFile> { ymap };

                var a = new WorldArea { Name = "R2 grass area", ZMin = -2.0f, ZMax = 10.0f };
                a.Corners.Add(new Vector3(-1, -1, 0));
                a.Corners.Add(new Vector3(10, -1, 0));
                a.Corners.Add(new Vector3(10, 21, 0));
                a.Corners.Add(new Vector3(-1, 21, 0));
                st.Areas.Clear();
                st.Add(a);
                st.Selected = st.Areas.IndexOf(a);
                st.IncludeGrass_R2 = true;

                RefreshAreaGrass_R2();
                int inside = st.GrassInstancesInside_R2;
                check("grass: the area tool counts the grass instances inside it", inside == 200,
                      $"{inside} of {batch.Instances.Length} (expected 200 - the left half of a 20x20 lawn)");
                check("grass: and says which batch they belong to", st.GrassInside_R2.Count == 1 && st.GrassInside_R2[0].Total == 400,
                      st.GrassInside_R2.Count > 0 ? $"{st.GrassInside_R2[0].Inside}/{st.GrassInside_R2[0].Total} in {st.GrassInside_R2[0].Ymap}" : "none");

                projectAutoAddTestOverride = true;
                int removed = AreaDeleteGrass_R2(true, out _, out _, out int batches);
                projectAutoAddTestOverride = false;
                RefreshAreaGrass_R2();
                check("grass: Delete removes exactly the instances inside the area", removed == 200 && batch.Instances.Length == 200,
                      $"removed {removed}, {batch.Instances.Length} left in the batch");
                check("grass: none of the remaining instances are inside the area", st.GrassInstancesInside_R2 == 0,
                      $"{st.GrassInstancesInside_R2} still inside");
                check("grass: the packed instance count follows the list", batch.Batch.InstanceList.Count1 == batch.Instances.Length,
                      $"packed {batch.Batch.InstanceList.Count1}, list {batch.Instances.Length}");
                check("grass: the ymap is marked changed so the edit can be saved", ymap.HasChanged && WorldEdit.IsDirty(ymap),
                      $"changed {ymap.HasChanged} dirty {WorldEdit.IsDirty(ymap)}");
                check("grass: ...and it joined the project", ProjWin.Project != null && ProjWin.Project.ContainsYmap(ymap),
                      $"project ymaps {ProjWin.Project?.YmapFiles.Count ?? 0}");
                check("grass: the delete is one undo step", WorldHistory.CanUndo && (WorldHistory.NextUndoName ?? "").Contains("grass"),
                      WorldHistory.NextUndoName ?? "none");

                TryWorldUndo();
                RefreshAreaGrass_R2();
                check("grass: undo puts every instance back", batch.Instances.Length == 400 && st.GrassInstancesInside_R2 == 200,
                      $"{batch.Instances.Length} in the batch, {st.GrassInstancesInside_R2} inside again");

                a.Corners.Clear();
                a.Corners.Add(new Vector3(-5, -5, 0));
                a.Corners.Add(new Vector3(25, -5, 0));
                a.Corners.Add(new Vector3(25, 25, 0));
                a.Corners.Add(new Vector3(-5, 25, 0));
                projectAutoAddTestOverride = true;
                int removedAll = AreaDeleteGrass_R2(true, out _, out _, out int batchesGone);
                projectAutoAddTestOverride = false;
                check("grass: an area that empties a batch removes the batch from its ymap",
                      removedAll == 400 && batchesGone == 1 && (ymap.GrassInstanceBatches?.Length ?? 0) == 0,
                      $"removed {removedAll}, batches gone {batchesGone}, ymap now has {ymap.GrassInstanceBatches?.Length ?? 0}");
                TryWorldUndo();
                check("grass: and undo brings the batch back", (ymap.GrassInstanceBatches?.Length ?? 0) == 1 && batch.Instances.Length == 400,
                      $"{ymap.GrassInstanceBatches?.Length ?? 0} batch(es), {batch.Instances.Length} instances");

                RefreshAreaGrass_R2();
                check("grass: the Delete button counts the grass in its label", st.DeleteLabel_R2.Contains("grass") && st.CanDelete_R2,
                      st.DeleteLabel_R2);
            }
            finally
            {
                areaGrassYmapsOverride_R2 = null;
                st.Areas.Clear();
                foreach (var w in wasAreas) st.Areas.Add(w);
                st.Selected = Math.Min(wasSelected, st.Areas.Count - 1);
                st.GrassInside_R2.Clear();
                st.GrassInstancesInside_R2 = 0;
                if (ymap != null)
                {
                    ProjWin.Project?.RemoveYmapFile(ymap);
                    if (!hadProject) { ProjWin.Project = null; ProjWin.Select(null); ProjWin.Visible = false; }
                    RebuildProjectOverrides();
                }
                worldBuilt = wasBuilt;
                panel.SwitchWorkspace(wasSpace);
            }
        }
    }
}

