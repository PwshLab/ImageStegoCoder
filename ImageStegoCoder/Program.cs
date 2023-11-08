using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
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
            bool test = false;

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
                    case "--test":
                        test = true;
                        break;
                    default:
                        break;
                }
            }

            if (firstImagePath == null || secondImagePath == null || dataFilePath == null || (!decode && !encode) || (decode && encode))
            {
                if (!test)
                {
                    Console.WriteLine("Invalid Arguments");
                    Console.WriteLine("Argument Options:");
                    Console.WriteLine("    --first-image");
                    Console.WriteLine("    --second-image");
                    Console.WriteLine("    --data-file");
                    Console.WriteLine("    --encode");
                    Console.WriteLine("    --decode");
                    Console.WriteLine("    --test");
                    return;
                }
            }

            if (test)
            {
                int width = 256, height = 256;
                RandomWrapper rand = new RandomWrapper();
                byte[] inBytes = new byte[width * height];
                rand.NextBytes(inBytes);
                string hash1 = Hashing.Sha256(inBytes);
                Console.WriteLine("In  Data Hash: " + hash1);

                SKColor[,] sourceColors = new SKColor[width, height];
                for (int i = 0; i < sourceColors.GetLength(0); i++)
                {
                    SKColor[] colors = new SKColor[sourceColors.GetLength(1)];
                    rand.NextColors(colors);
                    for (int j = 0; j < colors.Length; j++)
                    {
                        sourceColors[i, j] = colors[j];
                    }
                }
 
                SKColor[,] encodedColors = new SKColor[width, height];
                for (int i = 0; i < encodedColors.GetLength(0); i++)
                {
                    for (int j = 0; j < encodedColors.GetLength(1); j++)
                    {
                        encodedColors[i, j] = Converter.ByteEncode(sourceColors[i, j], inBytes[i * width + j]);
                    }
                }

                SKBitmap bitmap = Converter.ColorsToBitmap(encodedColors, new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
                encodedColors = Converter.BitmapToColors(bitmap);

                byte[] outBytes = new byte[width * height];
                for (int i = 0; i < encodedColors.GetLength(0); i++)
                {
                    for (int j = 0; j < encodedColors.GetLength(1); j++)
                    {
                        outBytes[i * width + j] = Converter.ByteDecode(sourceColors[i, j], encodedColors[i, j]);
                    }
                }

                string hash2 = Hashing.Sha256(outBytes);
                Console.WriteLine("Out Data Hash: " + hash2);

                if (hash1.Equals(hash2))
                {
                    Console.WriteLine("Encoding successfull");
                }
                else
                {
                    Console.WriteLine("Encoding failed");
                }

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
        private SKImageInfo imageInfo;

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
            imageInfo = new SKImageInfo();

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
            imageInfo = bitmap.Info;
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
                            //Console.WriteLine("Hit! (" + x + "/" + y + ") -> (" + newX + "/" + newY + ")");
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
            string hash = Hashing.Sha256(dataBytes);
            Console.WriteLine("Data Hash: " + hash);

            hasEncoded = true;
        }

        public void WriteOutImage(string outImagePath)
        {
            if (!loadedData)
                throw new Exception("Input Data has to be loaded before Encoding");

            EncodeImage();

            SKImage image = SKImage.FromBitmap(Converter.ColorsToBitmap(outImageColors, imageInfo));
            SKData imageData = image.Encode(SKEncodedImageFormat.Png, 100);

            //string hash = Hashing.Sha256(imageData.ToArray());
            //Console.WriteLine("Out Image Hash: " + hash);

            using Stream stream = imageData.AsStream();
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
        private SKImageInfo imageInfo;

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
            imageInfo = new SKImageInfo();

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
            imageInfo = bitmap.Info;
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

            //SKImage image = SKImage.FromBitmap(Converter.ColorsToBitmap(outImageColors));
            //SKData imageData = image.Encode();
            //string hash = Hashing.Sha256(imageData.ToArray());
            //Console.WriteLine("Out Image Hash: " + hash);
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
                            //Console.WriteLine("Hit! (" + x + "/" + y + ") -> (" + newX + "/" + newY + ")");           
                            x = newX;
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
            string hash = Hashing.Sha256(dataBytes);
            Console.WriteLine("Data Hash: " + hash);

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

        public static SKBitmap ColorsToBitmap(SKColor[,] colors, SKImageInfo info)
        {
            //SKColorType colorType = SKColorType.;
            //SKAlphaType alphaType = SKAlphaType.Unpremul;
            //SKColorSpace colorSpace = SKColorSpac
            //SKBitmap bitmap = new SKBitmap(colors.GetLength(0), colors.GetLength(1), colorType, alphaType);
            SKBitmap bitmap = new SKBitmap(info);
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

        //public static SKColor ByteEncode(SKColor inColor, byte b)
        //{
        //    byte mask = 0b_0000_0011;
        //    byte[] bytes = new byte[4];
        //    for (int i = 0; i < bytes.Length; i++)
        //    {
        //        bytes[i] = (byte)(b & mask);
        //        b = (byte)(b >> 2);
        //    }
        //    SKColor outColor = new SKColor(
        //        (byte)(inColor.Red ^ bytes[0]),
        //        (byte)(inColor.Green ^ bytes[1]),
        //        (byte)(inColor.Blue ^ bytes[2]),
        //        (byte)(inColor.Alpha ^ bytes[3])
        //        );
        //    return outColor;
        //}

        //public static byte ByteDecode(SKColor inColor, SKColor outColor)
        //{
        //    byte[] bytes = {
        //        (byte)(inColor.Red ^ outColor.Red),
        //        (byte)(inColor.Green ^ outColor.Green),
        //        (byte)(inColor.Blue ^ outColor.Blue),
        //        (byte)(inColor.Alpha ^ outColor.Alpha)
        //        };
        //    byte b = 0;
        //    for (int i = 0; i < bytes.Length; i++)
        //    {
        //        b = (byte)(b << 2);
        //        b |= bytes[bytes.Length - i - 1];
        //    }
        //    return b;
        //}

        public static SKColor ByteEncode(SKColor inColor, byte b)
        {
            byte[] bytes = new byte[]
            {
                (byte)(b & 0b_0000_0111),
                (byte)((b & 0b_0001_1000) >> 3),
                (byte)((b & 0b_1110_0000) >> 5)
            };
            SKColor outColor = new SKColor(
                (byte)(inColor.Red ^ bytes[0]),
                (byte)(inColor.Green ^ bytes[1]),
                (byte)(inColor.Blue ^ bytes[2]),
                inColor.Alpha
                );
            return outColor;
        }

        public static byte ByteDecode(SKColor inColor, SKColor outColor)
        {
            byte[] bytes = new byte[]
            {
                (byte)(inColor.Red ^ outColor.Red),
                (byte)((inColor.Green ^ outColor.Green) << 3),
                (byte)((inColor.Blue ^ outColor.Blue) << 5)
            };
            byte b = (byte)(bytes[0] | bytes[1] | bytes[2]);
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

        public void NextBytes(byte[] bytes)
        {
            randomGenerator.NextBytes(bytes);
        }

        public void NextColors(SKColor[] colors)
        {
            byte[] bytes = new byte[4];
            for (int i = 0; i < colors.Length; i++)
            {
                randomGenerator.NextBytes(bytes);
                colors[i] = new SKColor(bytes[0], bytes[1], bytes[2], bytes[3]);
            }
        }
    }

    internal static class Hashing
    {
        private static string ByteArrayToString(byte[] arrInput)
        {
            int i;
            StringBuilder sOutput = new StringBuilder(arrInput.Length);
            for (i = 0; i < arrInput.Length; i++)
            {
                sOutput.Append(arrInput[i].ToString("X2"));
            }
            return sOutput.ToString();
        }

        public static string Sha256(byte[] arrInput)
        {
            byte[] hash = SHA256.Create().ComputeHash(arrInput);
            return ByteArrayToString(hash);
        }
    }
}