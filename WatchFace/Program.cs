using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using BumpKit;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Targets;
using Resources;
using Resources.Models;
using WatchFace.Parser;
using WatchFace.Parser.Models;
using WatchFace.Parser.Utils;
using Image = System.Drawing.Image;
using Reader = WatchFace.Parser.Reader;
using Writer = WatchFace.Parser.Writer;

namespace WatchFace
{
    internal class Program
    {
        private const string AppName = "WatchFace";

        private static readonly bool IsRunningOnMono = Type.GetType("Mono.Runtime") != null;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private static void Main(string[] args)
        {
            if (args.Length == 0 || args[0] == null)
            {
                Console.WriteLine(
                    "{0}.exe 可以用来打包解包您的米环资源包、表盘（Error_E翻译，原作者：Valeriy Nironov）", AppName);
                Console.WriteLine();
                Console.WriteLine("举几个栗子:");
                Console.WriteLine("  {0}.exe watchface.bin   - 解包表盘配置和文件", AppName);
                Console.WriteLine("  {0}.exe watchface.json  - 打包表盘（通过json），请勿导入资源包内json",
                    AppName);
                Console.WriteLine("  {0}.exe mili_chaohu.res - 解包资源包", AppName);
                Console.WriteLine("  {0}.exe mili_chaohu     - 打包资源包（通过文件夹）", AppName);
                return;
            }

            if (args.Length > 1)
                Console.WriteLine("混合文件解包");

            foreach (var inputFileName in args)
            {
                var isDirectory = Directory.Exists(inputFileName);
                var isFile = File.Exists(inputFileName);
                if (!isDirectory && !isFile)
                {
                    Console.WriteLine("文件或目录 '{0}' 不存在╯︿╰", inputFileName);
                    continue;
                }

                if (isDirectory)
                {
                    Console.WriteLine("处理目录 '{0}'", inputFileName);
                    try
                    {
                        PackResources(inputFileName);
                    }
                    catch (Exception e)
                    {
                        Logger.Fatal(e);
                    }

                    continue;
                }

                Console.WriteLine("处理文件 '{0}'", inputFileName);
                var inputFileExtension = Path.GetExtension(inputFileName);
                try
                {
                    switch (inputFileExtension)
                    {
                        case ".bin":
                            UnpackWatchFace(inputFileName);
                            break;
                        case ".json":
                            PackWatchFace(inputFileName);
                            break;
                        case ".res":
                            UnpackResources(inputFileName);
                            break;
                        default:
                            Console.WriteLine("这是什么操作！？？不支持扩展名 {0}.", inputFileExtension);
                            Console.WriteLine("令人窒息QwQ");
                            break;
                    }
                }
                catch (Exception e)
                {
                    Logger.Fatal(e);
                }
            }
        }

        private static void PackWatchFace(string inputFileName)
        {
            var baseName = Path.GetFileNameWithoutExtension(inputFileName);
            var outputDirectory = Path.GetDirectoryName(inputFileName);
            var outputFileName = Path.Combine(outputDirectory, baseName + "_packed.bin");
            SetupLogger(Path.ChangeExtension(outputFileName, ".log"));

            var watchFace = ReadWatchFaceConfig(inputFileName);

            if (watchFace == null)
            {
                Logger.Fatal("道路千万条，安全第一条(ノ｀Д)ノ");
                Logger.Fatal("打包不规范，用户两行泪___*( ￣皿￣)/#____");
                Logger.Fatal("您可能导入了资源包json");
                throw new Exception("似乎json不正确，请确保导入正确的json");
            }

            var imagesDirectory = Path.GetDirectoryName(inputFileName);
            try
            {
                WriteWatchFace(outputDirectory, outputFileName, imagesDirectory, watchFace);
            }
            catch (Exception)
            {
                File.Delete(outputFileName);
                throw;
            }
        }

