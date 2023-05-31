/*
WawWriter based on https://github.com/filoe/cscore library

Copy of https://github.com/filoe/cscore/blob/master/license.md :

Microsoft Public License (Ms-PL)
This license governs use of the accompanying software. If you use the software, you accept this license. If you do not accept the license, do not use the software.

1. Definitions
The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here as under U.S. copyright law.
A "contribution" is the original software, or any additions or changes to the software.
A "contributor" is any person that distributes its contribution under this license.
"Licensed patents" are a contributor's patent claims that read directly on its contribution.

2. Grant of Rights
(A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.
(B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the contribution in the software.

3. Conditions and Limitations
(A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.
(B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, your patent license from such contributor to the software ends automatically.
(C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution notices that are present in the software.
(D) If you distribute any portion of the software in source code form, you may do so only under this license by including a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object code form, you may only do so under a license that complies with this license.
(E) The software is licensed "as-is." You bear the risk of using it.The contributors give no express warranties, guarantees or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness for a particular purpose and non-infringement.

*/

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Photon.Voice.Unity.UtilityScripts
{
    public class WaveWriter : IDisposable
    {
        private readonly long _waveStartPosition;
        private int _dataLength;
        private bool _isDisposed;
        private Stream _stream;
        private BinaryWriter _writer;

        int _sampleRate;
        int _bitsPerSample;
        int _channels;

        private readonly bool _closeStream;

        public WaveWriter(string fileName, int sampleRate, int bits, int channels)
            : this(File.OpenWrite(fileName), sampleRate, bits, channels)
        {
            _closeStream = true;
        }

        public WaveWriter(Stream stream, int sampleRate, int bitsPerSample, int channels)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (!stream.CanWrite)
                throw new ArgumentException("Stream not writeable.", "stream");
            if (!stream.CanSeek)
                throw new ArgumentException("Stream not seekable.", "stream");

            _sampleRate = sampleRate;
            _bitsPerSample = bitsPerSample;
            _channels = channels;

            _stream = stream;
            _waveStartPosition = stream.Position;
            _writer = new BinaryWriter(stream);
            for (int i = 0; i < 44; i++)
            {
                _writer.Write((byte)0);
            }
            WriteHeader();

            _closeStream = false;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void WriteSample(float sample)
        {
            if (sample < -1 || sample > 1)
                sample = Math.Max(-1, Math.Min(1, sample));

            switch (_bitsPerSample)
            {
                case 8:
                    Write((byte)(byte.MaxValue * sample));
                    break;
                case 16:
                    Write((short)(short.MaxValue * sample));
                    break;
                case 24:
                    byte[] buffer = BitConverter.GetBytes((int)(0x7fffff * sample));
                    Write(new[] { buffer[0], buffer[1], buffer[2] }, 0, 3);
                    break;
                case 32:
                    Write((int)(int.MaxValue * sample));
                    break;

                default:
                    throw new InvalidOperationException("Invalid Waveformat",
                        new InvalidOperationException("Invalid BitsPerSample while using PCM encoding."));
            }
        }

        public void WriteSamples(float[] samples, int offset, int count)
        {
            for (int i = offset; i < offset + count; i++)
            {
                WriteSample(samples[i]);
            }
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
            _dataLength += count;
        }

        public void Write(byte value)
        {
            _writer.Write(value);
            _dataLength++;
        }

        public void Write(short value)
        {
            _writer.Write(value);
            _dataLength += 2;
        }

        public void Write(int value)
        {
            _writer.Write(value);
            _dataLength += 4;
        }

        public void Write(float value)
        {
            _writer.Write(value);
            _dataLength += 4;
        }

        private void WriteHeader()
        {
            _writer.Flush();

            long currentPosition = _stream.Position;
            _stream.Position = _waveStartPosition;

            int blockAlign = (_bitsPerSample / 8) * _channels;
            var bytesPerSecond = blockAlign * _sampleRate;

            // RIFF header
            _writer.Write(Encoding.UTF8.GetBytes("RIFF"));
            _writer.Write((int)(_stream.Length - 8));
            _writer.Write(Encoding.UTF8.GetBytes("WAVE"));
            short tag = 0x0001; //Pcm

            // fmt chunk
            _writer.Write(Encoding.UTF8.GetBytes("fmt "));
            _writer.Write((int)16);
            _writer.Write((short)tag);
            _writer.Write((short)_channels);
            _writer.Write((int)_sampleRate);
            _writer.Write((int)bytesPerSecond);
            _writer.Write((short)blockAlign);
            _writer.Write((short)_bitsPerSample);

            // data chunl
            _writer.Write(Encoding.UTF8.GetBytes("data"));
            _writer.Write(_dataLength);

            _writer.Flush();

            _stream.Position = currentPosition;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (!disposing) return;

            try
            {
                WriteHeader();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("WaveWriter::Dispose: " + ex);
            }
            finally
            {
                if (_closeStream)
                {
                    if (_writer != null)
                    {
                        _writer.Close();
                        _writer = null;
                    }

                    if (_stream != null)
                    {
                        _stream.Dispose();
                        _stream = null;
                    }
                }
            }

            _isDisposed = true;
        }

        /// <summary>
        ///     Destructor of the <see cref="WaveWriter" /> which calls the <see cref="Dispose(bool)" /> method.
        /// </summary>
        ~WaveWriter()
        {
            Dispose(false);
        }
    }
}
