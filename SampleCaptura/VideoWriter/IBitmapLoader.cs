using System;
using System.Drawing;

namespace SampleCaptura
{
    public interface IBitmapLoader : IDisposable
    {
        IBitmapImage CreateBitmapBgr32(Size Size, IntPtr MemoryData, int Stride);

        IBitmapImage LoadBitmap(string FileName);
    }
}