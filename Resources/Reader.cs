using System.Collections.Generic;
using System.IO;
using NLog;
using Resources.Models;

namespace Resources
{
    public class Reader
    {
        private const int OffsetTableItemLength = 4;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly BinaryReader _binaryReader;
        private readonly Stream _stream;

        public Reader(Stream stream)
        {
            _stream = stream;
            _binaryReader = new BinaryReader(_stream);
        }

        public List<IResource> Read(uint resourcesCount)
        {
            var offsetsTableLength = (int) (resourcesCount * OffsetTableItemLength);
            Logger.Trace("读取资源偏移表 {0} 元素 ({1} 字节)",
                resourcesCount, offsetsTableLength
            );

            var offsets = new int[resourcesCount];
            for (var i = 0; i < resourcesCount; i++)
                offsets[i] = _binaryReader.ReadInt32();

            var resourcesOffset = (int) _stream.Position;
            var fileSize = (int) _stream.Length;

            Logger.Debug("读取 {0} 资源...", resourcesCount);
            var resources = new List<IResource>((int) resourcesCount);
            for (var i = 0; i < resourcesCount; i++)
            {
                var offset = offsets[i] + resourcesOffset;
                var nextOffset = i + 1 < resourcesCount ? offsets[i + 1] + resourcesOffset : fileSize;
                var length = nextOffset - offset;
                Logger.Trace("资源 {0} 偏移: {1}, 长度: {2}...", i, offset, length);
                if (_stream.Position != offset)
                {
                    var bytesGap = offset - _stream.Position;
                    Logger.Warn("在资源 {0} 发现 {1} 字节间隙",  i,bytesGap);
                    _stream.Seek(offset, SeekOrigin.Begin);
                }

                Logger.Debug("读取资源 {0}...", i);
                try
                {
                    var bitmap = new Image.Reader(_stream).Read();
                    var image = new Models.Image(bitmap);
                    resources.Add(image);
                }
                catch (InvalidResourceException)
                {
                    Logger.Warn("资源不是图像🙃！");
                    _stream.Seek(offset, SeekOrigin.Begin);
                    var data = new byte[length];
                    _stream.Read(data, 0, length);
                    var blob = new Blob(data);
                    resources.Add(blob);
                }
            }

            return resources;
        }
    }
}