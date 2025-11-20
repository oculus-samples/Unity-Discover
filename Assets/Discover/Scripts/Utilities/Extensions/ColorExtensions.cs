// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using UnityEngine;

namespace Discover.Utilities.Extensions
{
    [MetaCodeSample("Discover")]
    public static class ColorExtensions
    {

        public static float GetHue(this Color color)
        {
            Color.RGBToHSV(color, out var hue, out _, out _);
            return hue;
        }

        public static float GetSaturation(this Color color)
        {
            Color.RGBToHSV(color, out _, out var saturation, out _);
            return saturation;
        }

        public static float GetValue(this Color color)
        {
            Color.RGBToHSV(color, out _, out _, out var value);
            return value;
        }

        public static Vector3 GetHSV(this Color color)
        {
            Color.RGBToHSV(color, out var hue, out var saturation, out var value);
            return new Vector3(hue, saturation, value);
        }

        public static Color SetRGB(ref this Color color, float r, float g, float b)
        {
            color.r = r;
            color.g = g;
            color.b = b;
            return color;
        }

        public static Color SetRGBA(ref this Color color, float r, float g, float b, float a)
        {
            color.r = r;
            color.g = g;
            color.b = b;
            color.a = a;
            return color;
        }

        public static Color SetRGB(ref this Color color, Color other)
        {
            color.r = other.r;
            color.g = other.g;
            color.b = other.b;
            return color;
        }

        public static Color SetRGBA(ref this Color color, Color other)
        {
            color.r = other.r;
            color.g = other.g;
            color.b = other.b;
            color.a = other.a;
            return color;
        }

        public static Color SetRGB(ref this Color color, Vector3 vec)
        {
            color.r = vec.x;
            color.g = vec.y;
            color.b = vec.z;
            return color;
        }

        public static Color SetRGBA(ref this Color color, Vector4 vec)
        {
            color.r = vec.x;
            color.g = vec.y;
            color.b = vec.z;
            color.a = vec.w;
            return color;
        }

        public static Color SetHSV(ref this Color color, float h, float s, float v)
        {
            var rgb = Color.HSVToRGB(h, s, v);
            _ = color.SetRGB(rgb);
            return color;
        }

        public static Color SetHSVA(ref this Color color, float h, float s, float v, float a)
        {
            _ = color.SetHSV(h, s, v);
            color.a = a;
            return color;
        }

        public static Color SetHSV(ref this Color color, Vector3 vec)
        {
            _ = color.SetHSV(vec.x, vec.y, vec.z);
            return color;
        }

        public static Color SetHSVA(ref this Color color, Vector4 vec)
        {
            _ = color.SetHSV(vec.x, vec.y, vec.z);
            color.a = vec.w;
            return color;
        }

        public static Color SetHue(ref this Color color, float hue)
        {
            var hsv = color.GetHSV();
            hsv.x = hue;
            return color.SetHSV(hsv);
        }

        public static Color SetSaturation(ref this Color color, float saturation)
        {
            var hsv = color.GetHSV();
            hsv.y = saturation;
            return color.SetHSV(hsv);
        }

        public static Color SetValue(ref this Color color, float value)
        {
            var hsv = color.GetHSV();
            hsv.z = value;
            return color.SetHSV(hsv);
        }
    }
}
