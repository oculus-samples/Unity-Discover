// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using System.Linq;
using Meta.XR.Samples;

namespace Discover.Networking
{
    [MetaCodeSample("Discover")]
    public static class RegionMapping
    {
        public enum Regions
        {
            BEST_REGION = 0,
            ASIA,
            CHINA,
            JAPAN,
            EUROPE,
            SOUTH_AMERICA,
            SOUTH_KOREA,
            USA_EAST,
            USA_WEST,
            COUNT
        }

        public static Dictionary<Regions, string> RegionsToName = new()
        {
            { Regions.BEST_REGION, "Best Region" },
            { Regions.ASIA, "Asia" },
            { Regions.CHINA, "China" },
            { Regions.JAPAN, "Japan" },
            { Regions.EUROPE, "Europe" },
            { Regions.SOUTH_AMERICA, "South America" },
            { Regions.SOUTH_KOREA, "South Korea" },
            { Regions.USA_EAST, "USA East" },
            { Regions.USA_WEST, "USA West" },
        };

        public static Dictionary<Regions, string> RegionsToCode = new()
        {
            { Regions.BEST_REGION, null },
            { Regions.ASIA, "asia" },
            { Regions.CHINA, "cn" },
            { Regions.JAPAN, "jp" },
            { Regions.EUROPE, "eu" },
            { Regions.SOUTH_AMERICA, "sa" },
            { Regions.SOUTH_KOREA, "kr" },
            { Regions.USA_EAST, "us" },
            { Regions.USA_WEST, "usw" },
        };

        public static string CodeToName(string code)
        {
            var region = RegionsToCode.First(v => v.Value == code).Key;
            return RegionsToName[region];
        }
    }
}