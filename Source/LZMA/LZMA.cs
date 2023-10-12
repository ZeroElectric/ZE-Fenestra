using System;
using System.IO;

namespace ZeroElectric.Fenestra
{
    public class LZMA
    {
        public static void Compress(Stream inStream, Stream outStream)
        {
            if (inStream == null && outStream == null)
            {
                return;
            }

            SevenZip.Compression.LZMA.Encoder coder = new SevenZip.Compression.LZMA.Encoder();

            coder.WriteCoderProperties(outStream);

            for (int i = 0; i < 8; i++)
            {
                outStream.WriteByte((byte)(inStream.Length >> (8 * i)));
            }

            coder.Code(inStream, outStream, -1, -1, null);
        }

        public static void Decompress(Stream inStream, Stream outStream)
        {
            if (inStream == null && outStream == null)
            {
                return;
            }

            SevenZip.Compression.LZMA.Decoder coder = new SevenZip.Compression.LZMA.Decoder();

            // Read the decoder properties
            byte[] properties = new byte[5];
            inStream.Read(properties, 0, 5);

            // Read in the decompress file size.
            byte[] fileLengthBytes = new byte[8];
            inStream.Read(fileLengthBytes, 0, 8);
            long fileLength = BitConverter.ToInt64(fileLengthBytes, 0);

            coder.SetDecoderProperties(properties);
            coder.Code(inStream, outStream, inStream.Length, fileLength, null);
        }
    }
}
