using System;
using System.CodeDom;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GRD2GPC {
    internal class Record {
        private readonly long _timestamp;
        public readonly int[] Buttons;

        private Record(long timestamp, int[] buttons) {
            _timestamp = timestamp;
            Buttons = buttons;
        }

        public Record() {
            Buttons = new int[36];
        }

        public static Record FromStream(Stream src) {
            var tmp = new byte[8];
            if (src.Read(tmp, 0, tmp.Length) != tmp.Length)
                return null;
            var timestamp = BitConverter.ToInt64(tmp, 0);
            var buttons = new byte[36];
            return src.Read(buttons, 0, buttons.Length) != buttons.Length ? null : new Record(timestamp, buttons.Select(b => (int)(sbyte)b).ToArray());
        }

        public long GetTimeDiff(Record previous) {
            if (previous == null)
                return 0;
            var ret = _timestamp - previous._timestamp;
            return ret < 0 ? 0 : ret;
        }
    }
}