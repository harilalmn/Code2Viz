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

        /// <summary>
        /// Creates a new GIF encoder.
        /// </summary>
        /// <param name="stream">Output stream to write the GIF to</param>
        /// <param name="width">Width of the GIF in pixels</param>
        /// <param name="height">Height of the GIF in pixels</param>
        /// <param name="frameDelayMs">Delay between frames in milliseconds</param>
        /// <param name="repeat">Whether the animation should loop</param>
        public GifEncoder(Stream stream, int width, int height, int frameDelayMs = 100, bool repeat = true)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _width = width;
            _height = height;
            _delay = Math.Max(1, frameDelayMs / 10); // Convert to centiseconds
            _repeat = repeat;
        }

        /// <summary>
        /// Adds a frame to the GIF animation.
        /// </summary>
        /// <param name="frame">The bitmap frame to add</param>
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
            WriteBytes(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }); // "GIF89a"

            // Logical Screen Descriptor
            WriteShort(_width);
            WriteShort(_height);

            // Packed field: Global Color Table Flag = 1, Color Resolution = 7, Sort Flag = 0, Size of Global Color Table = 7 (256 colors)
            WriteByte(0xF7); // 11110111
            WriteByte(0x00); // Background color index
            WriteByte(0x00); // Pixel aspect ratio

            // Global Color Table (256 colors * 3 bytes = 768 bytes)
            WriteGlobalColorTable();

            // Netscape Application Extension for looping
            if (_repeat)
            {
                WriteBytes(new byte[] { 0x21, 0xFF, 0x0B }); // Extension introducer, Application Extension Label, Block size
                WriteBytes(new byte[] { 0x4E, 0x45, 0x54, 0x53, 0x43, 0x41, 0x50, 0x45 }); // "NETSCAPE"
                WriteBytes(new byte[] { 0x32, 0x2E, 0x30 }); // "2.0"
                WriteBytes(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 }); // Sub-block: loop forever
            }
        }

        private void WriteGlobalColorTable()
        {
            // Write a standard 256-color palette (6x6x6 color cube + grayscale)
            for (int r = 0; r < 6; r++)
            {
                for (int g = 0; g < 6; g++)
                {
                    for (int b = 0; b < 6; b++)
                    {
                        WriteByte((byte)(r * 51));
                        WriteByte((byte)(g * 51));
                        WriteByte((byte)(b * 51));
                    }
                }
            }
            // Fill remaining slots with grayscale (216 colors used, need 40 more)
            for (int i = 0; i < 40; i++)
            {
                byte gray = (byte)(i * 6);
                WriteByte(gray);
                WriteByte(gray);
                WriteByte(gray);
            }
        }

        private void WriteGraphicControlExtension()
        {
            WriteByte(0x21); // Extension introducer
            WriteByte(0xF9); // Graphic Control Label
            WriteByte(0x04); // Block size
            WriteByte(0x04); // Packed field: Disposal method = 1 (do not dispose), no user input, no transparency
            WriteShort(_delay); // Delay time
            WriteByte(0x00); // Transparent color index
            WriteByte(0x00); // Block terminator
        }

        private void WriteImageDescriptor()
        {
            WriteByte(0x2C); // Image separator
            WriteShort(0); // Left position
            WriteShort(0); // Top position
            WriteShort(_width); // Image width
            WriteShort(_height); // Image height
            WriteByte(0x00); // Packed field: No local color table, not interlaced
        }

        private void WriteImageData(BitmapSource frame)
        {
            // Convert frame to indexed color using our global palette
            var indexedPixels = ConvertToIndexedColor(frame);

            // LZW compression
            WriteByte(0x08); // LZW minimum code size (8 for 256 colors)

            var lzwData = LzwEncode(indexedPixels, 8);

            // Write LZW data in sub-blocks (max 255 bytes each)
            int offset = 0;
            while (offset < lzwData.Length)
            {
                int blockSize = Math.Min(255, lzwData.Length - offset);
                WriteByte((byte)blockSize);
                _stream.Write(lzwData, offset, blockSize);
                offset += blockSize;
            }

            WriteByte(0x00); // Block terminator
        }

        private byte[] ConvertToIndexedColor(BitmapSource frame)
        {
            // Convert to BGRA32 format
            var bgra32Frame = new FormatConvertedBitmap(frame, System.Windows.Media.PixelFormats.Bgra32, null, 0);

            int stride = _width * 4;
            var pixels = new byte[_height * stride];
            bgra32Frame.CopyPixels(pixels, stride, 0);

            var indexed = new byte[_width * _height];

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    int srcIndex = y * stride + x * 4;
                    byte b = pixels[srcIndex];
                    byte g = pixels[srcIndex + 1];
                    byte r = pixels[srcIndex + 2];

                    // Map to 6x6x6 color cube index
                    int rIndex = r / 51;
                    int gIndex = g / 51;
                    int bIndex = b / 51;

                    if (rIndex > 5) rIndex = 5;
                    if (gIndex > 5) gIndex = 5;
                    if (bIndex > 5) bIndex = 5;

                    indexed[y * _width + x] = (byte)(rIndex * 36 + gIndex * 6 + bIndex);
                }
            }

            return indexed;
        }

        private byte[] LzwEncode(byte[] data, int minCodeSize)
        {
            var output = new List<byte>();
            int clearCode = 1 << minCodeSize;
            int endCode = clearCode + 1;

            int codeSize = minCodeSize + 1;
            int nextCode = endCode + 1;

            var codeTable = new Dictionary<string, int>();

            // Initialize code table with single-character codes
            for (int i = 0; i < clearCode; i++)
            {
                codeTable[((char)i).ToString()] = i;
            }

            var bitBuffer = new BitBuffer();
            bitBuffer.WriteBits(clearCode, codeSize);

            if (data.Length == 0)
            {
                bitBuffer.WriteBits(endCode, codeSize);
                return bitBuffer.ToArray();
            }

            string current = ((char)data[0]).ToString();

            for (int i = 1; i < data.Length; i++)
            {
                string next = current + (char)data[i];

                if (codeTable.ContainsKey(next))
                {
                    current = next;
                }
                else
                {
                    bitBuffer.WriteBits(codeTable[current], codeSize);

                    if (nextCode < 4096)
                    {
                        codeTable[next] = nextCode++;

                        // Increase code size if needed
                        if (nextCode > (1 << codeSize) && codeSize < 12)
                        {
                            codeSize++;
                        }
                    }
                    else
                    {
                        // Table full, emit clear code and reset
                        bitBuffer.WriteBits(clearCode, codeSize);
                        codeSize = minCodeSize + 1;
                        nextCode = endCode + 1;
                        codeTable.Clear();
                        for (int j = 0; j < clearCode; j++)
                        {
                            codeTable[((char)j).ToString()] = j;
                        }
                    }

                    current = ((char)data[i]).ToString();
                }
            }

            // Output remaining code
            bitBuffer.WriteBits(codeTable[current], codeSize);
            bitBuffer.WriteBits(endCode, codeSize);

            return bitBuffer.ToArray();
        }

        private void WriteByte(byte value)
        {
            _stream.WriteByte(value);
        }

        private void WriteShort(int value)
        {
            _stream.WriteByte((byte)(value & 0xFF));
            _stream.WriteByte((byte)((value >> 8) & 0xFF));
        }

        private void WriteBytes(byte[] bytes)
        {
            _stream.Write(bytes, 0, bytes.Length);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_headerWritten)
                {
                    // Write GIF trailer
                    WriteByte(0x3B);
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Helper class for bit-level writing in LZW compression.
        /// </summary>
        private class BitBuffer
        {
            private readonly List<byte> _bytes = new List<byte>();
            private int _currentByte;
            private int _bitPosition;

            public void WriteBits(int value, int numBits)
            {
                while (numBits > 0)
                {
                    int bitsToWrite = Math.Min(numBits, 8 - _bitPosition);
                    int mask = (1 << bitsToWrite) - 1;

                    _currentByte |= ((value & mask) << _bitPosition);
                    _bitPosition += bitsToWrite;
                    value >>= bitsToWrite;
                    numBits -= bitsToWrite;

                    if (_bitPosition >= 8)
                    {
                        _bytes.Add((byte)_currentByte);
                        _currentByte = 0;
                        _bitPosition = 0;
                    }
                }
            }

            public byte[] ToArray()
            {
                if (_bitPosition > 0)
                {
                    _bytes.Add((byte)_currentByte);
                }
                return _bytes.ToArray();
            }
        }
    }
}
