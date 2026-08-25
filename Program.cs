using System;
using System.IO;
using LPR381.IP;
using LPR381.Events;
using LPR381.Utils;

namespace LPR381
{
    internal class Program
    {
        // ========== EVENT HANDLERS ==========

        private static void OnAlgorithmStarted(object sender, AlgorithmEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[START] {e.AlgorithmName} at {e.Timestamp:HH:mm:ss}");
            Console.ResetColor();
        }

        private static void OnAlgorithmProgress(object sender, AlgorithmEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[PROGRESS] {e.AlgorithmName}: {e.Message} ({e.Progress}%)");
            Console.ResetColor();
        }

        private static void OnAlgorithmCompleted(object sender, AlgorithmEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[COMPLETE] {e.AlgorithmName}: {e.Message}");
            Console.ResetColor();
        }

        private static void OnSolutionFound(object sender, SolutionFoundEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[SOLUTION] {e.AlgorithmName}: Z = {e.ObjectiveValue:F3}");
            Console.ResetColor();
        }

        private static void OnError(object sender, LPR381.Events.ErrorEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] {e.AlgorithmName}: {e.ErrorMessage}");
            if (e.Exception != null)
            {
                Console.WriteLine($"  {e.Exception.Message}");
            }
            Console.ResetColor();
        }

