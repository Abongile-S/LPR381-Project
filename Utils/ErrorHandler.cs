using System;
using System.Collections.Generic;
using System.IO;

namespace LPR381.Utils
{
    public static class ErrorHandler
    {
        private static List<string> errorLog = new List<string>();
        private static bool hasErrors = false;

        public static bool HasErrors => hasErrors;
        public static IReadOnlyList<string> ErrorLog => errorLog.AsReadOnly();

        public static void HandleException(Exception ex, string context = "")
        {
            string errorMsg = $"[ERROR] {context}: {ex.Message}";
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(errorMsg);
            Console.ResetColor();

            errorLog.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {errorMsg}");
            errorLog.Add($"  Stack Trace: {ex.StackTrace}");
            hasErrors = true;

            try
            {
                File.AppendAllText("error_log.txt",
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {context}: {ex.Message}\n{ex.StackTrace}\n\n");
            }
            catch { /* Ignore logging errors */ }
        }

        public static void HandleWarning(string warning, string context = "")
        {
            string warnMsg = $"[WARNING] {context}: {warning}";
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(warnMsg);
            Console.ResetColor();

            errorLog.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {warnMsg}");
        }

        public static void HandleInfo(string info, string context = "")
        {
            string infoMsg = $"[INFO] {context}: {info}";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(infoMsg);
            Console.ResetColor();
        }

        public static bool ValidateInputFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    HandleWarning($"File not found: {filePath}", "Input Validation");
                    return false;
                }

                var lines = File.ReadAllLines(filePath);
                if (lines.Length < 3)
                {
                    HandleWarning("Input file has fewer than 3 lines", "Input Validation");
                    return false;
                }

                var parts = lines[0].Trim().Split(' ');
                if (parts.Length < 2)
                {
                    HandleWarning("First line must have objective type and coefficients", "Input Validation");
                    return false;
                }

                if (parts[0].ToLower() != "max" && parts[0].ToLower() != "min")
                {
                    HandleWarning($"Objective type must be 'max' or 'min', got '{parts[0]}'", "Input Validation");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                HandleException(ex, "Input Validation");
                return false;
            }
        }

        public static void ClearErrors()
        {
            errorLog.Clear();
            hasErrors = false;
        }

        public static void WriteErrorReport(string filePath = "error_report.txt")
        {
            try
            {
                using (var writer = new StreamWriter(filePath))
                {
                    writer.WriteLine("========================================");
                    writer.WriteLine(" ERROR REPORT");
                    writer.WriteLine($" Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine("========================================");
                    writer.WriteLine();

                    if (errorLog.Count == 0)
                    {
                        writer.WriteLine("No errors reported.");
                    }
                    else
                    {
                        foreach (var error in errorLog)
                        {
                            writer.WriteLine(error);
                        }
                    }

                    writer.WriteLine();
                    writer.WriteLine("========================================");
                    writer.WriteLine(" END OF REPORT");
                    writer.WriteLine("========================================");
                }
            }
            catch { /* Ignore */ }
        }
    }
}