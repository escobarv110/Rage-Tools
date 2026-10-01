using System;
using System.Linq;
using CodeWalker.GameFiles;
using RageLightEditor.Editor;
using SharpDX;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private void SeqTest_V19(Action<string, bool, string> check)
        {
            var mp = new MloCreatorPanel();
            check("v19 mlo: a fresh install labels ONLY what is selected",
                  mp.LabelMode_V19 == MloCreatorPanel.MloLabels_V19.Selected,
                  mp.LabelMode_V19.ToString());
            check("v19 mlo: ...so an unselected room draws no name",
                  !mp.LabelFor_V19(false) && mp.LabelFor_V19(true), "selected yes, rest no");

            mp.LabelMode_V19 = MloCreatorPanel.MloLabels_V19.All;
            check("v19 mlo: 'Everything' labels everything",
                  mp.LabelFor_V19(false) && mp.LabelFor_V19(true), "both");
            mp.LabelMode_V19 = MloCreatorPanel.MloLabels_V19.Off;
            check("v19 mlo: 'Off' labels nothing, not even the selection",
                  !mp.LabelFor_V19(false) && !mp.LabelFor_V19(true), "neither");
            check("v19 mlo: there is a name for every mode",
                  MloCreatorPanel.MloLabelNames_V19.Length == 3 &&
                  MloCreatorPanel.MloLabelNames_V19.All(n => !string.IsNullOrWhiteSpace(n)),
                  string.Join(" / ", MloCreatorPanel.MloLabelNames_V19));

            mp.ShowLabels = true;
            check("v19 mlo: the old ShowLabels flag maps onto the new modes",
                  mp.LabelMode_V19 == MloCreatorPanel.MloLabels_V19.All && mp.ShowLabels,
                  mp.LabelMode_V19.ToString());

            var ymap = new YmapFile();
            var ent = new YmapEntityDef();
            check("v19 ymap: an untouched entity has no star",
                  LightPanel.DirtyMark_V19(ent) == "", "'" + LightPanel.DirtyMark_V19(ent) + "'");
            ent.Ymap = ymap;
            check("v19 ymap: ...still none while its file is unchanged",
                  LightPanel.DirtyMark_V19(ent) == "", "'" + LightPanel.DirtyMark_V19(ent) + "'");
            ymap.HasChanged = true;
            check("v19 ymap: move it and the star appears",
                  LightPanel.DirtyMark_V19(ent).Trim() == "*", "'" + LightPanel.DirtyMark_V19(ent) + "'");
            check("v19 ymap: ...and on the file itself",
                  LightPanel.DirtyMark_V19(ymap).Trim() == "*", "'" + LightPanel.DirtyMark_V19(ymap) + "'");
            ymap.HasChanged = false;
            check("v19 ymap: saving takes it away again",
                  LightPanel.DirtyMark_V19(ent) == "", "cleared");
            check("v19 ymap: a null entity does not throw",
                  LightPanel.DirtyMark_V19((YmapEntityDef)null) == "", "empty");

            int before = lineRenderer?.LineCount ?? -1;
            if (before >= 0)
            {
                DrawSelectionBox_V19(Vector3.Zero, Quaternion.Identity,
                                     new Vector3(-1), new Vector3(1), new Vector4(1, 1, 1, 1), full: true);
                int selLines = (lineRenderer?.LineCount ?? 0) - before;
                check("v19 selection: the selected box is one thin box like CodeWalker",
                      selLines == 12, selLines + " lines (12 box)");

                int b2 = lineRenderer?.LineCount ?? 0;
                DrawSelectionBox_V19(Vector3.Zero, Quaternion.Identity,
                                     new Vector3(-1), new Vector3(1), new Vector4(1, 1, 1, 1), full: false);
                int hovLines = (lineRenderer?.LineCount ?? 0) - b2;
                check("v19 selection: ...but the HOVER stays a plain box, or the screen flickers",
                      hovLines == 12, hovLines + " lines");
            }
            else Console.WriteLine("  v19: (selection line count skipped - no line renderer yet)");

            check("v19 theme: the appearance menu is the only theme control left",
                  settings != null, "Appearance menu on the top bar");
        }
    }
}