        static void Main(string[] args)
        {
            // Subscribe to events
            EventManager.OnAlgorithmStarted += OnAlgorithmStarted;
            EventManager.OnAlgorithmProgress += OnAlgorithmProgress;
            EventManager.OnAlgorithmCompleted += OnAlgorithmCompleted;
            EventManager.OnSolutionFound += OnSolutionFound;
            EventManager.OnError += OnError;

            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string projectPath = Path.GetFullPath(Path.Combine(basePath, @"..\..\"));
                string inputFilePath = Path.Combine(projectPath, "input.txt");
                string outputFilePath = Path.Combine(projectPath, "output.txt");

                Console.WriteLine("========================================");
                Console.WriteLine(" LPR381 - Operations Research Solver");
                Console.WriteLine(" Linear & Integer Programming - Project 2026");
                Console.WriteLine("========================================");
                Console.WriteLine();
                Console.WriteLine("Input file path: " + inputFilePath);
                Console.WriteLine("Output file path: " + outputFilePath);
                Console.WriteLine();

                // Validate input file
                if (!ErrorHandler.ValidateInputFile(inputFilePath))
                {
                    Console.WriteLine("[ERROR] Invalid input file. Check error_log.txt for details.");
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }

                InputFileReader reader = new InputFileReader();
                reader.Read(inputFilePath);

                Console.WriteLine("Model Summary:");
                Console.WriteLine($"  Type: {reader.ObjectiveType}");
                Console.WriteLine($"  Variables: {reader.ObjCoefficients.Count}");
                Console.WriteLine($"  Constraints: {reader.Constraints.Count}");
                Console.WriteLine();

                // ===== MENU SYSTEM =====
                bool exit = false;
                while (!exit)
                {
                    Console.WriteLine("========================================");
                    Console.WriteLine(" MAIN MENU");
                    Console.WriteLine("========================================");
                    Console.WriteLine("  1. Primal Simplex");
                    Console.WriteLine("  2. Revised Primal Simplex");
                    Console.WriteLine("  3. Branch and Bound Simplex (IP)");
                    Console.WriteLine("  4. Branch and Bound Knapsack (IP)");
                    Console.WriteLine("  5. Cutting Plane (IP)");
                    Console.WriteLine("  6. Run All Algorithms");
                    Console.WriteLine("  0. Exit");
                    Console.WriteLine();
                    Console.Write("Enter choice: ");

                    string choice = Console.ReadLine()?.Trim();

                    switch (choice)
                    {
                        case "1":
                            RunPrimalSimplex(reader, outputFilePath);
                            break;
                        case "2":
                            RunRevisedSimplex(reader, outputFilePath);
                            break;
                        case "3":
                            RunBranchAndBoundSimplex(reader, outputFilePath);
                            break;
                        case "4":
                            RunBranchAndBoundKnapsack(reader, outputFilePath);
                            break;
                        case "5":
                            RunCuttingPlane(reader, outputFilePath);
                            break;
                        case "6":
                            RunAllAlgorithms(reader, outputFilePath);
                            break;
                        case "0":
                            exit = true;
                            break;
                        default:
                            Console.WriteLine("[ERROR] Invalid choice. Please try again.");
                            break;
                    }

                    if (!exit)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Press any key to continue...");
                        Console.ReadKey();
                        Console.Clear();
                    }
                }

                Console.WriteLine("========================================");
                Console.WriteLine(" Thank you for using LPR381!");
                Console.WriteLine("========================================");

                if (ErrorHandler.HasErrors)
                {
                    ErrorHandler.WriteErrorReport();
                    Console.WriteLine("Errors were logged to error_report.txt");
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.HandleException(ex, "Program.Main");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
            finally
            {
                EventManager.OnAlgorithmStarted -= OnAlgorithmStarted;
                EventManager.OnAlgorithmProgress -= OnAlgorithmProgress;
                EventManager.OnAlgorithmCompleted -= OnAlgorithmCompleted;
                EventManager.OnSolutionFound -= OnSolutionFound;
                EventManager.OnError -= OnError;
            }
        }

        // ========== PRIMAL SIMPLEX ==========

        private static void RunPrimalSimplex(InputFileReader reader, string outputFilePath)
        {
            Console.WriteLine("\n>>> Running Primal Simplex...");
            Console.WriteLine(new string('-', 50));

            CanonicalForm canonical = new CanonicalForm();
            canonical.Convert(reader);
            canonical.Display();

            PrimalSimplex primal = new PrimalSimplex();
            primal.Solve(canonical, reader.ObjectiveType);

            OutputWriter.Write(outputFilePath, reader, canonical.GetDisplayString(), primal.Log, "");
            Console.WriteLine(primal.Log);
            Console.WriteLine($"[OK] Output written to: {outputFilePath}");
        }

        // ========== REVISED SIMPLEX ==========

        private static void RunRevisedSimplex(InputFileReader reader, string outputFilePath)
        {
            Console.WriteLine("\n>>> Running Revised Primal Simplex...");
            Console.WriteLine(new string('-', 50));

            RevisedSimplex revised = new RevisedSimplex();
            revised.Solve(reader);

            OutputWriter.Write(outputFilePath, reader, "", "", revised.Log);
            Console.WriteLine(revised.Log);
            Console.WriteLine($"[OK] Output written to: {outputFilePath}");
        }

        // ========== BRANCH AND BOUND SIMPLEX ==========

        private static void RunBranchAndBoundSimplex(InputFileReader reader, string outputFilePath)
        {
            Console.WriteLine("\n>>> Running Branch and Bound Simplex...");
            Console.WriteLine(new string('-', 50));

            var bnb = new BranchAndBoundSimplex();
            bnb.Solve(reader);

            OutputWriter.Write(outputFilePath, reader, "", "", "", bnb.Log, "", "");
            Console.WriteLine(bnb.Log);
            Console.WriteLine($"[OK] Output written to: {outputFilePath}");
        }

        // ========== BRANCH AND BOUND KNAPSACK ==========

        private static void RunBranchAndBoundKnapsack(InputFileReader reader, string outputFilePath)
        {
            Console.WriteLine("\n>>> Running Branch and Bound Knapsack...");
            Console.WriteLine(new string('-', 50));

            var knapsack = new BranchAndBoundKnapsack();
            knapsack.Solve(reader);

            OutputWriter.Write(outputFilePath, reader, "", "", "", "", knapsack.Log, "");
            Console.WriteLine(knapsack.Log);
            Console.WriteLine($"[OK] Output written to: {outputFilePath}");
        }

        // ========== CUTTING PLANE ==========

        private static void RunCuttingPlane(InputFileReader reader, string outputFilePath)
        {
            Console.WriteLine("\n>>> Running Cutting Plane...");
            Console.WriteLine(new string('-', 50));

            var cutting = new CuttingPlane();
            cutting.Solve(reader);

            OutputWriter.Write(outputFilePath, reader, "", "", "", "", "", cutting.Log);
            Console.WriteLine(cutting.Log);
            Console.WriteLine($"[OK] Output written to: {outputFilePath}");
        }

        // ========== RUN ALL ALGORITHMS ==========

        private static void RunAllAlgorithms(InputFileReader reader, string outputFilePath)
        {
            Console.WriteLine("\n>>> Running All Algorithms...");
            Console.WriteLine(new string('-', 50));

            RunPrimalSimplex(reader, outputFilePath);
            RunRevisedSimplex(reader, outputFilePath);
            RunBranchAndBoundSimplex(reader, outputFilePath);
            RunBranchAndBoundKnapsack(reader, outputFilePath);
            RunCuttingPlane(reader, outputFilePath);

            Console.WriteLine("\n[OK] All algorithms completed!");
        }
    }
}