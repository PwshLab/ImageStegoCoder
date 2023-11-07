using System.Security.Cryptography;
using SkiaSharp;

namespace ImageStegoCoder
{
    public static class Entrypoint
    {
        public static void Main(string[] args)
        {
            string? firstImagePath = null;
            string? secondImagePath = null;
            string? dataFilePath = null;
            bool decode = false;
            bool encode = false;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--first-image":
                        firstImagePath = args[i + 1];
                        break;
                    case "--second-image":
                        secondImagePath = args[i + 1];
                        break;
                    case "--data-file":
                        dataFilePath = args[i + 1];
                        break;
                    case "--encode":
                        encode = true;
                        break;
                    case "--decode":
                        decode = true;
                        break;
                    default:
                        break;
                }
            }

            if (firstImagePath == null || secondImagePath == null || dataFilePath == null || (!decode && !encode) || (decode && encode))
            {
                Console.WriteLine("Invalid Arguments");
                Console.WriteLine("Argument Options:");
                Console.WriteLine("    --first-image");
                Console.WriteLine("    --second-image");
                Console.WriteLine("    --data-file");
                Console.WriteLine("    --encode");
                Console.WriteLine("    --decode");
                return;
            }

            if (encode)
            {
                ImageEncoder encoder = new ImageEncoder();
                encoder.LoadSourceImage(firstImagePath);
                encoder.LoadIputData(dataFilePath);
                encoder.WriteOutImage(secondImagePath);
            }

