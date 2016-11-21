using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace GRD2GPC {
    internal class Program {
        private static int _minimumAccelMovement = 1;
        private static int _minimumStickMovement = 1;
        private static int _stickDeadzone = 20;
        private static bool _accel;
        private static bool _touch;

        private static readonly string[] ButtonNames = {
                                                           "PS4_PS",
                                                           "PS4_SHARE",
                                                           "PS4_OPTIONS",
                                                           "PS4_R1",
                                                           "PS4_R2",
                                                           "PS4_R3",
                                                           "PS4_L1",
                                                           "PS4_L2",
                                                           "PS4_L3",
                                                           "PS4_RX",
                                                           "PS4_RY",
                                                           "PS4_LX",
                                                           "PS4_LY",
                                                           "PS4_UP",
                                                           "PS4_DOWN",
                                                           "PS4_LEFT",
                                                           "PS4_RIGHT",
                                                           "PS4_TRIANGLE",
                                                           "PS4_CIRCLE",
                                                           "PS4_CROSS",
                                                           "PS4_SQUARE",
                                                           "PS4_ACCX",
                                                           "PS4_ACCY",
                                                           "PS4_ACCZ",
                                                           "PS4_GYROX",
                                                           "PS4_GYROY",
                                                           "PS4_GYROZ",
                                                           "PS4_TOUCH",
                                                           "PS4_TOUCHX",
                                                           "PS4_TOUCHY"
                                                       };

        private static void Finished() {
            Console.ResetColor();
            Console.WriteLine("Hit <Enter> To Exit");
            Console.ReadLine();
        }

        private static void Error(string fmt, params object[] args) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(fmt, args);
            Console.ResetColor();
        }

        private static void Main(string[] args) {
            string grdname = null;
            var gpcname = "";
            var haveGpc = false;
            var cmbname = "grd2gpc";
            foreach (var s in args) {
                if (s.Equals("--accel"))
                    _accel = true;
                else if (s.Equals("--touch"))
                    _touch = true;
                else if (s.StartsWith("--gpc=")) {
                    gpcname = s.Substring(6);
                    haveGpc = true;
                }
                else if (s.StartsWith("--cmbname="))
                    cmbname = s.Substring(10);
                else if (s.StartsWith("--deadzone="))
                    int.TryParse(s.Substring(11), out _stickDeadzone);
                else if (s.StartsWith("--minstick="))
                    int.TryParse(s.Substring(11), out _minimumStickMovement);
                else if (s.StartsWith("--minaccel="))
                    int.TryParse(s.Substring(11), out _minimumAccelMovement);
                else
                    grdname = s;
            }
            _stickDeadzone = Math.Min(_stickDeadzone, 99);
            _stickDeadzone = Math.Max(_stickDeadzone, 0);
            _minimumStickMovement = Math.Min(_minimumStickMovement, 99 - _stickDeadzone);
            _minimumStickMovement = Math.Max(_minimumStickMovement, 0);
            _minimumAccelMovement = Math.Min(_minimumAccelMovement, 99);
            _minimumAccelMovement = Math.Max(_minimumAccelMovement, 0);
            var ver = Assembly.GetAssembly(typeof(Program)).GetName().Version;
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("GRD2GPC v{0}.{1} Started...", ver.Major, ver.Minor);
            Console.WriteLine("This software was brought to you by Swizzy");
            Console.ResetColor();
            Console.WriteLine();
            if (!File.Exists(grdname)) {
                Error("ERROR: GRD File '{0}' doesn't exist...", grdname ?? "<Undefined>");
                Finished();
                return;
            }
            if (!haveGpc)
                gpcname = Path.GetFileNameWithoutExtension(grdname) + ".gpc";
            Console.WriteLine("Settings:");
            Console.WriteLine("GRD File: {0}", grdname);
            Console.WriteLine("GPC File: {0}", gpcname);
            Console.WriteLine("ComboName: {0}", cmbname);
            Console.WriteLine("Minimum Acceleration Movement: {0}", _minimumAccelMovement);
            Console.WriteLine("Minimum Stick Movement: {0}", _minimumStickMovement);
            Console.WriteLine("Stick Deadzone: {0}", _stickDeadzone);
            Console.WriteLine("Include Accelerometers: {0}", _accel ? "Yes" : "No");
            Console.WriteLine("Include Touchpad: {0}", _touch ? "Yes" : "No");
            Console.WriteLine();
            var records = new List<Record>();
            Console.WriteLine("Parsing GRD...");
            using (var fs = File.OpenRead(grdname)) {
                while (true) {
                    var r = Record.FromStream(fs);
                    if (r != null)
                        records.Add(r);
                    else
                        break; // We're done processing this file now, there's nothing more to parse...
                }
            }
            if (records.Count <= 1) {
                Error("ERROR: Something went wrong with parsing the GRD, it either only had a single entry or it's incorrect size... try another one...");
                Finished();
                return;
            }
            var totalTime = records.Last().GetTimeDiff(records[0]);
            if (totalTime <= 0) {
                Error("ERROR: Something went wrong with parsing the GRD, the total time difference between the first and last entry is less then or equal to 0, it's likely ocrrupt... try another one...");
                Finished();
                return;
            }
            Console.WriteLine("Parsing finished, total time in recording: {0} ms", totalTime);
            Console.WriteLine("Generating GPC code...");
            using (var fs = File.OpenWrite(gpcname)) {
                using (var sw = new StreamWriter(fs)) {
                    sw.WriteLine("//");
                    sw.WriteLine("// " + DateTime.Now.ToString("yyyy-MM-d HH:mm:ss"));
                    sw.WriteLine("// GPC Generated by GRD2GPC v{0}.{1}", ver.Major, ver.Minor);
                    sw.WriteLine("//----------------------------------------");
                    sw.WriteLine("combo {0} {{", cmbname);
                    var sval = GenerateSetVal(records[0], new Record());
                    if (!string.IsNullOrWhiteSpace(sval))
                        sw.WriteLine(sval.TrimEnd());
                    long waitTime = 0;
                    for (var i = 1; i < records.Count; i++) {
                        sval = GenerateSetVal(records[i], records[i - 1]);
                        if (!string.IsNullOrWhiteSpace(sval) || waitTime >= 4000) {
                            Trace.WriteLine("Wait: " + waitTime + " sval: " + sval);
                            if (waitTime <= 0)
                                waitTime = 10;
                            if (waitTime % 10 > 0) {
                                if (waitTime % 10 >= 5) {
                                    waitTime -= waitTime % 10;
                                    waitTime += 10; // Round it up
                                }
                                else
                                    waitTime -= waitTime % 10;
                            }
                            if (waitTime > 4000)
                                waitTime = 4000;
                            sw.WriteLine("\twait(" + waitTime.ToString("F0") + ");");
                            waitTime = 0;
                            sval = GenerateSetVal(records[i], records[i - 1], true).TrimEnd();
                            if (!string.IsNullOrWhiteSpace(sval))
                                sw.WriteLine(sval);
                        }
                        else
                            waitTime += records[i].GetTimeDiff(records[i - 1]);
                    }
                    if (waitTime > 0) {
                        if (waitTime % 10 > 0) {
                            if (waitTime % 10 >= 5) {
                                waitTime -= waitTime % 10;
                                waitTime += 10; // Round it up
                            }
                            else
                                waitTime -= waitTime % 10;
                            if (waitTime > 4000)
                                waitTime = 4000;
                        }
                        sw.WriteLine("\twait(" + waitTime.ToString("F0") + ");");
                    }
                    sw.WriteLine("}");
                }
            }
            Finished();
        }

        private static string GetSetVal(int index, int value) { return value == 0 ? null : string.Format("\tset_val({0}, {1});", ButtonNames[index], value); }

        private static int GetNewValue(int v1, int v2, out bool changed) {
            v1 = v1 > 0 ? 100 : 0;
            v2 = v2 > 0 ? 100 : 0;
            changed = true;
            if (v1 != v2)
                return v2;
            changed = false;
            return v1;
        }

        private static int GetNewValue(int v1, int v2, bool isStick, out bool changed) {
            if (isStick) {
                if (Math.Abs(v1) < _stickDeadzone)
                    v1 = 0; // Set deadzone values to 0
                if (Math.Abs(v2) < _stickDeadzone)
                    v2 = 0; // Set deadzone values to 0
                changed = Math.Abs(v1 - v2) > _minimumStickMovement;
                return v1;
            }
            changed = Math.Abs(v1 - v2) > _minimumAccelMovement;
            return v1;
        }

        private static string GenerateSetVal(Record data, Record last, bool overrideChanged = false) {
            bool changed;
            var sb = new StringBuilder();
            var inp = GetSetVal(0, GetNewValue(data.Buttons[0], last.Buttons[0], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(1, GetNewValue(data.Buttons[1], last.Buttons[1], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(02, GetNewValue(data.Buttons[2], last.Buttons[2], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(03, GetNewValue(data.Buttons[3], last.Buttons[3], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(04, GetNewValue(data.Buttons[4], last.Buttons[4], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(05, GetNewValue(data.Buttons[5], last.Buttons[5], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(06, GetNewValue(data.Buttons[6], last.Buttons[6], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(07, GetNewValue(data.Buttons[7], last.Buttons[7], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(08, GetNewValue(data.Buttons[8], last.Buttons[8], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(09, GetNewValue(data.Buttons[9], last.Buttons[9], true, out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(10, GetNewValue(data.Buttons[10], last.Buttons[10], true, out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(11, GetNewValue(data.Buttons[11], last.Buttons[11], true, out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(12, GetNewValue(data.Buttons[12], last.Buttons[12], true, out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(13, GetNewValue(data.Buttons[13], last.Buttons[13], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(14, GetNewValue(data.Buttons[14], last.Buttons[14], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(15, GetNewValue(data.Buttons[15], last.Buttons[15], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(16, GetNewValue(data.Buttons[16], last.Buttons[16], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(17, GetNewValue(data.Buttons[17], last.Buttons[17], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(18, GetNewValue(data.Buttons[18], last.Buttons[18], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(19, GetNewValue(data.Buttons[19], last.Buttons[19], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(20, GetNewValue(data.Buttons[20], last.Buttons[20], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            if (_accel) {
                inp = GetSetVal(21, GetNewValue(data.Buttons[21], last.Buttons[21], false, out changed));
                if (inp != null && (changed || overrideChanged))
                    sb.AppendLine(inp);
                inp = GetSetVal(22, GetNewValue(data.Buttons[22], last.Buttons[22], false, out changed));
                if (inp != null && (changed || overrideChanged))
                    sb.AppendLine(inp);
                inp = GetSetVal(23, GetNewValue(data.Buttons[23], last.Buttons[23], false, out changed));
                if (inp != null && (changed || overrideChanged))
                    sb.AppendLine(inp);
            }
            if (!_touch)
                return sb.ToString();
            inp = GetSetVal(27, GetNewValue(data.Buttons[27], last.Buttons[27], out changed));
            if (inp != null && (changed || overrideChanged))
                sb.AppendLine(inp);
            inp = GetSetVal(28, data.Buttons[28]);
            if (inp != null && data.Buttons[28] != 0)
                sb.AppendLine(inp);
            inp = GetSetVal(29, data.Buttons[29]);
            if (inp != null && data.Buttons[29] != 0)
                sb.AppendLine(inp);
            return sb.ToString();
        }
    }
}