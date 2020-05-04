using System;
using System.IO;
using System.Text;
using NLog;
using Resources.Models;

namespace Resources
{
    public class FileReader
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static FileDescriptor Read(Stream stream)
        {
            var binaryReader = new BinaryReader(stream);

            var signature = Encoding.ASCII.GetString(binaryReader.ReadBytes(5));
            Logger.Trace("资源签名已读:");
            stream.Seek(0, SeekOrigin.Begin);
            Logger.Trace("签名: {0}", signature);

            Header header;
            switch (signature) {
                case Header.ResSignature:
                    header = Header.ReadFrom(binaryReader);
                    break;
                case NewHeader.ResSignature:
                    header = NewHeader.ReadFrom(binaryReader);
                    break;
                default:
                    throw new ArgumentException($"签名 '{signature}' 无法识别");
            }

            Logger.Trace("资源头文件已读:");
            Logger.Trace("版本: {0}, 资源计数: {1}", header.Version, header.ResourcesCount);

            return new FileDescriptor
            {
                HasNewHeader = header is NewHeader,
                ResourcesCount = header.ResourcesCount,
                Version = header.Version,
                Unknown = (header as NewHeader)?.Unknown,
                Resources = new Reader(stream).Read(header.ResourcesCount)
            };
        }
    }
}