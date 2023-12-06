using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace SampleCameraNet
{
    public class GraphicsBitmapLoader : IBitmapLoader
    {
        GraphicsBitmapLoader() { }

        public static GraphicsBitmapLoader Instance { get; } = new GraphicsBitmapLoader();

        public IBitmapImage CreateBitmapBgr32(Size Size, IntPtr MemoryData, int Stride)
        {
            var bmp = new System.Drawing.Bitmap(Size.Width, Size.Height, Stride, System.Drawing.Imaging.PixelFormat.Format32bppRgb, MemoryData);
            return new DrawingImage(bmp);
        }

        public IBitmapImage LoadBitmap(string FileName)
        {
            var bmp = new System.Drawing.Bitmap(FileName);

            return new DrawingImage(bmp);
        }

        public void Dispose() { }
    }
}