        private static void UnpackWatchFace(string inputFileName)
        {
            var outputDirectory = CreateOutputDirectory(inputFileName);
            var baseName = Path.GetFileNameWithoutExtension(inputFileName);
            SetupLogger(Path.Combine(outputDirectory, $"{baseName}.log"));

            var reader = ReadWatchFace(inputFileName);
            if (reader == null) return;

            var watchFace = ParseResources(reader);
            if (watchFace == null) return;

            GeneratePreviews(reader.Parameters, reader.Images, outputDirectory, baseName);

            Logger.Debug("导出到 '{0}'", outputDirectory);
            var reDescriptor = new FileDescriptor {Resources = reader.Resources};
            new Extractor(reDescriptor).Extract(outputDirectory);
            ExportWatchFaceConfig(watchFace, Path.Combine(outputDirectory, $"{baseName}.json"));
        }

        private static void PackResources(string inputDirectory)
        {
            var outputDirectory = Path.GetDirectoryName(inputDirectory);
            var baseName = Path.GetFileName(inputDirectory);
            var outputFileName = Path.Combine(outputDirectory, $"{baseName}_packed.res");
            var logFileName = Path.Combine(outputDirectory, $"{baseName}_packed.log");
            SetupLogger(logFileName);

            FileDescriptor resDescriptor;
            var headerFileName = Path.Combine(inputDirectory, "header.json");
            var versionFileName = Path.Combine(inputDirectory, "version");
            if (File.Exists(headerFileName))
            {
                resDescriptor = ReadResConfig(headerFileName);
            }
            else if (File.Exists(versionFileName))
            {
                resDescriptor = new FileDescriptor();
                using (var stream = File.OpenRead(versionFileName))
                using (var reader = new BinaryReader(stream))
                {
                    resDescriptor.Version = reader.ReadByte();
                }
            }
            else
            {
                throw new ArgumentException(
                    "'header.json' 或 'version' 必须在文件内！"
                );
            }

            var i = 0;
            var images = new List<IResource>();
            while (resDescriptor.ResourcesCount == null || i < resDescriptor.ResourcesCount.Value)
            {
                try
                {
                    var resource = ImageLoader.LoadResourceForNumber(inputDirectory, i);
                    images.Add(resource);
                }
                catch (FileNotFoundException)
                {
                    Logger.Info("所有图片序列已装载， 最新装载: {0}", i - 1);
                    break;
                }

                i++;
            }

            if (resDescriptor.ResourcesCount != null && resDescriptor.ResourcesCount.Value != images.Count)
                throw new ArgumentException(
                    $"res应该包括 {resDescriptor.ResourcesCount.Value} 图像，但是你导入了 {images.Count} 图像");

            resDescriptor.Resources = images;

            using (var stream = File.OpenWrite(outputFileName))
            {
                new FileWriter(stream).Write(resDescriptor);
            }
        }

        private static void UnpackResources(string inputFileName)
        {
            var outputDirectory = CreateOutputDirectory(inputFileName);
            var baseName = Path.GetFileNameWithoutExtension(inputFileName);
            SetupLogger(Path.Combine(outputDirectory, $"{baseName}.log"));

            FileDescriptor resDescriptor;
            using (var stream = File.OpenRead(inputFileName))
            {
                resDescriptor = FileReader.Read(stream);
            }

            ExportResConfig(resDescriptor, Path.Combine(outputDirectory, "header.json"));
            new Extractor(resDescriptor).Extract(outputDirectory);
        }

        private static void WriteWatchFace(string outputDirectory, string outputFileName, string imagesDirectory, Parser.WatchFace watchFace)
        {
            try
            {
                Logger.Debug("从 '{0}' 读取参考图像", imagesDirectory);
                var imagesReader = new ResourcesLoader(imagesDirectory);
                imagesReader.Process(watchFace);

                Logger.Trace("建构表盘参数...");
                var descriptor = ParametersConverter.Build(watchFace);

                var baseFilename = Path.GetFileNameWithoutExtension(outputFileName);
                GeneratePreviews(descriptor, imagesReader.Images, outputDirectory, baseFilename);

                Logger.Debug("写入表盘到： '{0}'", outputFileName);
                using (var fileStream = File.OpenWrite(outputFileName))
                {
                    var writer = new Writer(fileStream, imagesReader.Resources);
                    writer.Write(descriptor);
                    fileStream.Flush();
                }
            }
            catch (Exception e)
            {
                Logger.Fatal(e);
                File.Delete(outputFileName);
            }
        }

