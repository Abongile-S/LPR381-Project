using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LPR381
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Input file path
            string inputFilePath = @"C:\LPR381\LPR381 PROJECT\input.txt";

            // Output file path
            string outputFilePath = @"C:\LPR381\LPR381 PROJECT\output.txt";

            // Read the input file
            InputFileReader reader = new InputFileReader();
            reader.Read(inputFilePath);

            // Display results in console
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

            // Write to output file
            OutputWriter.Write(outputFilePath, reader);
            Console.WriteLine("\n\nOutput written to: " + outputFilePath);

            Console.ReadLine();
        }
    }
}