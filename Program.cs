using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace LPR381
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Get the folder where the .exe is running
            string basePath = AppDomain.CurrentDomain.BaseDirectory;

            // Go up to the project folder (bin\Debug\ -> project folder)
            string projectPath = Path.GetFullPath(Path.Combine(basePath, @"..\..\"));

            // Input and output file paths (relative to project folder)
            string inputFilePath = Path.Combine(projectPath, "input.txt");
            string outputFilePath = Path.Combine(projectPath, "output.txt");

            Console.WriteLine("Input file path: " + inputFilePath);
            Console.WriteLine("Output file path: " + outputFilePath);
            Console.WriteLine();

            // ----- READ INPUT FILE -----
            InputFileReader reader = new InputFileReader();
            reader.Read(inputFilePath);

            // Display what was read
            Console.WriteLine("Objective Type: " + reader.ObjectiveType);
            Console.Write("Objective Coefficients: ");
            foreach (var c in reader.ObjCoefficients)
            {
                Console.Write(c + " ");
            }

            Console.WriteLine("\n\nConstraints:");
            foreach (var con in reader.Constraints)
            {
                foreach (var c in con.Coefficients)
                    Console.Write(c + " ");
                Console.WriteLine(con.Relation + " " + con.RHS);
            }

            Console.Write("\nSign Restrictions: ");
            foreach (var s in reader.SignRestrictions)
                Console.Write(s + " ");

            // ----- WRITE OUTPUT FILE -----
            OutputWriter.Write(outputFilePath, reader);
            Console.WriteLine("\n\nOutput written to: " + outputFilePath);

            // ----- CANONICAL FORM -----
            Console.WriteLine("\n\n===== CONVERTING TO CANONICAL FORM =====");
            CanonicalForm canonical = new CanonicalForm();
            canonical.Convert(reader);
            canonical.Display();

            Console.WriteLine("\nPress Enter to exit...");
            Console.ReadLine();
        }
    }
}