        private static Reader ReadWatchFace(string inputFileName)
        {
            Logger.Debug("打开表盘 '{0}'", inputFileName);
            try
            {
                using (var fileStream = File.OpenRead(inputFileName))
                {
                    var reader = new Reader(fileStream);
                    Logger.Debug("读取参数...");
                    reader.Read();
                    return reader;
                }
            }
            catch (Exception e)
            {
                Logger.Fatal(e);
                return null;
            }
        }

        private static Parser.WatchFace ParseResources(Reader reader)
        {
            Logger.Debug("打包参数...");

            try
            {
                return ParametersConverter.Parse<Parser.WatchFace>(reader.Parameters);
            }
            catch (Exception e)
            {
                Logger.Fatal(e);
                return null;
            }
        }

        private static string CreateOutputDirectory(string originalFileName)
        {
            var path = Path.GetDirectoryName(originalFileName);
            var name = Path.GetFileNameWithoutExtension(originalFileName);
            var unpackedPath = Path.Combine(path, $"{name}");
            if (!Directory.Exists(unpackedPath)) Directory.CreateDirectory(unpackedPath);
            return unpackedPath;
        }

        private static Parser.WatchFace ReadWatchFaceConfig(string jsonFileName)
        {
            Logger.Debug("读取配置...");
            try
            {
                using (var fileStream = File.OpenRead(jsonFileName))
                using (var reader = new StreamReader(fileStream))
                {
                    return JsonConvert.DeserializeObject<Parser.WatchFace>(reader.ReadToEnd(),
                        new JsonSerializerSettings
                        {
                            MissingMemberHandling = MissingMemberHandling.Error,
                            NullValueHandling = NullValueHandling.Ignore
                        });
                }
            }
            catch (Exception e)
            {
                Logger.Fatal(e);
                return null;
            }
        }

        private static FileDescriptor ReadResConfig(string jsonFileName)
        {
            Logger.Debug("读取资源配置...");
            try
            {
                using (var fileStream = File.OpenRead(jsonFileName))
                using (var reader = new StreamReader(fileStream))
                {
                    return JsonConvert.DeserializeObject<FileDescriptor>(reader.ReadToEnd(),
                        new JsonSerializerSettings
                        {
                            MissingMemberHandling = MissingMemberHandling.Error,
                            NullValueHandling = NullValueHandling.Ignore
                        });
                }
            }
            catch (Exception e)
            {
                Logger.Fatal(e);
                return null;
            }
        }

        private static void ExportWatchFaceConfig(Parser.WatchFace watchFace, string jsonFileName)
        {
            Logger.Debug("导出配置...");
            try
            {
                using (var fileStream = File.OpenWrite(jsonFileName))
                using (var writer = new StreamWriter(fileStream))
                {
                    writer.Write(JsonConvert.SerializeObject(watchFace, Formatting.Indented,
                        new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore}));
                    writer.Flush();
                }
            }
            catch (Exception e)
            {
                Logger.Fatal(e);
            }
        }

        private static void ExportResConfig(FileDescriptor resDescriptor, string jsonFileName)
        {
            Logger.Debug("导出资源配置...");
            try
            {
                using (var fileStream = File.OpenWrite(jsonFileName))
                using (var writer = new StreamWriter(fileStream))
                {
                    writer.Write(JsonConvert.SerializeObject(resDescriptor, Formatting.Indented,
                        new JsonSerializerSettings {NullValueHandling = NullValueHandling.Ignore}));
                    writer.Flush();
                }
            }
            catch (Exception e)
            {
                Logger.Fatal(e);
            }
        }

