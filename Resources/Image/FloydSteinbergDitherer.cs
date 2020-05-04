using System.Drawing;
using BumpKit;
using NLog;

namespace Resources.Image
{
    public class FloydSteinbergDitherer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static void Process(Bitmap image)
        {
            var isImageAltered = false;
            using (var context = image.CreateUnsafeContext())
            {
                for (var y = 0; y < context.Height; y++)
                for (var x = 0; x < context.Width; x++)
                {
                    var color = context.GetPixel(x, y);
                    var colorError = new ColorError(color);

                    if (colorError.IsZero) continue;

                    if (!isImageAltered)
                    {
                        Logger.Warn(
                            "应用图像抖动。监视面板中的资源将仅使用支持的颜色。不能通过打开表盘来恢复原始图像。"
                        );
                        isImageAltered = true;
                    }

                    context.SetPixel(x, y, colorError.NewColor);

                    if (x + 1 < context.Width)
                    {
                        color = context.GetPixel(x + 1, y);
                        context.SetPixel(x + 1, y, colorError.ApplyError(color, 7, 16));
                    }

                    if (y + 1 < context.Height)
                    {
                        if (x > 1)
                        {
                            color = context.GetPixel(x - 1, y + 1);
                            context.SetPixel(x - 1, y + 1, colorError.ApplyError(color, 3, 16));
                        }

                        color = context.GetPixel(x, y + 1);
                        context.SetPixel(x, y + 1, colorError.ApplyError(color, 5, 16));

                        if (x < context.Width - 1)
                        {
                            color = context.GetPixel(x + 1, y + 1);
                            context.SetPixel(x + 1, y + 1, colorError.ApplyError(color, 1, 16));
                        }
                    }
                }
            }
        }
    }
}