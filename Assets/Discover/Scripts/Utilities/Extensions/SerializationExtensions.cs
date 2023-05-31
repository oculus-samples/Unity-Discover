// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Discover.Utilities.Extensions
{
    public static class SerializationExtensions
    {

        public static byte[] ToByteArray(this object obj)
        {
            var bf = new BinaryFormatter();
            using var ms = new MemoryStream();
            bf.Serialize(ms, obj);
            return ms.ToArray();
        }

        public static T Deserialize<T>(this byte[] bytes)
        {
            using var ms = new MemoryStream();
            var bf = new BinaryFormatter();
            ms.Write(bytes, 0, bytes.Length);
            _ = ms.Seek(0, SeekOrigin.Begin);
            return (T)bf.Deserialize(ms);
        }
    }
}
