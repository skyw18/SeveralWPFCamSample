using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace SampleCaptura
{
    public class DrawingImage : IBitmapImage
    {
        public Image Image { get; }

        public DrawingImage(Image Image)
        {
            this.Image = Image;
        }

        public void Dispose()
        {
            Image.Dispose();
        }

        public int Width => Image.Width;
        public int Height => Image.Height;

        public void Save(string FileName, ImageFormats Format)
        {
            Image.Save(FileName, ToDrawingImageFormat(Format));
        }

        public Bitmap GetImage()
        {
            return Image as Bitmap;
        }

        public void Save(Stream Stream, ImageFormats Format)
        {
            Image.Save(Stream, ToDrawingImageFormat(Format));
        }



        public static ImageFormat ToDrawingImageFormat(ImageFormats Format)
        {
            switch (Format)
            {
                case ImageFormats.Jpg:
                    return ImageFormat.Jpeg;

                case ImageFormats.Png:
                    return ImageFormat.Png;

                case ImageFormats.Gif:
                    return ImageFormat.Gif;

                case ImageFormats.Bmp:
                    return ImageFormat.Bmp;

                default:
                    return ImageFormat.Png;
            }
        }
    }
}