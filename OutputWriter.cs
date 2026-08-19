using System;
using System.Collections.Generic;
using System.IO;

namespace LPR381
{
    public class OutputWriter
    {
        public static void Write(string filePath, InputFileReader reader)
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("===========================================");
                writer.WriteLine("         LP MODEL SUMMARY");
                writer.WriteLine("===========================================");
                writer.WriteLine("Objective Type: " + reader.ObjectiveType);
                writer.Write("Objective Coefficients: ");
                foreach (var c in reader.ObjCoefficients)
                    writer.Write(c + " ");
                writer.WriteLine();

                writer.WriteLine("\nConstraints:");
                foreach (var con in reader.Constraints)
                {
                    foreach (var c in con.Coefficients)
                        writer.Write(c + " ");
                    writer.WriteLine(con.Relation + " " + con.RHS);
                }

                writer.Write("\nSign Restrictions: ");
                foreach (var s in reader.SignRestrictions)
                    writer.Write(s + " ");
                writer.WriteLine();
                writer.WriteLine("===========================================");
            }
        }
    }
}