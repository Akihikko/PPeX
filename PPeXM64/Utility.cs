﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;

namespace PPeXM64
{
    public static class Utility
    {
        public static string GetBytesReadable(long i)
        {
            long absolute_i = (i < 0 ? -i : i);

            string suffix;
            double readable;
            if (absolute_i >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = (i >> 50);
            }
            else if (absolute_i >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = (i >> 40);
            }
            else if (absolute_i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = (i >> 30);
            }
            else if (absolute_i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = (i >> 20);
            }
            else if (absolute_i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = (i >> 10);
            }
            else if (absolute_i >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = i;
            }
            else
            {
                return i.ToString("0 B"); // Byte
            }
            readable = (readable / 1024);

            return readable.ToString("0.### ") + suffix;
        }

        public static string GetGameDir()
        {
            return (string)Microsoft.Win32.Registry.GetValue(
                @"HKEY_CURRENT_USER\SOFTWARE\illusion\AA2Play",
                "INSTALLDIR",
                "");
        }

        public static void CopyTo(this Stream input, Stream output, long offset, int length)
        {
            int remaining = length;
            byte[] buffer = new byte[4096];

            input.Position = offset;

            while (remaining > 0)
            {
                int toRead = Math.Min(remaining, 4096);

                int read = input.Read(buffer, 0, toRead);

                if (read <= 0)
                    return;

                output.Write(buffer, 0, read);

                remaining -= read;
            }
        }
    }
}
