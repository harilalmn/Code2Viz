using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace Code2Viz.Export
{
    /// <summary>
    /// Encodes multiple bitmap frames into an animated GIF file.
    /// Uses GIF89a format with Netscape extension for looping.
    /// </summary>
    public class GifEncoder : IDisposable
    {
        private readonly Stream _stream;
        private readonly int _width;
        private readonly int _height;
        private readonly int _delay; // Delay in centiseconds (1/100th of a second)
        private readonly bool _repeat;
        private bool _headerWritten;
        private bool _disposed;

        public GifEncoder(Stream stream, int width, int height, int frameDelayMs = 100, bool repeat = true)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _width = width;
            _height = height;
            _delay = Math.Max(2, frameDelayMs / 10); // Convert to centiseconds, min 2 (20ms)
            _repeat = repeat;
        }

        public void AddFrame(BitmapSource frame)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GifEncoder));

            if (!_headerWritten)
            {
                WriteHeader();
                _headerWritten = true;
            }

            WriteGraphicControlExtension();
            WriteImageDescriptor();
            WriteImageData(frame);
        }

        private void WriteHeader()
        {
            // GIF89a signature
            _stream.Write(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, 0, 6);

            // Logical Screen Descriptor
            WriteShort(_width);
            WriteShort(_height);
            _stream.WriteByte(0xF7); // Global Color Table Flag=1, Color Resolution=7, Sort=0, Size=7 (256 colors)
            _stream.WriteByte(0x00); // Background color index
            _stream.WriteByte(0x00); // Pixel aspect ratio

            // Global Color Table (256 colors)
            WriteGlobalColorTable();

            // Netscape Application Extension for looping
            if (_repeat)
            {
                _stream.Write(new byte[] {
                    0x21, 0xFF, 0x0B,  // Extension introducer, App Extension, Block size
                    0x4E, 0x45, 0x54, 0x53, 0x43, 0x41, 0x50, 0x45,  // "NETSCAPE"
                    0x32, 0x2E, 0x30,  // "2.0"
                    0x03, 0x01, 0x00, 0x00,  // Sub-block size, loop sub-block id, loop count (0=forever)
                    0x00  // Block terminator
                }, 0, 19);
            }
        }

        private void WriteGlobalColorTable()
        {
            // 6x6x6 color cube (216 colors)
            for (int r = 0; r < 6; r++)
            {
                for (int g = 0; g < 6; g++)
                {
                    for (int b = 0; b < 6; b++)
                    {
                        _stream.WriteByte((byte)(r * 51));
                        _stream.WriteByte((byte)(g * 51));
                        _stream.WriteByte((byte)(b * 51));
                    }
                }
            }
            // Fill remaining 40 slots with grayscale
            for (int i = 0; i < 40; i++)
            {
                byte gray = (byte)(i * 6);
                _stream.WriteByte(gray);
                _stream.WriteByte(gray);
                _stream.WriteByte(gray);
            }
        }

        private void WriteGraphicControlExtension()
        {
            _stream.WriteByte(0x21); // Extension introducer
            _stream.WriteByte(0xF9); // Graphic Control Label
            _stream.WriteByte(0x04); // Block size
            _stream.WriteByte(0x08); // Disposal=2 (restore to background), no transparency
            WriteShort(_delay);      // Delay time in centiseconds
            _stream.WriteByte(0x00); // Transparent color index (not used)
            _stream.WriteByte(0x00); // Block terminator
        }

        private void WriteImageDescriptor()
        {
            _stream.WriteByte(0x2C); // Image separator
            WriteShort(0);           // Left position
            WriteShort(0);           // Top position
            WriteShort(_width);      // Image width
            WriteShort(_height);     // Image height
            _stream.WriteByte(0x00); // No local color table, not interlaced
        }

        private void WriteImageData(BitmapSource frame)
        {
            var indexedPixels = ConvertToIndexedColor(frame);

            _stream.WriteByte(0x08); // LZW minimum code size

            var lzwData = LzwEncode(indexedPixels);

            // Write in sub-blocks (max 255 bytes each)
            int offset = 0;
            while (offset < lzwData.Length)
            {
                int blockSize = Math.Min(255, lzwData.Length - offset);
                _stream.WriteByte((byte)blockSize);
                _stream.Write(lzwData, offset, blockSize);
                offset += blockSize;
            }

            _stream.WriteByte(0x00); // Block terminator
        }

        private byte[] ConvertToIndexedColor(BitmapSource frame)
        {
            var converted = new FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            int stride = _width * 4;
            var pixels = new byte[_height * stride];
            converted.CopyPixels(pixels, stride, 0);

            var indexed = new byte[_width * _height];

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int srcIndex = y * stride + x * 4;
                    byte b = pixels[srcIndex];
                    byte g = pixels[srcIndex + 1];
                    byte r = pixels[srcIndex + 2];

                    // Map to 6x6x6 color cube
                    int ri = Math.Min(5, r / 51);
                    int gi = Math.Min(5, g / 51);
                    int bi = Math.Min(5, b / 51);

                    indexed[y * _width + x] = (byte)(ri * 36 + gi * 6 + bi);
                }
            }

            return indexed;
        }

        private byte[] LzwEncode(byte[] pixels)
        {
            const int MinCodeSize = 8;
            const int ClearCode = 256;
            const int EndCode = 257;
            const int MaxCode = 4095;

            var output = new List<byte>();
            int bitBuffer = 0;
            int bitCount = 0;

            void WriteBits(int code, int numBits)
            {
                bitBuffer |= code << bitCount;
                bitCount += numBits;
                while (bitCount >= 8)
                {
                    output.Add((byte)(bitBuffer & 0xFF));
                    bitBuffer >>= 8;
                    bitCount -= 8;
                }
            }

            // Hash table for LZW - maps (prefix_code, byte) to code
            var hashTable = new int[5003];
            var codeTable = new int[5003];
            int codeSize = MinCodeSize + 1;
            int nextCode = EndCode + 1;

            void ClearTable()
            {
                Array.Fill(hashTable, -1);
                codeSize = MinCodeSize + 1;
                nextCode = EndCode + 1;
            }

            int HashKey(int prefixCode, byte b)
            {
                int hash = ((b << 12) ^ prefixCode) % 5003;
                return hash < 0 ? hash + 5003 : hash;
            }

            int FindCode(int prefixCode, byte b)
            {
                int hash = HashKey(prefixCode, b);
                int key = (prefixCode << 8) | b;

                while (hashTable[hash] != -1)
                {
                    if (hashTable[hash] == key)
                        return codeTable[hash];
                    hash = (hash + 1) % 5003;
                }
                return -1;
            }

            void AddCode(int prefixCode, byte b, int code)
            {
                int hash = HashKey(prefixCode, b);
                int key = (prefixCode << 8) | b;

                while (hashTable[hash] != -1)
                    hash = (hash + 1) % 5003;

                hashTable[hash] = key;
                codeTable[hash] = code;
            }

            ClearTable();
            WriteBits(ClearCode, codeSize);

            if (pixels.Length == 0)
            {
                WriteBits(EndCode, codeSize);
                if (bitCount > 0)
                    output.Add((byte)(bitBuffer & 0xFF));
                return output.ToArray();
            }

            int currentCode = pixels[0];

            for (int i = 1; i < pixels.Length; i++)
            {
                byte b = pixels[i];
                int existingCode = FindCode(currentCode, b);

                if (existingCode >= 0)
                {
                    currentCode = existingCode;
                }
                else
                {
                    WriteBits(currentCode, codeSize);

                    if (nextCode <= MaxCode)
                    {
                        AddCode(currentCode, b, nextCode++);
                        if (nextCode > (1 << codeSize) && codeSize < 12)
                            codeSize++;
                    }
                    else
                    {
                        WriteBits(ClearCode, codeSize);
                        ClearTable();
                    }

                    currentCode = b;
                }
            }

            WriteBits(currentCode, codeSize);
            WriteBits(EndCode, codeSize);

            if (bitCount > 0)
                output.Add((byte)(bitBuffer & 0xFF));

            return output.ToArray();
        }

        private void WriteShort(int value)
        {
            _stream.WriteByte((byte)(value & 0xFF));
            _stream.WriteByte((byte)((value >> 8) & 0xFF));
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_headerWritten)
                    _stream.WriteByte(0x3B); // GIF trailer
                _disposed = true;
            }
        }
    }
}