        private static void SetupLogger(string logFileName)
        {
            var config = new LoggingConfiguration();

            var fileTarget = new FileTarget
            {
                FileName = logFileName,
                Layout = "${level}|${message}",
                KeepFileOpen = true,
                ConcurrentWrites = false,
                OpenFileCacheTimeout = 30
            };
            config.AddTarget("file", fileTarget);
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, fileTarget));

            var consoleTarget = new ColoredConsoleTarget {Layout = @"${message}"};
            config.AddTarget("console", consoleTarget);
            config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, consoleTarget));

            LogManager.Configuration = config;
        }

        private static void GeneratePreviews(List<Parameter> parameters, Bitmap[] images, string outputDirectory, string baseName)
        {
            Logger.Debug("生成预览...");

            var states = GetPreviewStates();
            var staticPreview = PreviewGenerator.CreateImage(parameters, images, new WatchState());
            staticPreview.Save(Path.Combine(outputDirectory, $"{baseName}_static.png"), ImageFormat.Png);

            var previewImages = PreviewGenerator.CreateAnimation(parameters, images, states);

            if (IsRunningOnMono)
            {
                var i = 0;
                foreach (var previewImage in previewImages)
                {
                    previewImage.Save(Path.Combine(outputDirectory, $"{baseName}_animated_{i}.png"), ImageFormat.Png);
                    i++;
                }
            }
            else
            {
                using (var gifOutput = File.OpenWrite(Path.Combine(outputDirectory, $"{baseName}_animated.gif")))
                using (var encoder = new GifEncoder(gifOutput))
                {
                    foreach (var previewImage in previewImages)
                        encoder.AddFrame(previewImage, frameDelay: TimeSpan.FromSeconds(1));
                }
            }
        }

        private static IEnumerable<WatchState> GetPreviewStates()
        {
            var appPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var previewStatesPath = Path.Combine(appPath, "PreviewStates.json");

            if (File.Exists(previewStatesPath))
                using (var stream = File.OpenRead(previewStatesPath))
                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    return JsonConvert.DeserializeObject<List<WatchState>>(json);
                }

            var previewStates = GenerateSampleStates();
            using (var stream = File.OpenWrite(previewStatesPath))
            using (var writer = new StreamWriter(stream))
            {
                var json = JsonConvert.SerializeObject(previewStates, Formatting.Indented);
                writer.Write(json);
                writer.Flush();
            }

            return previewStates;
        }

        private static IEnumerable<WatchState> GenerateSampleStates()
        {
            var time = DateTime.Now;
            var states = new List<WatchState>();

            for (var i = 0; i < 10; i++)
            {
                var num = i + 1;
                var watchState = new WatchState
                {
                    BatteryLevel = 100 - i * 10,
                    Pulse = 60 + num * 2,
                    Steps = num * 1000,
                    Calories = num * 75,
                    Distance = num * 700,
                    Bluetooth = num > 1 && num < 6,
                    Unlocked = num > 2 && num < 7,
                    Alarm = num > 3 && num < 8,
                    DoNotDisturb = num > 4 && num < 9,

                    DayTemperature = -15 + 2 * i,
                    NightTemperature = -24 + i * 4,
                };

                if (num < 3)
                {
                    watchState.AirQuality = AirCondition.Unknown;
                    watchState.AirQualityIndex = null;

                    watchState.CurrentWeather = WeatherCondition.Unknown;
                    watchState.CurrentTemperature = null;
                }
                else
                {
                    var index = num - 2;
                    watchState.AirQuality = (AirCondition) index;
                    watchState.CurrentWeather = (WeatherCondition) index;

                    watchState.AirQualityIndex = index * 50 - 25;
                    watchState.CurrentTemperature = -10 + i * 6;
                }

                watchState.Time = new DateTime(time.Year, num, num * 2 + 5, i * 2, i * 6, i);
                states.Add(watchState);
            }

            return states;
        }
    }
}