﻿using System;
using System.Runtime.InteropServices;

namespace SampleCaptura
{
    public class Kernel32
    {
        const string DllName = "Kernel32";

        [DllImport(DllName)]
        public static extern void CopyMemory(IntPtr Dest, IntPtr Src, int Count);
    }
}