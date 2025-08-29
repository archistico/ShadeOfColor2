using System.Security.Cryptography;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

// Install-package SixLabors.ImageSharp

namespace ShadeOfColor
{
    public static class FileToImage
    {
        private const int FileNameFieldLength = 256; // byte riservati per il nome file
        private const int Sha1Length = 20;
        private const int HeaderLength = 2 + 8 + FileNameFieldLength + Sha1Length; // 286

        public static void EncryptFileToImage(string inputFile, string outputImage)
        {
            byte[] fileBytes = File.ReadAllBytes(inputFile);
            string fileName = Path.GetFileName(inputFile);

            byte[] header = CreateHeader(fileBytes, fileName);

            // dati = header + contenuto file
            byte[] data = new byte[header.Length + fileBytes.Length];
            Buffer.BlockCopy(header, 0, data, 0, header.Length);
            Buffer.BlockCopy(fileBytes, 0, data, header.Length, fileBytes.Length);

            // pixel RGBA -> 4 byte a pixel
            int size = (int)Math.Ceiling(Math.Sqrt(data.Length / 4.0));
            using var image = new Image<Rgba32>(size, size);

            int i = 0;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte r = i < data.Length ? data[i++] : (byte)0;
                    byte g = i < data.Length ? data[i++] : (byte)0;
                    byte b = i < data.Length ? data[i++] : (byte)0;
                    byte a = i < data.Length ? data[i++] : (byte)255; // padding alpha se finiamo i dati
                    image[x, y] = new Rgba32(r, g, b, a);
                }
            }

            image.Save(outputImage); // inferisce PNG/JPEG dal nome; usa .png
        }

        /// <summary>
        /// Se 'outputPathOrDir' è una directory (o termina con separatore), salva col nome originale dall'header.
        /// Altrimenti usa esattamente il percorso indicato.
        /// Ritorna il percorso effettivo salvato.
        /// </summary>
        public static string DecryptImageToFile(string inputImage, string outputPathOrDir)
        {
            using var image = Image.Load<Rgba32>(inputImage);

            // Estrai tutti i byte RGBA, canale per canale
            int capacity = checked(image.Width * image.Height * 4);
            byte[] allBytes = new byte[capacity];

            int i = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 p = image[x, y];
                    allBytes[i++] = p.R;
                    allBytes[i++] = p.G;
                    allBytes[i++] = p.B;
                    allBytes[i++] = p.A;
                }
            }

            // --- parsing header ---
            if (allBytes.Length < HeaderLength)
                throw new Exception("Dati insufficienti per l'header.");

            string signature = Encoding.ASCII.GetString(allBytes, 0, 2);
            if (signature != "ER")
                throw new Exception("Firma non valida: non è un file generato da questo programma.");

            long fileSize = BitConverter.ToInt64(allBytes, 2); // little-endian
            if (fileSize < 0)
                throw new Exception("Dimensione file non valida nell'header.");

            string embeddedName = Encoding.UTF8.GetString(allBytes, 10, FileNameFieldLength).TrimEnd('\0');

            byte[] sha1Stored = new byte[Sha1Length];
            Buffer.BlockCopy(allBytes, 10 + FileNameFieldLength, sha1Stored, 0, Sha1Length);

            int dataOffset = HeaderLength;

            if (dataOffset + fileSize > allBytes.Length)
                throw new Exception("L'immagine non contiene tutti i dati dichiarati.");

            byte[] fileData = new byte[fileSize];
            Buffer.BlockCopy(allBytes, dataOffset, fileData, 0, (int)fileSize);

            // verifica SHA1
            using var sha1 = SHA1.Create();
            byte[] sha1Calc = sha1.ComputeHash(fileData);
            if (!BytesEqual(sha1Stored, sha1Calc))
                throw new Exception("SHA1 non corrisponde: dati corrotti o alterati.");

            string outputPath = ResolveOutputPath(outputPathOrDir, embeddedName);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, fileData);

            return outputPath;
        }

        private static string ResolveOutputPath(string outputPathOrDir, string embeddedName)
        {
            bool endsWithSep =
                outputPathOrDir.EndsWith(Path.DirectorySeparatorChar) ||
                outputPathOrDir.EndsWith(Path.AltDirectorySeparatorChar);

            if (endsWithSep || Directory.Exists(outputPathOrDir))
            {
                return Path.Combine(outputPathOrDir, embeddedName);
            }

            // Se esiste già un file con quel nome, sovrascrive: comportamento semplice/esplicito
            return outputPathOrDir;
        }

        private static byte[] CreateHeader(byte[] fileBytes, string fileName)
        {
            using var sha1 = SHA1.Create();
            byte[] sha1Hash = sha1.ComputeHash(fileBytes);

            if (Encoding.UTF8.GetByteCount(fileName) > FileNameFieldLength)
                throw new Exception($"Nome file troppo lungo (max {FileNameFieldLength} byte UTF-8).");

            byte[] header = new byte[HeaderLength];

            // "ER"
            header[0] = (byte)'E';
            header[1] = (byte)'R';

            // size (8 byte, little-endian)
            BitConverter.GetBytes((long)fileBytes.Length).CopyTo(header, 2);

            // filename (UTF-8, padded con \0 fino a 256 byte)
            byte[] nameBytes = Encoding.UTF8.GetBytes(fileName);
            nameBytes.CopyTo(header, 10);
            // il resto del campo è già 0

            // sha1 (20 byte)
            sha1Hash.CopyTo(header, 10 + FileNameFieldLength);

            return header;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}