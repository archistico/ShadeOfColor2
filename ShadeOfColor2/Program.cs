using System;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ShadeOfColor
{
    class Program
    {
        static void Help()
        {
            Console.WriteLine("Help:");
            Console.WriteLine("  shadeofcolor.exe -crypt <inputFile> <outputImage.png>");
            Console.WriteLine("  shadeofcolor.exe -decrypt <inputImage.png>");
        }

        static void Main(string[] args)
        {
            string commandCrypt = "-crypt";
            string commandDecrypt = "-decrypt";
            string input = string.Empty;
            string output = string.Empty;

            if (args.Length < 1)
            {
                Help();
                return;
            }

            string command = args[0].ToLowerInvariant();

            if ((args.Length > 2) && (command == commandDecrypt))
            {
                Help();
                return;
            }

            if ((args.Length < 3) && (command == commandCrypt))
            {
                Help();
                return;
            }

            try
            {
                switch (command)
                {
                    case string str when str.Equals(commandCrypt):
                        input = args[1];
                        output = args[2];
                        FileToImage.EncryptFileToImage(input, output);
                        Console.WriteLine($"OK: '{input}' -> '{output}'");
                        break;

                    case string str when str.Equals(commandDecrypt):
                        input = args[1];
                        string savedAs = FileToImage.DecryptImageToFile(input, Directory.GetCurrentDirectory());
                        Console.WriteLine($"OK: '{input}' -> '{savedAs}'");
                        break;

                    default:
                        Console.WriteLine($"Comando sconosciuto: {command}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore: {ex.Message}");
            }
        }
    }
}