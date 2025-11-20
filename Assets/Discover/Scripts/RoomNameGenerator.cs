// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Text;
using Meta.XR.Samples;
using UnityEngine;

namespace Discover
{
    [MetaCodeSample("Discover")]
    public class RoomNameGenerator
    {
        private const string DATA_SOURCE = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        /// <summary>
        /// Generate a random room name of the given length
        /// </summary>
        /// <param name="length">How many characters should be in the name</param>
        /// <returns></returns>
        public static string GenerateRoom(int length = 6)
        {
            var dataLeght = DATA_SOURCE.Length;
            var sb = new StringBuilder(length);
            for (var i = 0; i < length; ++i)
            {
                var index = Random.Range(0, dataLeght);
                _ = sb.Append(DATA_SOURCE[index]);
            }

            return sb.ToString();
        }
    }
}