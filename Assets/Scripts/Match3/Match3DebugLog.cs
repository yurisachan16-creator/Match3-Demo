using System.Text;

namespace Match3.App.Demo
{
    public static class Match3DebugLog
    {
        public static bool Enabled { get; set; } = true;
        public static int Capacity { get; set; } = 256;

        private static readonly object Sync = new object();
        private static string[] _buffer = new string[256];
        private static int _count;
        private static int _index;

        public static void Record(string message)
        {
            if (Enabled == false || string.IsNullOrEmpty(message))
            {
                return;
            }

            lock (Sync)
            {
                EnsureCapacity();
                _buffer[_index] = message;
                _index = (_index + 1) % _buffer.Length;
                _count = _count < _buffer.Length ? _count + 1 : _count;
            }
        }

        public static string Dump()
        {
            lock (Sync)
            {
                var sb = new StringBuilder(_count * 32);
                int start = (_index - _count + _buffer.Length) % _buffer.Length;
                for (int i = 0; i < _count; i++)
                {
                    int idx = (start + i) % _buffer.Length;
                    sb.AppendLine(_buffer[idx]);
                }
                return sb.ToString();
            }
        }

        public static void Clear()
        {
            lock (Sync)
            {
                _count = 0;
                _index = 0;
            }
        }

        private static void EnsureCapacity()
        {
            int cap = Capacity <= 0 ? 1 : Capacity;
            if (_buffer != null && _buffer.Length == cap)
            {
                return;
            }

            var old = _buffer ?? new string[0];
            var next = new string[cap];

            int toCopy = _count;
            int start = (_index - _count + old.Length) % (old.Length == 0 ? 1 : old.Length);
            for (int i = 0; i < toCopy; i++)
            {
                int idx = (start + i) % old.Length;
                next[i] = old[idx];
            }

            _buffer = next;
            _count = toCopy;
            _index = toCopy % cap;
        }
    }
}

