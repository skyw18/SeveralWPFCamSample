using System;
using System.Drawing;

namespace SampleCameraNet
{
    public interface IBitmapLoader : IDisposable
    {
        IBitmapImage CreateBitmapBgr32(Size Size, IntPtr MemoryData, int Stride);

        IBitmapImage LoadBitmap(string FileName);
    }
}