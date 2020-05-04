using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using BumpKit;
using NLog;
using Resources.Utils;

namespace Resources.Image
{
    public class Writer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly byte[] Signature = {(byte) 'B', (byte) 'M', (byte) 'd', 0};
        private readonly List<Color> _palette;

        private readonly BinaryWriter _writer;

        private ushort _bitsPerPixel;
        private ushort _height;
        private Bitmap _image;
        private ushort _paletteColors;
        private ushort _rowLengthInBytes;
        private ushort _transparency;
        private ushort _width;

        public Writer(Stream stream)
        {
            _writer = new BinaryWriter(stream);
            _palette = new List<Color>();
        }

        public void Write(Bitmap image)
        {
            _image = image;
            _width = (ushort) image.Width;
            _height = (ushort) image.Height;

            ExtractPalette();

            if (_bitsPerPixel == 3) _bitsPerPixel = 4;
            if (_bitsPerPixel == 0) _bitsPerPixel = 1;

            if (_bitsPerPixel > 4)
                throw new ArgumentException(
                    $"图片有 {_bitsPerPixel} bit/像素，不能用作表盘，似乎图像不正确*( ￣皿￣)/#"
                );

            _rowLengthInBytes = (ushort) Math.Ceiling(_width * _bitsPerPixel / 8.0);

            _writer.Write(Signature);

            WriteHeader();
            WritePalette();
            WriteImage();
        }

        private void ExtractPalette()
        {
            Logger.Trace("提取调色板...");

            using (var context = _image.CreateUnsafeContext())
            {
                for (var y = 0; y < _height; y++)
                for (var x = 0; x < _width; x++)
                {
                    var color = context.GetPixel(x, y);
                    if (_palette.Contains(color)) continue;

                    if (color.A < 0x80 && _transparency == 0)
                    {
                        Logger.Trace("调色板元件 {0}: R {1:X2}, G {2:X2}, B {3:X2}, 透明颜色",
                            _palette.Count, color.R, color.G, color.B
                        );
                        _palette.Insert(0, color);
                        _transparency = 1;
                    }
                    else
                    {
                        Logger.Trace("调色板元件 {0}: R {1:X2}, G {2:X2}, B {3:X2}",
                            _palette.Count, color.R, color.G, color.B
                        );
                        _palette.Add(color);
                    }
                }
            }

            var startIndex = _transparency == 0 ? 0 : 1;

            for (var i = startIndex; i < _palette.Count - 1; i++)
            {
                var minColor = (uint) _palette[i].ToArgb();
                var minIndex = i;
                for (var j = i + 1; j < _palette.Count; j++)
                {
                    var color = (uint) _palette[j].ToArgb();
                    if (color >= minColor) continue;

                    minColor = color;
                    minIndex = j;
                }

                if (minIndex == i) continue;

                var tmp = _palette[i];
                _palette[i] = _palette[minIndex];
                _palette[minIndex] = tmp;
            }

            _paletteColors = (ushort) _palette.Count;
            _bitsPerPixel = (ushort) Math.Ceiling(Math.Log(_paletteColors, 2));
        }

        private void WriteHeader()
        {
            Logger.Trace("写入图片头文件...");
            Logger.Trace("宽: {0}, 高: {1}, 行长度: {2}", _width, _height, _rowLengthInBytes);
            Logger.Trace("bit/像素: {0}, 色板颜色: {1}, 透明: {2}",
                _bitsPerPixel, _paletteColors, _transparency
            );

            _writer.Write(_width);
            _writer.Write(_height);
            _writer.Write(_rowLengthInBytes);
            _writer.Write(_bitsPerPixel);
            _writer.Write(_paletteColors);
            _writer.Write(_transparency);
        }

        private void WritePalette()
        {
            Logger.Trace("写入调色板...");
            foreach (var color in _palette)
            {
                _writer.Write(color.R);
                _writer.Write(color.G);
                _writer.Write(color.B);
                _writer.Write((byte) 0); // always 0 maybe padding
            }
        }

        private void WriteImage()
        {
            Logger.Trace("写入图像...");

            var paletteHash = new Dictionary<Color, byte>();
            byte i = 0;
            foreach (var color in _palette)
            {
                paletteHash[color] = i;
                i++;
            }

            using (var context = _image.CreateUnsafeContext())
            {
                for (var y = 0; y < _height; y++)
                {
                    var rowData = new byte[_rowLengthInBytes];
                    var memoryStream = new MemoryStream(rowData);
                    var bitWriter = new BitWriter(memoryStream);
                    for (var x = 0; x < _width; x++)
                    {
                        var color = context.GetPixel(x, y);
                        if (color.A < 0x80 && _transparency == 1)
                        {
                            bitWriter.WriteBits(0, _bitsPerPixel);
                        }
                        else
                        {
                            var paletteIndex = paletteHash[color];
                            bitWriter.WriteBits(paletteIndex, _bitsPerPixel);
                        }
                    }

                    bitWriter.Flush();
                    _writer.Write(rowData);
                }
            }
        }
    }
}