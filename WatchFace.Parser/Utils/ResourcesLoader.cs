﻿using NLog;
using Resources;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Resources.Models;
using WatchFace.Parser.Attributes;
using Image = Resources.Models.Image;

namespace WatchFace.Parser.Utils
{
    public class ResourcesLoader
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly string _imagesDirectory;

        private readonly Dictionary<long, long> _mapping;

        public ResourcesLoader(string imagesDirectory)
        {
            Resources = new List<IResource>();
            _mapping = new Dictionary<long, long>();
            _imagesDirectory = imagesDirectory;
        }

        public List<IResource> Resources { get; }
        public Bitmap[] Images => Resources.OfType<Image>().Select(i => i.Bitmap).ToArray();

        public void Process<T>(T serializable, string path = "")
        {
            if (!string.IsNullOrEmpty(path)) Logger.Trace("正在为 {0} '{1}' 加载资源", path, typeof(T).Name);

            long? lastImageIndexValue = null;

            foreach (var kv in ElementsHelper.SortedProperties<T>())
            {
                var id = kv.Key;
                var currentPath = string.IsNullOrEmpty(path)
                    ? id.ToString()
                    : string.Concat(path, '.', id.ToString());

                var propertyInfo = kv.Value;
                var propertyType = propertyInfo.PropertyType;
                dynamic propertyValue = propertyInfo.GetValue(serializable, null);

                var imageIndexAttribute =
                    ElementsHelper.GetCustomAttributeFor<ParameterImageIndexAttribute>(propertyInfo);
                var imagesCountAttribute =
                    ElementsHelper.GetCustomAttributeFor<ParameterImagesCountAttribute>(propertyInfo);

                if (imagesCountAttribute != null && imageIndexAttribute != null)
                    throw new ArgumentException(
                        $"{propertyInfo.Name} 不能同时拥有 ParameterImageIndexAttribute 和 ParameterImagesCountAttribute"
                    );

                if (propertyType == typeof(long) || propertyType.IsGenericType &&
                    (propertyType.GetGenericTypeDefinition() == typeof(List<>) ||
                     propertyType.GetGenericTypeDefinition() == typeof(Nullable<>)))
                {
                    if (imageIndexAttribute != null)
                    {
                        if (propertyValue == null) continue;
                        long imageIndex = propertyValue;

                        lastImageIndexValue = imageIndex;
                        var mappedIndex = LoadImage(imageIndex);
                        propertyInfo.SetValue(serializable, mappedIndex, null);
                    }
                    else if (imagesCountAttribute != null)
                    {
                        if (lastImageIndexValue == null)
                            throw new ArgumentException(
                                $"{propertyInfo.Name} 不能处理，因为 ImageIndex 不存在或为0"
                            );

                        var imagesCount = propertyType.IsGenericType
                            ? (propertyType.GetGenericTypeDefinition() == typeof(Nullable<>)
                                ? propertyValue.Value
                                : propertyValue.Count)
                            : propertyValue;

                        for (var i = lastImageIndexValue + 1; i < lastImageIndexValue + imagesCount; i++)
                            LoadImage(i.Value);
                    }
                }
                else
                {
                    if (imagesCountAttribute == null && imageIndexAttribute == null)
                    {
                        if (propertyValue != null)
                            Process(propertyValue, currentPath);
                    }
                    else
                    {
                        throw new ArgumentException(
                            $"{propertyInfo.Name} 类型 {propertyType.Name} 不能拥有 ParameterImageIndexAttribute 或 ParameterImagesCountAttribute"
                        );
                    }
                }
            }
        }

        private long LoadImage(long index)
        {
            if (_mapping.ContainsKey(index))
                return _mapping[index];

            var newImageIndex = Resources.Count;
            Logger.Trace("加载图像 {0}...", newImageIndex);
            var resource = ImageLoader.LoadResourceForNumber(_imagesDirectory, index);
            Resources.Add(resource);
            _mapping[index] = newImageIndex;
            return newImageIndex;
        }
    }
}