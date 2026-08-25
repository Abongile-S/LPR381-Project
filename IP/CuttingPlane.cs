using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LPR381.Events;
using LPR381.Utils;

namespace LPR381.IP
{
    public class CuttingPlane
    {
        // ===== Configuration =====
        private double tolerance = 1e-6;
        private int maxIterations = 100;

        // ===== Private Fields =====
        private InputFileReader currentReader;
        private int numVars;
        private int numConstraints;
        private List<string> signRestrictions;
        private string objectiveType;

        private double[] bestSolution;
        private double bestObjective;
        private bool solutionFound;
        private int iteration;

        // ===== Logging =====
        private readonly StringBuilder log = new StringBuilder();
        public string Log => log.ToString();

        // ===== Constructor =====
        public CuttingPlane()
        {
            bestSolution = Array.Empty<double>();
            bestObjective = 0;
            solutionFound = false;
            iteration = 0;
        }

        // ===== Main Solver =====
        public void Solve(InputFileReader reader)
        {
            try
            {
                EventManager.RaiseAlgorithmStarted("Cutting Plane");

                this.currentReader = CloneReader(reader);
                this.numVars = reader.ObjCoefficients.Count;
                this.numConstraints = reader.Constraints.Count;
                this.signRestrictions = new List<string>(reader.SignRestrictions);
                this.objectiveType = reader.ObjectiveType;

                bestSolution = new double[numVars];

                LogMessage("========================================");
                LogMessage(" CUTTING PLANE ALGORITHM");
                LogMessage("========================================");
                LogMessage($"Objective: {objectiveType.ToUpper()}");
                LogMessage($"Variables: {numVars}");
                LogMessage($"Constraints: {numConstraints}");
                LogMessage($"Binary Variables: {signRestrictions.Count(s => s == "bin")}");
                LogMessage("");

                LogMessage("--- ITERATION 0: LP RELAXATION ---");
                var lpResult = SolveLP();

                if (!lpResult.IsFeasible)
                {
                    LogMessage("[ERROR] LP relaxation is infeasible.");
                    EventManager.RaiseError("Cutting Plane", "LP relaxation is infeasible");
                    return;
                }

                LogMessage($"LP Relaxation Solution: Z = {lpResult.ObjectiveValue:F3}");
                LogMessage($"Solution: [{string.Join(", ", lpResult.Solution.Select(v => v.ToString("F3")))}]");
                LogMessage("");

                if (IsIntegerSolution(lpResult.Solution))
                {
                    LogMessage("[SUCCESS] INTEGER SOLUTION FOUND AT LP RELAXATION");
                    bestSolution = lpResult.Solution;
                    bestObjective = lpResult.ObjectiveValue;
                    solutionFound = true;
                    DisplayBestSolution();
                    EventManager.RaiseSolutionFound("Cutting Plane", bestObjective, bestSolution);
                    return;
                }

                LogMessage("--- STARTING GOMORY CUTS ---");
                LogMessage("");

                int cutCount = 0;
                bool optimalFound = false;

                while (iteration < maxIterations && !optimalFound)
                {
                    iteration++;
                    cutCount++;

                    LogMessage($"--- ITERATION {iteration}: ADDING GOMORY CUT ---");

                    // Find fractional variable
                    int fracVarIndex = -1;
                    double fracValue = 0;
                    double fracPart = 0;

                    for (int i = 0; i < lpResult.Solution.Length; i++)
                    {
                        if (signRestrictions[i] == "bin" || signRestrictions[i] == "int")
                        {
                            double val = Math.Abs(lpResult.Solution[i] - Math.Round(lpResult.Solution[i]));
                            if (val > tolerance)
                            {
                                fracVarIndex = i;
                                fracValue = lpResult.Solution[i];
                                fracPart = val;
                                break;
                            }
                        }
                    }

                    if (fracVarIndex == -1)
                    {
                        LogMessage("[SUCCESS] All basic variables are integer.");
                        optimalFound = true;
                        break;
                    }

                    // Generate Gomory cut
                    double floorVal = Math.Floor(fracValue);
                    double ceilVal = Math.Ceiling(fracValue);
                    string cut;

                    if (fracPart > 0.5)
                    {
                        cut = $"x{fracVarIndex + 1} <= {floorVal:F0}";
                    }
                    else
                    {
                        cut = $"x{fracVarIndex + 1} >= {ceilVal:F0}";
                    }

                    LogMessage($"Gomory Cut Generated:");
                    LogMessage($"  {cut}");

                    // Add cut constraint
                    var parts = cut.Split(' ');
                    if (parts.Length >= 3)
                    {
                        string varPart = parts[0];
                        string op = parts[1];
                        string valPart = parts[2];

                        int varIndex = int.Parse(varPart.Substring(1)) - 1;
                        double value = double.Parse(valPart);

                        var coeffs = new double[numVars];
                        coeffs[varIndex] = (op == "<=") ? 1 : -1;
                        double rhs = (op == "<=") ? value : -value;

                        currentReader.Constraints.Add(new Constraint(
                            coeffs.ToList(),
                            "<=",
                            rhs
                        ));

                        numConstraints++;
                    }

                    LogMessage($"Re-solving with cut {cutCount}...");
                    lpResult = SolveLP();

                    if (!lpResult.IsFeasible)
                    {
                        LogMessage("[ERROR] LP became infeasible after adding cut.");
                        break;
                    }

                    LogMessage($"New Solution: Z = {lpResult.ObjectiveValue:F3}");
                    LogMessage($"Solution: [{string.Join(", ", lpResult.Solution.Select(v => v.ToString("F3")))}]");

                    if (IsIntegerSolution(lpResult.Solution))
                    {
                        LogMessage("[SUCCESS] INTEGER SOLUTION FOUND!");
                        bestSolution = lpResult.Solution;
                        bestObjective = lpResult.ObjectiveValue;
                        solutionFound = true;
                        optimalFound = true;
                        EventManager.RaiseSolutionFound("Cutting Plane", bestObjective, bestSolution);
                    }

                    LogMessage("");
                }

                if (iteration >= maxIterations)
                {
                    LogMessage($"[WARNING] Max iterations reached ({maxIterations})");
                    ErrorHandler.HandleWarning($"Max iterations reached ({maxIterations})", "Cutting Plane");
                }

                LogMessage("");
                LogMessage("--- BEST CANDIDATE ---");
                if (solutionFound)
                {
                    DisplayBestSolution();
                    EventManager.RaiseAlgorithmCompleted("Cutting Plane", $"Solution found: Z = {bestObjective:F3}");
                }
                else
                {
                    LogMessage("[ERROR] No feasible integer solution found.");
                    EventManager.RaiseError("Cutting Plane", "No feasible integer solution found");
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.HandleException(ex, "Cutting Plane");
                EventManager.RaiseError("Cutting Plane", ex.Message);
                LogMessage($"[ERROR] {ex.Message}");
            }
        }

        private LPSolutionResult SolveLP()
        {
            var result = new LPSolutionResult();

            try
            {
                var revised = new RevisedSimplex();
                revised.Solve(currentReader);

                result.Solution = ParseSolution(revised.Log);
                result.ObjectiveValue = ParseObjective(revised.Log);
                result.IsFeasible = true;
            }
            catch (Exception)
            {
                result.IsFeasible = false;
                result.Solution = new double[numVars];
                result.ObjectiveValue = double.MinValue;
            }

            return result;
        }

        private bool IsIntegerSolution(double[] solution)
        {
            for (int i = 0; i < solution.Length; i++)
            {
                if (signRestrictions[i] == "bin" || signRestrictions[i] == "int")
                {
                    double val = Math.Abs(solution[i] - Math.Round(solution[i]));
                    if (val > tolerance)
                        return false;

                    if (signRestrictions[i] == "bin")
                    {
                        if (solution[i] < -tolerance || solution[i] > 1 + tolerance)
                            return false;
                    }
                }
            }
            return true;
        }

        private double[] ParseSolution(string logText)
        {
            var values = new double[numVars];
            var lines = logText.Split('\n');

            foreach (var line in lines)
            {
                for (int i = 0; i < numVars; i++)
                {
                    if (line.Contains($"x{i + 1} ="))
                    {
                        var parts = line.Split('=');
                        if (parts.Length >= 2)
                        {
                            string valStr = parts[1].Trim().Replace(",", ".");
                            if (double.TryParse(valStr, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out double val))
                            {
                                values[i] = val;
                            }
                        }
                    }
                }
            }

            return values;
        }

        private double ParseObjective(string logText)
        {
            var lines = logText.Split('\n');
            foreach (var line in lines)
            {
                if (line.Contains("Optimal Z =") || line.Contains("Z ="))
                {
                    var parts = line.Split('=');
                    if (parts.Length >= 2)
                    {
                        string valStr = parts[1].Trim().Replace(",", ".");
                        if (double.TryParse(valStr, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out double val))
                        {
                            return val;
                        }
                    }
                }
            }
            return double.MinValue;
        }

        private InputFileReader CloneReader(InputFileReader original)
        {
            var clone = new InputFileReader();
            var type = typeof(InputFileReader);

            type.GetProperty("ObjectiveType")?.SetValue(clone, original.ObjectiveType);
            type.GetProperty("ObjCoefficients")?.SetValue(clone, new List<double>(original.ObjCoefficients));
            type.GetProperty("SignRestrictions")?.SetValue(clone, new List<string>(original.SignRestrictions));

            var newConstraints = new List<Constraint>();
            foreach (var c in original.Constraints)
            {
                newConstraints.Add(new Constraint(
                    new List<double>(c.Coefficients),
                    c.Relation,
                    c.RHS
                ));
            }
            type.GetProperty("Constraints")?.SetValue(clone, newConstraints);

            return clone;
        }

        private void LogMessage(string message, int indent = 0)
        {
            string prefix = new string(' ', indent * 2);
            string line = $"{prefix}{message}";
            Console.WriteLine(line);
            log.AppendLine(line);
        }

        private void DisplayBestSolution()
        {
            LogMessage("========================================");
            LogMessage(" BEST INTEGER SOLUTION");
            LogMessage("========================================");
            for (int i = 0; i < bestSolution.Length; i++)
            {
                LogMessage($"x{i + 1} = {bestSolution[i]:F3}");
            }
            LogMessage("");
            LogMessage($"Optimal Z = {bestObjective:F3}");
            LogMessage($"Iterations: {iteration}");
            LogMessage("========================================");
        }

        private class LPSolutionResult
        {
            public double[] Solution { get; set; } = Array.Empty<double>();
            public double ObjectiveValue { get; set; }
            public bool IsFeasible { get; set; } = true;
        }
    }
}