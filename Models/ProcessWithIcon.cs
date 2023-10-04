using System.Diagnostics;
using System.Windows.Media.Imaging;

namespace NetLamer.Models
{
    public class ProcessWithIcon
    {

        public Process? Process { get; private set; }
        public BitmapSource? BitmapSource { get; private set; }
        public bool IsLimited { get; private set; }

        public ProcessWithIcon(Process? process, BitmapSource? bitmapSource, bool isLimited)
        {
            Process = process;
            BitmapSource = bitmapSource;
            IsLimited = isLimited;
        }

        public void UpdateProcess(Process? process, BitmapSource? bitmapSource, bool isLimited)
        {
            Process = process;
            BitmapSource = bitmapSource;
            IsLimited = isLimited;
        }
    }
}