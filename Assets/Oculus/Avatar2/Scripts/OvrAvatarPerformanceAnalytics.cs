using System;
namespace Oculus.Avatar2
{
    public static class OvrAvatarPerformanceAnalytics
    {
        //:: Constants
        private const string logScope = "performance_analytics";

        private static byte[] toByteArray(string str, ref UInt32 size)
        {
            if (str == null)
            {
                size = 0;
                return null;
            }

            var bytes = System.Text.ASCIIEncoding.ASCII.GetBytes(str.ToCharArray());
            size = (UInt32)bytes.Length;
            return bytes;
        }

        public static void enable(string testAppName, uint approxSampleCount = 0)
        {
            unsafe
            {
                UInt32 size = 0;
                var bytes = toByteArray(testAppName, ref size);
                fixed (byte* ptr = bytes)
                {
                }
            }
        }


        public static bool updateMetric(Int32 metric, double value)
        {
            return false;
        }


        public static bool sendMetric(Int32 metric, double value, string comment = null, byte[] payload = null)
        {
            var payloadSize = payload == null ? 0 : (UInt32)payload.Length;
            UInt32 commentSize = 0;

            unsafe
            {
                var commentBytes = toByteArray(comment, ref commentSize);
                fixed (byte* commentPtr = commentBytes, payloadPtr = payload)
                {
                    return false;
                }
            }
        }


        public static void begin()
        {
        }

    }
}
