// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Text;
using Meta.XR.Samples;
using UnityEngine;

namespace Discover.SpatialAnchors
{
    [MetaCodeSample("Discover")]
    public class AnchorUtils
    {
        // Converts Byte array to string with Uuid format "XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX"
        public static string UuidToString(byte[] encodedMessage)
        {
            if (encodedMessage.Length != 16)
            {
                Debug.Log("UuidToString failed because uuid byte array was incorrect length: " + encodedMessage.Length);
                return "";
            }

            var message = new StringBuilder("", 36);
            var messageWithNoDashes =
                BitConverter.ToString(encodedMessage).ToLower().Replace('-'.ToString(), string.Empty);
            for (var iter = 0; iter < 32; iter++)
            {
                if (iter is 8 or 12 or 16 or 20)
                {
                    _ = message.Append("-");
                }

                _ = message.Append(messageWithNoDashes[iter]);
            }

            return message.ToString();
        }

        public static string GuidToString(Guid uuid)
        {
            return UuidToString(uuid.ToByteArray());
        }
    }
}