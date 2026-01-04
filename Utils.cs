using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TinyVolumeAdjuster
{
    static class NativeMethods
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern uint ExtractIconEx(
            string szFileName,
            int nIconIndex,
            IntPtr[]? phiconLarge,
            IntPtr[]? phiconSmall,
            uint nIcons
        );

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);

        public const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(
            int access,
            bool inherit,
            int processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool QueryFullProcessImageName(
            IntPtr hProcess,
            int flags,
            StringBuilder text,
            ref int size);

        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern int SHLoadIndirectString(
                string pszSource,
                StringBuilder pszOutBuf,
                int cchOutBuf,
                IntPtr pvReserved);
    }

    class Utils
    {
        public static ImageSource? LoadIcon(string iconPath, bool large = false)
        {
            var parts = iconPath.Split(',');
            string file = parts[0];
            int index = parts.Length > 1 ? int.Parse(parts[1]) : 0;

            IntPtr[]? largeIcons = large ? new IntPtr[1] : null;
            IntPtr[]? smallIcons = large ? null : new IntPtr[1];

            NativeMethods.ExtractIconEx(
                file,
                index,
                largeIcons,
                smallIcons,
                1
            );

            IntPtr hIcon = large
                ? largeIcons![0]
                : smallIcons![0];

            if (hIcon == IntPtr.Zero)
                return null;

            try
            {
                using var icon = Icon.FromHandle(hIcon);
                return Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    System.Windows.Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions()
                );
            }
            finally
            {
                NativeMethods.DestroyIcon(hIcon);
            }
        }


        public static string? GetProcessPath(int pid)
        {
            var h = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (h == IntPtr.Zero)
                return null;

            try
            {
                var sb = new StringBuilder(260);
                int size = sb.Capacity;
                return NativeMethods.QueryFullProcessImageName(h, 0, sb, ref size)
                    ? sb.ToString()
                    : null;
            }
            finally
            {
                NativeMethods.CloseHandle(h);
            }
        }

        public static ImageSource? LoadExeIcon(string exePath)
        {
            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon == null)
                return null;

            return Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(24, 24)
            );
        }

        public static string ResolveIndirectString(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return raw;

            raw = raw.Trim();

            // 不是 indirect string，直接返回
            if (!raw.StartsWith("@"))
                return raw;

            var sb = new StringBuilder(260);
            int hr = NativeMethods.SHLoadIndirectString(
                raw,
                sb,
                sb.Capacity,
                IntPtr.Zero);

            return hr == 0 ? sb.ToString() : raw;
        }

        public static string NormalizeIconPath(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            raw = raw.Trim();

            if (raw.StartsWith("@"))
                raw = raw.Substring(1);

            raw = Environment.ExpandEnvironmentVariables(raw);

            return raw;
        }
    }
}
