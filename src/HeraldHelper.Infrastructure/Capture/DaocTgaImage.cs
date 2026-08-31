using System.Buffers.Binary;

namespace HeraldHelper.Infrastructure.Capture;

internal sealed class DaocTgaImage
{
    private const int HeaderLength = 18;
    private const int MaximumDimension = 4096;
    private const int MaximumPixelCount = 16_777_216;
    private readonly byte[] _pixels;

    private DaocTgaImage(int width, int height, int bytesPerPixel, byte[] pixels)
    {
        Width = width;
        Height = height;
        BytesPerPixel = bytesPerPixel;
        _pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }
    public int BytesPerPixel { get; }

    public static DaocTgaImage Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < HeaderLength)
        {
            throw new InvalidDataException("TGA header is truncated.");
        }

        var idLength = bytes[0];
        var colorMapType = bytes[1];
        var imageType = bytes[2];
        var width = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(12, 2));
        var height = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(14, 2));
        var bitsPerPixel = bytes[16];
        var descriptor = bytes[17];
        if (width == 0 || height == 0 ||
            width > MaximumDimension || height > MaximumDimension ||
            (long)width * height > MaximumPixelCount ||
            colorMapType != 0 || imageType is not 2 and not 10 || bitsPerPixel is not 24 and not 32)
        {
            throw new InvalidDataException("Unsupported DAoC TGA format.");
        }

        var bytesPerPixel = bitsPerPixel / 8;
        var pixelCount = checked(width * height);
        var sourceOffset = HeaderLength + idLength;
        var stored = imageType == 2
            ? ReadUncompressed(bytes, sourceOffset, checked(pixelCount * bytesPerPixel))
            : ReadRle(bytes, sourceOffset, pixelCount, bytesPerPixel);

        var topOrigin = (descriptor & 0x20) != 0;
        var rightOrigin = (descriptor & 0x10) != 0;
        var pixels = new byte[stored.Length];
        for (var y = 0; y < height; y++)
        {
            var sourceY = topOrigin ? y : height - 1 - y;
            for (var x = 0; x < width; x++)
            {
                var sourceX = rightOrigin ? width - 1 - x : x;
                stored.AsSpan(((sourceY * width) + sourceX) * bytesPerPixel, bytesPerPixel)
                    .CopyTo(pixels.AsSpan(((y * width) + x) * bytesPerPixel, bytesPerPixel));
            }
        }

        return new DaocTgaImage(width, height, bytesPerPixel, pixels);
    }

    public (byte B, byte G, byte R, byte A) GetPixel(int x, int y)
    {
        var offset = ((y * Width) + x) * BytesPerPixel;
        return (
            _pixels[offset],
            _pixels[offset + 1],
            _pixels[offset + 2],
            BytesPerPixel == 4 ? _pixels[offset + 3] : byte.MaxValue);
    }

    private static byte[] ReadUncompressed(byte[] source, int offset, int length)
    {
        if ((long)offset + length > source.Length)
        {
            throw new InvalidDataException("TGA pixel data is truncated.");
        }
        return source.AsSpan(offset, length).ToArray();
    }

    private static byte[] ReadRle(byte[] source, int offset, int pixelCount, int bytesPerPixel)
    {
        var result = new byte[checked(pixelCount * bytesPerPixel)];
        var decodedPixels = 0;
        while (decodedPixels < pixelCount)
        {
            if (offset >= source.Length)
            {
                throw new InvalidDataException("TGA RLE data is truncated.");
            }

            var header = source[offset++];
            var count = (header & 0x7f) + 1;
            if (decodedPixels + count > pixelCount)
            {
                throw new InvalidDataException("TGA RLE packet exceeds image bounds.");
            }

            var target = decodedPixels * bytesPerPixel;
            if ((header & 0x80) != 0)
            {
                if ((long)offset + bytesPerPixel > source.Length)
                {
                    throw new InvalidDataException("TGA RLE pixel is truncated.");
                }
                var pixel = source.AsSpan(offset, bytesPerPixel);
                offset += bytesPerPixel;
                for (var index = 0; index < count; index++)
                {
                    pixel.CopyTo(result.AsSpan(target + (index * bytesPerPixel), bytesPerPixel));
                }
            }
            else
            {
                var length = checked(count * bytesPerPixel);
                if ((long)offset + length > source.Length)
                {
                    throw new InvalidDataException("TGA RLE raw packet is truncated.");
                }
                source.AsSpan(offset, length).CopyTo(result.AsSpan(target));
                offset += length;
            }
            decodedPixels += count;
        }
        return result;
    }
}