            if (decode)
            {
                ImageDecoder decoder = new ImageDecoder();
                decoder.LoadSourceImage(firstImagePath);
                decoder.LoadOutputImage(secondImagePath);
                decoder.WriteOutData(dataFilePath);
            }
        }
    }

    public class ImageEncoder
    {
        private SKColor[,] sourceImageColors;
        private SKColor[,] outImageColors;
        private bool[,] outImageWritten;
        private RandomWrapper randomGenerator;
        private byte[] dataBytes;

        private bool loadedSource;
        private bool loadedData;
        private bool hasEncoded;

        private static readonly int[][] seedPositions = new int[][] {
            new int[] { 0, 0 },
            new int[] { 0, 1 },
            new int[] { 1, 0 },
            new int[] { 1, 1 }
        };

        private static readonly int[][] lengthPositions = new int[][] {
            new int[] { 2, 0 },
            new int[] { 2, 1 },
            new int[] { 0, 2 },
            new int[] { 1, 2 }
        };

        public ImageEncoder()
        {
            sourceImageColors = new SKColor[0, 0];
            outImageColors = new SKColor[0, 0];
            outImageWritten = new bool[0, 0];
            randomGenerator = new RandomWrapper();
            dataBytes = new byte[0];

            loadedSource = false;
            loadedData = false;
            hasEncoded = false;
        }

        public void LoadSourceImage(string sourceImagePath)
        {
            //MemoryStream stream = new MemoryStream();
            //FileUtility.ReadFile(sourceImagePath, stream);
            //SKBitmap bitmap = SKBitmap.Decode(sourceImagePath);
            SKBitmap bitmap = SKBitmap.Decode(sourceImagePath);
            sourceImageColors = Converter.BitmapToColors(bitmap);
            //outImageColors = new SKColor[sourceImageColors.GetLength(0), sourceImageColors.GetLength(1)];
            outImageColors = Converter.BitmapToColors(bitmap);
            outImageWritten = new bool[sourceImageColors.GetLength(0), sourceImageColors.GetLength(1)];
            loadedSource = true;
        }

        public void LoadIputData(string inputFilePath)
        {
            if (!loadedSource)
                throw new Exception("Source Data has to be loaded before Input Data");

            byte[] bytes = FileUtility.ReadFile(inputFilePath);

            if ((bytes.Length + 8) > (int)((7 * sourceImageColors.Length) / 8))
            {
                Console.WriteLine("" + bytes.Length + " " + sourceImageColors.Length);
                Console.WriteLine("" + (bytes.Length + 4) + " " + (int)((7 * sourceImageColors.Length) / 8));
                throw new Exception("Input Data should not exceed 7/8 of Source Data");
            }

            dataBytes = bytes;
            loadedData = true;
        }

        private void WriteByte(byte b)
        {
            int x = randomGenerator.Next(0, outImageColors.GetLength(0));
            int y = randomGenerator.Next(0, outImageColors.GetLength(1));
            WriteByte(x, y, b);
        }

        private void WriteByte(int x, int y, byte b)
        {
            if (outImageWritten[x, y])
            {
                for (int i = 0; i < outImageWritten.GetLength(0); i++)
                {
                    for (int j = 0; j < outImageWritten.GetLength(1); j++)
                    {
                        int newX = (x + i) % outImageWritten.GetLength(0);
                        int newY = (y + j) % outImageWritten.GetLength(1);
                        if (!outImageWritten[newX, newY])
                        {
                            Console.WriteLine("Hit! (" + x + "/" + y + ") -> (" + newX + "/" + newY + ")";
                            x = newX;
                            y = newY;
                            goto Foo;
                        }
                    }
                }
            }
            Foo:
            outImageWritten[x, y] = true;
            outImageColors[x, y] = Converter.ByteEncode(sourceImageColors[x, y], b);
        }

        private void EncodeImage()
        {
            if (hasEncoded)
                return;

            int seed = randomGenerator.GetSeed();
            byte[] seedBytes = Converter.ToByteArray(seed);
            for (int i = 0; i < seedBytes.Length; i++)
            {
                int x = seedPositions[i][0];
                int y = seedPositions[i][1];
                WriteByte(x, y, seedBytes[i]);
            }
            Console.WriteLine("Random Seed: " + seed);

            int length = dataBytes.Length;
            byte[] lengthBytes = Converter.ToByteArray(length);
            for (int i = 0; i < seedBytes.Length; i++)
            {
                int x = lengthPositions[i][0];
                int y = lengthPositions[i][1];
                WriteByte(x, y, lengthBytes[i]);
            }
            Console.WriteLine("Data Length: " + length);

            for (int i = 0; i < dataBytes.Length; i++)
            {
                WriteByte(dataBytes[i]);
            }

            hasEncoded = true;
        }

        public void WriteOutImage(string outImagePath)
        {
            if (!loadedData)
                throw new Exception("Input Data has to be loaded before Encoding");

            EncodeImage();

            SKImage image = SKImage.FromBitmap(Converter.ColorsToBitmap(outImageColors));
            using Stream stream = image.Encode().AsStream();
            FileUtility.WriteFile(outImagePath, stream);
        }
    }

    public class ImageDecoder
    {
        private SKColor[,] sourceImageColors;
        private SKColor[,] outImageColors;
        private bool[,] outImageRead;
        private RandomWrapper randomGenerator;
        private byte[] dataBytes;

        private bool loadedSource;
        private bool loadedOutput;
        private bool hasDecoded;

        private static readonly int[][] seedPositions = new int[][] {
            new int[] { 0, 0 },
            new int[] { 0, 1 },
            new int[] { 1, 0 },
            new int[] { 1, 1 }
        };

        private static readonly int[][] lengthPositions = new int[][] {
            new int[] { 2, 0 },
            new int[] { 2, 1 },
            new int[] { 0, 2 },
            new int[] { 1, 2 }
        };

        public ImageDecoder()
        {
            sourceImageColors = new SKColor[0, 0];
            outImageColors = new SKColor[0, 0];
            outImageRead = new bool[0, 0];
            randomGenerator = new RandomWrapper();
            dataBytes = new byte[0];

            loadedSource = false;
            loadedOutput = false;
            hasDecoded = false;
        }

        public void LoadSourceImage(string sourceImagePath)
        {
            //MemoryStream stream = new MemoryStream();
            //FileUtility.ReadFile(sourceImagePath, stream);
            //SKBitmap bitmap = SKBitmap.Decode(stream);
            SKBitmap bitmap = SKBitmap.Decode(sourceImagePath);
            sourceImageColors = Converter.BitmapToColors(bitmap);
            loadedSource = true;
        }

        public void LoadOutputImage(string outputImagePath)
        {
            if (!loadedSource)
                throw new Exception("Source Image has to be loaded before Output Image");

            //MemoryStream stream = new MemoryStream();
            //FileUtility.ReadFile(outputImagePath, stream);
            //SKBitmap bitmap = SKBitmap.Decode(stream);
            SKBitmap bitmap = SKBitmap.Decode(outputImagePath);

            if (bitmap.Width != sourceImageColors.GetLength(0) || bitmap.Height != sourceImageColors.GetLength(1))
                throw new Exception("Images are not the same size");

            outImageColors = Converter.BitmapToColors(bitmap);
            outImageRead = new bool[sourceImageColors.GetLength(0), sourceImageColors.GetLength(1)];
            loadedOutput = true;
        }

        private byte ReadByte()
        {
            int x = randomGenerator.Next(0, outImageColors.GetLength(0));
            int y = randomGenerator.Next(0, outImageColors.GetLength(1));
            return ReadByte(x, y);
        }

        private byte ReadByte(int x, int y)
        {
            if (outImageRead[x, y])
            {
                for (int i = 0; i < outImageRead.GetLength(0); i++)
                {
                    for (int j = 0; j < outImageRead.GetLength(1); j++)
                    {
                        int newX = (x + i) % outImageRead.GetLength(0);
                        int newY = (y + j) % outImageRead.GetLength(1);
                        if (!outImageRead[newX, newY])
                        {

                            Console.WriteLine("Hit! (" + x + "/" + y + ") -> (" + newX + "/" + newY + ")";           x = newX;
                            y = newY;
                            goto Foo;
                        }
                    }
                }
            }
            Foo:
            outImageRead[x, y] = true;
            return Converter.ByteDecode(sourceImageColors[x, y], outImageColors[x, y]);
        }

        private void DecodeImage()
        {
            if (hasDecoded)
                return;

            byte[] seedBytes = new byte[4];
            for (int i = 0; i < seedBytes.Length; i++)
            {
                int x = seedPositions[i][0];
                int y = seedPositions[i][1];
                seedBytes[i] = ReadByte(x, y);
            }
            int seed = Converter.FromByteArray(seedBytes);
            randomGenerator.SetSeed(seed);
            Console.WriteLine("Random Seed: " + seed);

            byte[] lengthBytes = new byte[4];
            for (int i = 0; i < seedBytes.Length; i++)
            {
                int x = lengthPositions[i][0];
                int y = lengthPositions[i][1];
                lengthBytes[i] = ReadByte(x, y);
            }
            int length = Converter.FromByteArray(lengthBytes);
            dataBytes = new byte[length];
            Console.WriteLine("Data Length: " + length);

            for (int i = 0; i < dataBytes.Length; i++)
            {
                dataBytes[i] = ReadByte();
            }

            hasDecoded = true;
        }

        public void WriteOutData(string outputFilePath)
        {
            if (!loadedOutput)
                throw new Exception("Output Data has to be loaded before Decoding");

            DecodeImage();

            FileUtility.WriteFile(outputFilePath, dataBytes);
        } 
    }

    internal static class Converter
    {
        public static SKColor[,] BitmapToColors(SKBitmap bitmap)
        {
            SKColor[,] colors = new SKColor[bitmap.Width, bitmap.Height];
            for (int i = 0; i < bitmap.Width; i++)
            {
                for (int j = 0; j < bitmap.Height; j++)
                {
                    SKColor color = bitmap.GetPixel(i, j);
                    colors[i, j] = color;
                }
            }
            return colors;
        }

        public static SKBitmap ColorsToBitmap(SKColor[,] colors)
        {
            SKBitmap bitmap = new SKBitmap(colors.GetLength(0), colors.GetLength(1));
            for (int i = 0; i < bitmap.Width; i++)
            {
                for (int j = 0; j < bitmap.Height; j++)
                {
                    SKColor color = colors[i, j];
                    bitmap.SetPixel(i, j, color);
                }
            }
            return bitmap;
        }

        public static SKColor ByteEncode(SKColor inColor, byte b)
        {
            byte mask = 0b_0000_0011;
            byte[] bytes = new byte[4];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(b & mask);
                b = (byte)(b >> 2);
            }
            SKColor outColor = new SKColor(
                (byte)(inColor.Red ^ bytes[0]),
                (byte)(inColor.Green ^ bytes[1]),
                (byte)(inColor.Blue ^ bytes[2]),
                (byte)(inColor.Alpha ^ bytes[3])
                );
            return outColor;
        }

        public static byte ByteDecode(SKColor inColor, SKColor outColor)
        {
            byte[] bytes = {
                (byte)(inColor.Red ^ outColor.Red),
                (byte)(inColor.Green ^ outColor.Green),
                (byte)(inColor.Blue ^ outColor.Blue),
                (byte)(inColor.Alpha ^ outColor.Alpha)
                };
            byte b = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                b = (byte)(b << 2);
                b |= bytes[bytes.Length - i - 1];
            }
            return b;
        }

        public static byte[] ToByteArray(int value)
        {
            byte mask = 0b_1111_1111;
            byte[] bytes = new byte[4];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(value & mask);
                value >>= 8;
            }
            return bytes;
        }

        public static int FromByteArray(byte[] bytes)
        {
            int value = 0;
            for (int i = 0; i < bytes.Length; i++)
            {
                value <<= 8;
                value |= bytes[bytes.Length - i - 1];
            }
            return value;
        }
    }

    internal static class FileUtility
    {
        public static byte[] ReadFile(string filePath)
        {
            using FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            byte[] bytes = new byte[fileStream.Length];
            int remaining = bytes.Length;
            int currentIndex = 0;
            int read;
            do
            {
                read = fileStream.Read(bytes, currentIndex, remaining);
                remaining -= read;
                currentIndex += read;
            } while (remaining > 0 && read > 0);
            fileStream.Flush();
            return bytes;
        }

        public static void WriteFile(string filePath, byte[] bytes)
        {
            using FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            fileStream.Write(bytes);
            fileStream.Flush();
        }

        public static void ReadFile(string filePath, Stream stream)
        {
            using FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            fileStream.CopyTo(stream);
            fileStream.Flush();
            stream.Flush();
        }

        public static void WriteFile(string filePath, Stream stream)
        {
            using FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            stream.CopyTo(fileStream);
            stream.Flush();
            fileStream.Flush();
        }
    }


    internal class RandomWrapper
    {
        private Random randomGenerator;
        private int seedValue;

        public RandomWrapper()
        {
            seedValue = RandomNumberGenerator.GetInt32(int.MaxValue);
            randomGenerator = new Random(seedValue);
        }

        public int Next(int upper)
        {
            return randomGenerator.Next(upper);
        }

        public int Next(int lower, int upper)
        {
            return randomGenerator.Next(lower, upper);
        }

        public int GetSeed()
        {
            return seedValue;
        }

        public void SetSeed(int value)
        {
            seedValue = value;
            randomGenerator = new Random(value);
        }
    }
}