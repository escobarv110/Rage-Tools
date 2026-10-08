using System;

namespace RageLightEditor
{
    public partial class MainForm
    {
        private bool SeqOnly_U28(Action<string, bool, string> check, ref int fails)
        {
            var only = Environment.GetEnvironmentVariable("RLE_SEQONLY");
            if (string.IsNullOrWhiteSpace(only)) return false;
            foreach (var name in only.Split(','))
            {
                switch (name.Trim().ToUpperInvariant())
                {
                    case "R2": SeqTest_R2(check); break;
                    case "S1": SeqTest_S1(check); break;
                    case "T1": SeqTest_T1(check); break;
                    case "T2": TabMatrixTest_T2(check); break;
                    case "Q4": SectionIsolationTest_Q4(check); break;
                    case "V21": SeqTest_SectionCams_V21(check); break;
                    case "U28": SeqTest_U28(check); break;
                    case "W3": SeqTest_W3(check); break;
                    case "U21": SeqTest_Log_U21(check); break;
                    case "U30": SeqTest_U30(check); break;
                    case "YMF": YmfProbe_U30(check); break;
                    case "V31": SeqTest_MloExport_V31(check); break;
                    case "UPD":
                        Editor.UpdateCheck_U30.Start();
                        for (int i = 0; i < 100 && !Editor.UpdateCheck_U30.Checked; i++) System.Threading.Thread.Sleep(100);
                        check("update check: GitHub answers", Editor.UpdateCheck_U30.Checked && Editor.UpdateCheck_U30.Error == null, Editor.UpdateCheck_U30.Summary + " | latest " + Editor.UpdateCheck_U30.Latest);
                        break;
                }
            }
            Console.WriteLine(fails == 0 ? "SEQTEST PASSED" : $"SEQTEST FAILED ({fails})");
            Close();
            return true;
        }

        partial void SeqTest_U28(Action<string, bool, string> check);
        partial void SeqTest_U30(Action<string, bool, string> check);
    }
}
