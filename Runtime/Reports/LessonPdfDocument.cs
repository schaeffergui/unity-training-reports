using Docnet.Core;
using Docnet.Core.Models;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

public sealed class LessonPdfDocument : IDisposable
{
    private const int MaxFileBytes = 64 * 1024 * 1024;
    private const int MaxPages = 512;
    private static readonly object NativeLock = new object();
    private byte[] bytes;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    // DriveInfo uses calls that are not implemented in IL2CPP; only queries the drive type on Windows.
    [DllImport("kernel32.dll", EntryPoint = "GetDriveTypeW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint GetDriveType(string rootPath);
#endif

    public string FileName { get; private set; }
    public Vector2[] PageSizes { get; private set; }

    public readonly struct PageImage
    {
        public readonly byte[] Pixels;
        public readonly int Width;
        public readonly int Height;

        public PageImage(byte[] pixels, int width, int height)
        {
            Pixels = pixels;
            Width = width;
            Height = height;
        }
    }

    public static LessonPdfDocument Load(string path, CancellationToken token)
    {
#if !UNITY_STANDALONE_WIN && !UNITY_EDITOR_WIN
        throw new PlatformNotSupportedException();
#else
        string fullPath = ValidateLocalPath(path);
        byte[] data;
        using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (stream.Length < 5 || stream.Length > MaxFileBytes)
            {
                throw new InvalidDataException("The PDF is empty or exceeds the 64 MB limit.");
            }

            data = new byte[(int)stream.Length];
            int offset = 0;
            while (offset < data.Length)
            {
                token.ThrowIfCancellationRequested();
                int read = stream.Read(data, offset, Math.Min(65536, data.Length - offset));
                if (read == 0) throw new EndOfStreamException();
                offset += read;
            }
        }

        if (data[0] != '%' || data[1] != 'P' || data[2] != 'D' || data[3] != 'F' || data[4] != '-')
        {
            throw new InvalidDataException("The selected file is not a valid PDF.");
        }

        lock (NativeLock)
        {
            token.ThrowIfCancellationRequested();
            // PDFium is global and not thread-safe; no operation keeps handles after returning.
            using (var library = DocLib.Instance)
            using (var reader = library.GetDocReader(data, new PageDimensions(1.0)))
            {
                int count = reader.GetPageCount();
                if (count < 1 || count > MaxPages)
                {
                    throw new InvalidDataException("The PDF must contain between 1 and 512 pages.");
                }

                var sizes = new Vector2[count];
                for (int i = 0; i < count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    using (var page = reader.GetPageReader(i))
                    {
                        int width = page.GetPageWidth();
                        int height = page.GetPageHeight();
                        if (width < 1 || height < 1 || width > 16384 || height > 16384)
                        {
                            throw new InvalidDataException("The PDF contains a page with invalid dimensions.");
                        }
                        sizes[i] = new Vector2(width, height);
                    }
                }
                token.ThrowIfCancellationRequested();
                return new LessonPdfDocument { bytes = data, FileName = Path.GetFileName(fullPath), PageSizes = sizes };
            }
        }
#endif
    }

    public PageImage Render(int pageIndex, int maxDimension, CancellationToken token)
    {
        byte[] data = GetBytes();
        if ((uint)pageIndex >= PageSizes.Length) throw new ArgumentOutOfRangeException(nameof(pageIndex));
        maxDimension = Math.Max(64, Math.Min(2880, maxDimension));

        lock (NativeLock)
        {
            token.ThrowIfCancellationRequested();
            using (var library = DocLib.Instance)
            using (var reader = library.GetDocReader(data, new PageDimensions(maxDimension, maxDimension)))
            using (var page = reader.GetPageReader(pageIndex))
            {
                int width = page.GetPageWidth();
                int height = page.GetPageHeight();
                if (width < 1 || height < 1 || (long)width * height > 8 * 1024 * 1024)
                {
                    throw new InvalidDataException("The page exceeds the viewer memory limit.");
                }

                // No forms, JavaScript, links or attachments: only the pixels of the local document.
                byte[] pixels = page.GetImage(RenderFlags.LimitImageCacheSize);
                token.ThrowIfCancellationRequested();
                for (int y = 0; y < height; y++)
                {
                    token.ThrowIfCancellationRequested();
                    int end = (y + 1) * width * 4;
                    for (int i = y * width * 4; i < end; i += 4)
                    {
                        int alpha = pixels[i + 3];
                        byte blue = pixels[i];
                        pixels[i] = (byte)((pixels[i + 2] * alpha + 255 * (255 - alpha)) / 255);
                        pixels[i + 1] = (byte)((pixels[i + 1] * alpha + 255 * (255 - alpha)) / 255);
                        pixels[i + 2] = (byte)((blue * alpha + 255 * (255 - alpha)) / 255);
                        pixels[i + 3] = 255;
                    }
                }
                return new PageImage(pixels, width, height);
            }
        }
    }

    public string SaveCopy(string folder, CancellationToken token)
    {
        byte[] data = GetBytes();
        string destinationFolder = ValidateLocalPath(Path.Combine(folder, FileName), false);
        destinationFolder = Path.GetDirectoryName(destinationFolder);
        Directory.CreateDirectory(destinationFolder);
        for (int index = 1; index <= 999; index++)
        {
            token.ThrowIfCancellationRequested();
            string name = Path.GetFileNameWithoutExtension(FileName) + "_copia_" + index + ".pdf";
            string destination = Path.Combine(destinationFolder, name);
            FileStream stream;
            try
            {
                stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            }
            catch (IOException) when (File.Exists(destination))
            {
                continue;
            }

            try
            {
                using (stream)
                {
                    for (int offset = 0; offset < data.Length; offset += 65536)
                    {
                        token.ThrowIfCancellationRequested();
                        stream.Write(data, offset, Math.Min(65536, data.Length - offset));
                    }
                }
                return destination;
            }
            catch
            {
                File.Delete(destination);
                throw;
            }
        }
        throw new IOException("Could not create another copy in this folder.");
    }

    private static string ValidateLocalPath(string path, bool mustExist = true)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || path.StartsWith(@"\\") ||
            path.IndexOf("://", StringComparison.Ordinal) >= 0 || !string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Select a PDF file in a local folder.");
        }

        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath);
        if (fullPath.IndexOf(':', root.Length) >= 0)
        {
            throw new InvalidDataException("Only local PDF files are allowed.");
        }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (GetDriveType(root) == (uint)DriveType.Network)
        {
            throw new InvalidDataException("Only local PDF files are allowed.");
        }
#endif

        for (string current = fullPath; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("Use a local folder without redirects or symbolic links.");
            }
        }
        if (mustExist && !File.Exists(fullPath)) throw new FileNotFoundException();
        return fullPath;
    }

    private byte[] GetBytes() => Volatile.Read(ref bytes) ?? throw new ObjectDisposedException(nameof(LessonPdfDocument));

    public void Dispose()
    {
        // A job that has already started releases its reference when it ends; closing the UI never waits for native rendering.
        Interlocked.Exchange(ref bytes, null);
    }
}
