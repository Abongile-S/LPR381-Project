using System;
using System.Collections.Generic;
using System.IO;

namespace LPR381
{
    public class OutputWriter
    {
        public static void Write(string filePath, InputFileReader reader, string canonicalLog, string primalLog, string revisedLog, string bnbLog = "",
                                  string knapsackLog = "",
                                  string cuttingLog = "")
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                // ========== HEADER ==========
                WriteHeader(writer);
                
                // ========== MODEL SUMMARY ==========
                WriteModelSummary(writer, reader);
                
                // ========== CANONICAL FORM ==========
                if (!string.IsNullOrEmpty(canonicalLog))
                    WriteSection(writer, "CANONICAL FORM", canonicalLog);
                
                // ========== PRIMAL SIMPLEX ==========
                if (!string.IsNullOrEmpty(primalLog))
                    WriteSection(writer, "PRIMAL SIMPLEX", primalLog);
                
                // ========== REVISED SIMPLEX ==========
                if (!string.IsNullOrEmpty(revisedLog))
                    WriteSection(writer, "REVISED PRIMAL SIMPLEX", revisedLog);
                
                // ========== BRANCH AND BOUND SIMPLEX ==========
                if (!string.IsNullOrEmpty(bnbLog))
                    WriteSection(writer, "BRANCH AND BOUND SIMPLEX", bnbLog);
                
                // ========== BRANCH AND BOUND KNAPSACK ==========
                if (!string.IsNullOrEmpty(knapsackLog))
                    WriteSection(writer, "BRANCH AND BOUND KNAPSACK", knapsackLog);
                
                // ========== CUTTING PLANE ==========
                if (!string.IsNullOrEmpty(cuttingLog))
                    WriteSection(writer, "CUTTING PLANE", cuttingLog);
                
                // ========== FOOTER ==========
                WriteFooter(writer);
            }
        }

        // ========== HEADER ==========

        private static void WriteHeader(StreamWriter writer)
        {
            writer.WriteLine("========================================");
            writer.WriteLine("         LP MODEL SUMMARY");
            writer.WriteLine("========================================");
        }

        // ========== MODEL SUMMARY ==========

        private static void WriteModelSummary(StreamWriter writer, InputFileReader reader)
        {
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

        // ========== SECTION HEADER ==========

        private static void WriteSection(StreamWriter writer, string title, string content)
        {
            writer.WriteLine();
            writer.WriteLine("###########################################");
            writer.WriteLine($"#            {title}              #");
            writer.WriteLine("###########################################");
            writer.WriteLine(content);
        }

        // ========== FOOTER ==========

        private static void WriteFooter(StreamWriter writer)
        {
            writer.WriteLine();
            writer.WriteLine("========================================");
            writer.WriteLine("         END OF REPORT");
            writer.WriteLine("========================================");
        }
    }
}
         