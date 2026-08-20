using System;
using System.IO;

namespace LPR381
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string projectPath = Path.GetFullPath(Path.Combine(basePath, @"..\..\"));
            string inputFilePath = Path.Combine(projectPath, "input.txt");
            string outputFilePath = Path.Combine(projectPath, "output.txt");

            Console.WriteLine("Input file path: " + inputFilePath);
            Console.WriteLine("Output file path: " + outputFilePath);
            Console.WriteLine();

            InputFileReader reader = new InputFileReader();
            reader.Read(inputFilePath);

            CanonicalForm canonical = new CanonicalForm();
            canonical.Convert(reader);
            canonical.Display();

            PrimalSimplex primal = new PrimalSimplex();
            primal.Solve(canonical, reader.ObjectiveType);

            RevisedSimplex revised = new RevisedSimplex();
            revised.Solve(reader);

            OutputWriter.Write(outputFilePath, reader, canonical.GetDisplayString(), primal.Log, revised.Log);
            Console.WriteLine("\nOutput written to: " + outputFilePath);

            Console.WriteLine("\nPress Enter to exit...");
            Console.ReadLine();
        }
    }
}