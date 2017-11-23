﻿using WatchFace.Utils;

namespace WatchFace.Elements.BasicElements
{
    public class Coordinates
    {
        [RawParameter(Id = 1)]
        public long X { get; set; }

        [RawParameter(Id = 2)]
        public long Y { get; set; }
    }
}