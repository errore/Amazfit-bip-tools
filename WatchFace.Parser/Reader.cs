using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using NLog;
using Resources.Models;
using WatchFace.Parser.Models;
using Header = WatchFace.Parser.Models.Header;
using Image = Resources.Models.Image;

namespace WatchFace.Parser
{
    public class Reader
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly Stream _stream;

        public Reader(Stream stream)
        {
            _stream = stream;
        }

        public List<Parameter> Parameters { get; private set; }
        public List<IResource> Resources { get; private set; }
        public Bitmap[] Images => Resources.OfType<Image>().Select(i => i.Bitmap).ToArray();

        public void Read()
        {
            Logger.Trace("阅读头文件...");
            var header = Header.ReadFrom(_stream);
            Logger.Trace("已读:");
            Logger.Trace("已签名: {0}, 未知: {1}, 参数大小: {2}, 错误（非法）: {3}", header.Signature,
                header.Unknown,
                header.ParametersSize, header.IsValid);
            if (!header.IsValid) return;

            Logger.Trace("读取偏移参数...");
            var parametersStream = StreamBlock(_stream, (int) header.ParametersSize);
            Logger.Trace("偏移参数已从文件读取");

            Logger.Trace("读取参数标识符...");
            var mainParam = Parameter.ReadFrom(parametersStream);
            Logger.Trace("参数标识符已读取:");
            var parametrsTableLength = mainParam.Children[0].Value;
            var imagesCount = mainParam.Children[1].Value;
            Logger.Trace($"参数表长: {parametrsTableLength}, 图像数: {imagesCount}");

            Logger.Trace("读取位置参数...");
            var parametersLocations = Parameter.ReadList(parametersStream);
            Logger.Trace("表盘位置参数读取已读取:");

            Parameters = ReadParameters(parametrsTableLength, parametersLocations);
            Resources = new Resources.Reader(_stream).Read((uint) imagesCount);
        }

        private List<Parameter> ReadParameters(long coordinatesTableSize, ICollection<Parameter> parametersDescriptors)
        {
            var parametersStream = StreamBlock(_stream, (int) coordinatesTableSize);

            var result = new List<Parameter>(parametersDescriptors.Count);
            foreach (var prameterDescriptor in parametersDescriptors)
            {
                var descriptorOffset = prameterDescriptor.Children[0].Value;
                var descriptorLength = prameterDescriptor.Children[1].Value;
                Logger.Trace("读取参数标识符 {0}", prameterDescriptor.Id);
                Logger.Trace("标识符偏移: {0}, 标识符长度: {1}", descriptorOffset, descriptorLength);
                parametersStream.Seek(descriptorOffset, SeekOrigin.Begin);
                var descriptorStream = StreamBlock(parametersStream, (int) descriptorLength);
                Logger.Trace("打包参数标识符 {0}...", prameterDescriptor.Id);
                result.Add(new Parameter(prameterDescriptor.Id, Parameter.ReadList(descriptorStream)));
            }
            return result;
        }

        private static Stream StreamBlock(Stream stream, int size)
        {
            var buffer = new byte[size];
            stream.Read(buffer, 0, buffer.Length);
            return new MemoryStream(buffer);
        }
    }
}