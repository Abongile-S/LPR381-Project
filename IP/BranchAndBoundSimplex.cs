using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using LPR381.Events;
using LPR381.Utils;

namespace LPR381.IP
{
    public class BranchAndBoundSimplex
    {
        // ============================================================
        // CONFIGURATION
        // ============================================================

        private const double Tolerance = 1e-6;
        private const int MaxNodes = 1000;

        // ============================================================
        // PRIVATE FIELDS
        // ============================================================

        private InputFileReader originalReader;
        private int numVars;

        private double bestObjective;
        private double[] bestSolution;

        private bool solutionFound;
        private int nodeCounter;

        private bool isMaximization;

        // ============================================================
        // LOGGING
        // ============================================================

        private readonly StringBuilder log = new StringBuilder();

        public string Log
        {
            get { return log.ToString(); }
        }

        // ============================================================
        // CONSTRUCTOR
        // ============================================================

        public BranchAndBoundSimplex()
        {
            bestObjective = double.MinValue;
            bestSolution = new double[0];
            solutionFound = false;
            nodeCounter = 0;
            isMaximization = true;
        }

        // ============================================================
        // MAIN SOLVER
        // ============================================================

        public void Solve(InputFileReader reader)
        {
            try
            {
                EventManager.RaiseAlgorithmStarted(
                    "Branch and Bound Simplex"
                );

                originalReader = reader;

                numVars = reader.ObjCoefficients.Count;

                isMaximization =
                    reader.ObjectiveType.Trim().ToLower() == "max";

                bestSolution = new double[numVars];

                solutionFound = false;
                nodeCounter = 0;

                if (isMaximization)
                    bestObjective = double.MinValue;
                else
                    bestObjective = double.MaxValue;

                log.Clear();

                LogMessage("========================================");
                LogMessage(" BRANCH AND BOUND SIMPLEX ALGORITHM");
                LogMessage("========================================");

                LogMessage(
                    "Objective: " +
                    reader.ObjectiveType.ToUpper()
                );

                LogMessage(
                    "Variables: " +
                    numVars
                );

                LogMessage(
                    "Constraints: " +
                    reader.Constraints.Count
                );

                LogMessage("");

                // ====================================================
                // ROOT LP RELAXATION
                // ====================================================

                LogMessage("--- ROOT NODE: LP RELAXATION ---");

                LPSolutionResult rootResult =
                    SolveLPRelaxation(reader);

                if (!rootResult.IsFeasible)
                {
                    LogMessage(
                        "[ERROR] LP relaxation is infeasible."
                    );

                    EventManager.RaiseError(
                        "Branch and Bound Simplex",
                        "LP relaxation is infeasible"
                    );

                    return;
                }

                LogMessage(
                    "Root LP Solution: Z = " +
                    rootResult.ObjectiveValue.ToString("F3")
                );

                LogMessage(
                    "Solution: [" +
                    string.Join(
                        ", ",
                        rootResult.Solution.Select(
                            x => x.ToString("F3")
                        )
                    ) +
                    "]"
                );

                LogMessage("");

                // ====================================================
                // CHECK ROOT INTEGER
                // ====================================================

                if (IsIntegerSolution(rootResult.Solution))
                {
                    LogMessage(
                        "[SUCCESS] INTEGER SOLUTION FOUND AT ROOT NODE"
                    );

                    bestObjective =
                        rootResult.ObjectiveValue;

                    bestSolution =
                        (double[])rootResult.Solution.Clone();

                    solutionFound = true;

                    DisplayBestSolution();

                    EventManager.RaiseSolutionFound(
                        "Branch and Bound Simplex",
                        bestObjective,
                        bestSolution
                    );

                    EventManager.RaiseAlgorithmCompleted(
                        "Branch and Bound Simplex",
                        "Integer solution found at root"
                    );

                    return;
                }

                // ====================================================
                // CREATE ROOT NODE
                // ====================================================

                BnbNode rootNode = new BnbNode();

                rootNode.Id = nodeCounter++;
                rootNode.Solution = rootResult.Solution;
                rootNode.ObjectiveValue =
                    rootResult.ObjectiveValue;

                rootNode.Branches =
                    new List<BranchConstraint>();

                rootNode.Depth = 0;
                rootNode.Status = NodeStatus.Active;
                rootNode.IsFeasible = true;

                // ====================================================
                // DFS STACK
                // ====================================================

                Stack<BnbNode> stack =
                    new Stack<BnbNode>();

                stack.Push(rootNode);

                // ====================================================
                // BRANCH AND BOUND
                // ====================================================

                while (
                    stack.Count > 0 &&
                    nodeCounter < MaxNodes &&
                    !solutionFound
                )
                {
                    BnbNode node = stack.Pop();

                    LogMessage(
                        "--- PROCESSING NODE " +
                        node.Id +
                        " ---"
                    );

                    LogMessage(
                        "Depth: " +
                        node.Depth
                    );

                    LogMessage(
                        "LP Bound: Z = " +
                        node.ObjectiveValue.ToString("F3")
                    );

                    // ------------------------------------------------
                    // BOUND CHECK
                    // ------------------------------------------------

                    if (HasWorseBound(node.ObjectiveValue))
                    {
                        LogMessage(
                            "Node " +
                            node.Id +
                            " FATHOMED BY BOUND"
                        );

                        continue;
                    }

                    // ------------------------------------------------
                    // INTEGER CHECK
                    // ------------------------------------------------

                    if (IsIntegerSolution(node.Solution))
                    {
                        LogMessage(
                            "Node " +
                            node.Id +
                            " INTEGER SOLUTION"
                        );

                        if (
                            !solutionFound ||
                            IsBetter(
                                node.ObjectiveValue,
                                bestObjective
                            )
                        )
                        {
                            bestObjective =
                                node.ObjectiveValue;

                            bestSolution =
                                (double[])node.Solution.Clone();

                            solutionFound = true;

                            DisplayBestSolution();

                            EventManager.RaiseSolutionFound(
                                "Branch and Bound Simplex",
                                bestObjective,
                                bestSolution
                            );

                            /*
                             * We do NOT immediately stop here.
                             *
                             * The LP relaxation at other nodes can
                             * potentially provide a better integer
                             * solution.
                             */

                            continue;
                        }

                        continue;
                    }

                    // ------------------------------------------------
                    // FIND FRACTIONAL VARIABLE
                    // ------------------------------------------------

                    int branchingVariable =
                        GetBranchingVariable(
                            node.Solution
                        );

                    if (branchingVariable == -1)
                    {
                        LogMessage(
                            "No valid branching variable."
                        );

                        continue;
                    }

                    double value =
                        node.Solution[branchingVariable];

                    double floorValue =
                        Math.Floor(value);

                    double ceilValue =
                        Math.Ceiling(value);

                    LogMessage(
                        "Branching on x" +
                        (branchingVariable + 1) +
                        " = " +
                        value.ToString("F3")
                    );

                    // =================================================
                    // LEFT CHILD: x <= floor(x)
                    // =================================================

                    BnbNode leftChild =
                        CreateChildNode(
                            node,
                            branchingVariable,
                            "<=",
                            floorValue
                        );

                    if (
                        leftChild != null &&
                        leftChild.IsFeasible
                    )
                    {
                        stack.Push(leftChild);
                    }

                    // =================================================
                    // RIGHT CHILD: x >= ceil(x)
                    // =================================================

                    BnbNode rightChild =
                        CreateChildNode(
                            node,
                            branchingVariable,
                            ">=",
                            ceilValue
                        );

                    if (
                        rightChild != null &&
                        rightChild.IsFeasible
                    )
                    {
                        stack.Push(rightChild);
                    }

                    LogMessage(
                        "Nodes currently in stack: " +
                        stack.Count
                    );

                    LogMessage("");
                }

                // ====================================================
                // MAX NODE WARNING
                // ====================================================

                if (nodeCounter >= MaxNodes)
                {
                    LogMessage(
                        "[WARNING] Maximum node limit reached: " +
                        MaxNodes
                    );

                    ErrorHandler.HandleWarning(
                        "Maximum node limit reached: " +
                        MaxNodes,
                        "Branch and Bound Simplex"
                    );
                }

                // ====================================================
                // FINAL RESULT
                // ====================================================

                LogMessage("");
                LogMessage("--- FINAL RESULT ---");

                if (solutionFound)
                {
                    DisplayBestSolution();

                    EventManager.RaiseAlgorithmCompleted(
                        "Branch and Bound Simplex",
                        "Integer solution found"
                    );
                }
                else
                {
                    LogMessage(
                        "[ERROR] No feasible integer solution found."
                    );

                    EventManager.RaiseError(
                        "Branch and Bound Simplex",
                        "No feasible integer solution found"
                    );
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.HandleException(
                    ex,
                    "Branch and Bound Simplex"
                );

                EventManager.RaiseError(
                    "Branch and Bound Simplex",
                    ex.Message
                );

                LogMessage(
                    "[ERROR] " +
                    ex.Message
                );
            }
        }

        // ============================================================
        // CREATE CHILD NODE
        // ============================================================

        private BnbNode CreateChildNode(
            BnbNode parent,
            int variableIndex,
            string operation,
            double value)
        {
            try
            {
                /*
                 * Prevent duplicate/invalid branches.
                 */

                foreach (
                    BranchConstraint branch
                    in parent.Branches)
                {
                    if (
                        branch.VariableIndex ==
                        variableIndex &&
                        branch.Operator == operation &&
                        Math.Abs(
                            branch.Value - value
                        ) < Tolerance
                    )
                    {
                        return null;
                    }
                }

                /*
                 * Clone the original model and add ALL
                 * parent branch constraints.
                 */

                InputFileReader childReader =
                    CloneReader(originalReader);

                foreach (
                    BranchConstraint branch
                    in parent.Branches)
                {
                    AddBranchConstraint(
                        childReader,
                        branch.VariableIndex,
                        branch.Operator,
                        branch.Value
                    );
                }

                /*
                 * Add the new branch constraint.
                 */

                AddBranchConstraint(
                    childReader,
                    variableIndex,
                    operation,
                    value
                );

                /*
                 * Solve the LP relaxation.
                 */

                LPSolutionResult result =
                    SolveLPRelaxation(childReader);

                if (!result.IsFeasible)
                {
                    LogMessage(
                        "Child node infeasible.",
                        1
                    );

                    return null;
                }

                /*
                 * Check bound immediately.
                 */

                if (HasWorseBound(
                    result.ObjectiveValue))
                {
                    LogMessage(
                        "Child node pruned by bound.",
                        1
                    );

                    return null;
                }

                /*
                 * Build branch history.
                 */

                List<BranchConstraint> branches =
                    new List<BranchConstraint>(
                        parent.Branches
                    );

                branches.Add(
                    new BranchConstraint
                    {
                        VariableIndex =
                            variableIndex,

                        Operator =
                            operation,

                        Value =
                            value
                    }
                );

                BnbNode child =
                    new BnbNode();

                child.Id = nodeCounter++;

                child.Solution =
                    result.Solution;

                child.ObjectiveValue =
                    result.ObjectiveValue;

                child.Branches =
                    branches;

                child.Status =
                    NodeStatus.Active;

                child.Depth =
                    parent.Depth + 1;

                child.ParentId =
                    parent.Id;

                child.IsFeasible = true;

                LogMessage(
                    "Created Node " +
                    child.Id +
                    ": x" +
                    (variableIndex + 1) +
                    " " +
                    operation +
                    " " +
                    value.ToString("F0"),
                    1
                );

                return child;
            }
            catch (Exception ex)
            {
                LogMessage(
                    "[ERROR] Creating child node: " +
                    ex.Message,
                    1
                );

                return null;
            }
        }

        // ============================================================
        // ADD BRANCH CONSTRAINT
        // ============================================================

        private void AddBranchConstraint(
            InputFileReader reader,
            int variableIndex,
            string operation,
            double value)
        {
            /*
             * We cannot directly replace reader.Constraints
             * because its setter is private.
             *
             * However, Add() is allowed because the List itself
             * is returned by the getter.
             */

            List<double> coefficients =
                new List<double>();

            for (int i = 0; i < numVars; i++)
                coefficients.Add(0);

            if (operation == "<=")
            {
                coefficients[variableIndex] = 1;

                reader.Constraints.Add(
                    new Constraint(
                        coefficients,
                        "<=",
                        value
                    )
                );
            }
            else
            {
                /*
                 * x >= value
                 *
                 * Convert to:
                 *
                 * -x <= -value
                 */

                coefficients[variableIndex] = -1;

                reader.Constraints.Add(
                    new Constraint(
                        coefficients,
                        "<=",
                        -value
                    )
                );
            }
        }

        // ============================================================
        // CLONE READER
        // ============================================================

        private InputFileReader CloneReader(
            InputFileReader original)
        {
            /*
             * Because InputFileReader has private setters,
             * we create a temporary input file and let the
             * existing Read() method populate the new object.
             */

            string tempFile =
                Path.Combine(
                    Path.GetTempPath(),
                    "LPR381_BNB_" +
                    Guid.NewGuid().ToString("N") +
                    ".txt"
                );

            try
            {
                using (StreamWriter writer =
                    new StreamWriter(tempFile))
                {
                    /*
                     * Objective
                     */

                    writer.Write(
                        original.ObjectiveType
                    );

                    foreach (
                        double coefficient
                        in original.ObjCoefficients)
                    {
                        writer.Write(
                            " " +
                            coefficient.ToString(
                                CultureInfo.InvariantCulture
                            )
                        );
                    }

                    writer.WriteLine();

                    /*
                     * Constraints
                     */

                    foreach (
                        Constraint constraint
                        in original.Constraints)
                    {
                        for (
                            int i = 0;
                            i < constraint.Coefficients.Count;
                            i++)
                        {
                            if (i > 0)
                                writer.Write(" ");

                            writer.Write(
                                constraint.Coefficients[i]
                                    .ToString(
                                        CultureInfo.InvariantCulture
                                    )
                            );
                        }

                        writer.Write(
                            " " +
                            constraint.Relation +
                            " " +
                            constraint.RHS.ToString(
                                CultureInfo.InvariantCulture
                            )
                        );

                        writer.WriteLine();
                    }

                    /*
                     * Sign restrictions
                     */

                    writer.WriteLine(
                        string.Join(
                            " ",
                            original.SignRestrictions
                        )
                    );
                }

                InputFileReader clone =
                    new InputFileReader();

                clone.Read(tempFile);

                return clone;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                catch
                {
                    // Ignore cleanup errors.
                }
            }
        }

        // ============================================================
        // SOLVE LP RELAXATION
        // ============================================================

        private LPSolutionResult SolveLPRelaxation(
            InputFileReader reader)
        {
            LPSolutionResult result =
                new LPSolutionResult();

            try
            {
                CanonicalForm canonical =
                    new CanonicalForm();

                canonical.Convert(reader);

                RevisedSimplex revised =
                    new RevisedSimplex();

                revised.Solve(reader);

                string solverLog =
                    revised.Log;

                result.Solution =
                    ParseSolution(solverLog);

                result.ObjectiveValue =
                    ParseObjective(solverLog);

                /*
                 * If we couldn't obtain a valid objective,
                 * treat the relaxation as infeasible.
                 */

                if (
                    result.Solution == null ||
                    result.Solution.Length != numVars ||
                    result.ObjectiveValue ==
                    double.MinValue
                )
                {
                    result.IsFeasible = false;
                    return result;
                }

                /*
                 * Make sure the solution actually satisfies
                 * all constraints.
                 */

                if (!SatisfiesConstraints(
                    reader,
                    result.Solution))
                {
                    result.IsFeasible = false;
                    return result;
                }

                result.IsFeasible = true;
            }
            catch
            {
                result.IsFeasible = false;

                result.Solution =
                    new double[numVars];

                result.ObjectiveValue =
                    double.MinValue;
            }

            return result;
        }

        // ============================================================
        // CHECK CONSTRAINTS
        // ============================================================

        private bool SatisfiesConstraints(
            InputFileReader reader,
            double[] solution)
        {
            foreach (
                Constraint constraint
                in reader.Constraints)
            {
                double lhs = 0;

                for (int i = 0; i < numVars; i++)
                {
                    lhs +=
                        constraint.Coefficients[i] *
                        solution[i];
                }

                if (constraint.Relation == "<=")
                {
                    if (
                        lhs >
                        constraint.RHS + Tolerance
                    )
                        return false;
                }
                else if (
                    constraint.Relation == ">=")
                {
                    if (
                        lhs <
                        constraint.RHS - Tolerance
                    )
                        return false;
                }
                else if (
                    constraint.Relation == "=")
                {
                    if (
                        Math.Abs(
                            lhs - constraint.RHS
                        ) > Tolerance
                    )
                        return false;
                }
            }

            /*
             * Check binary variables.
             */

            for (int i = 0; i < numVars; i++)
            {
                if (
                    reader.SignRestrictions[i] ==
                    "bin"
                )
                {
                    if (
                        solution[i] < -Tolerance ||
                        solution[i] > 1 + Tolerance
                    )
                        return false;
                }
            }

            return true;
        }

        // ============================================================
        // INTEGER SOLUTION CHECK
        // ============================================================

        private bool IsIntegerSolution(
            double[] solution)
        {
            for (int i = 0; i < solution.Length; i++)
            {
                string restriction =
                    originalReader
                        .SignRestrictions[i]
                        .Trim()
                        .ToLower();

                if (
                    restriction == "int" ||
                    restriction == "bin"
                )
                {
                    double difference =
                        Math.Abs(
                            solution[i] -
                            Math.Round(solution[i])
                        );

                    if (difference > Tolerance)
                        return false;

                    if (restriction == "bin")
                    {
                        if (
                            solution[i] < -Tolerance ||
                            solution[i] > 1 + Tolerance
                        )
                            return false;
                    }
                }
            }

            return true;
        }

        // ============================================================
        // GET BRANCHING VARIABLE
        // ============================================================

        private int GetBranchingVariable(
            double[] solution)
        {
            int bestIndex = -1;
            double largestFraction = 0;

            for (int i = 0; i < solution.Length; i++)
            {
                string restriction =
                    originalReader
                        .SignRestrictions[i]
                        .Trim()
                        .ToLower();

                if (
                    restriction != "int" &&
                    restriction != "bin"
                )
                    continue;

                double fractional =
                    Math.Abs(
                        solution[i] -
                        Math.Round(solution[i])
                    );

                /*
                 * We only branch when the variable is actually
                 * fractional.
                 */

                if (
                    fractional > Tolerance &&
                    fractional > largestFraction
                )
                {
                    largestFraction = fractional;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        // ============================================================
        // BOUND CHECK
        // ============================================================

        private bool HasWorseBound(
            double objectiveValue)
        {
            if (!solutionFound)
                return false;

            if (isMaximization)
            {
                return objectiveValue <=
                       bestObjective + Tolerance;
            }

            return objectiveValue >=
                   bestObjective - Tolerance;
        }

        // ============================================================
        // COMPARE OBJECTIVES
        // ============================================================

        private bool IsBetter(
            double value,
            double currentBest)
        {
            if (isMaximization)
            {
                return value >
                       currentBest + Tolerance;
            }

            return value <
                   currentBest - Tolerance;
        }

        // ============================================================
        // PARSE SOLUTION
        // ============================================================

        private double[] ParseSolution(
            string logText)
        {
            double[] values =
                new double[numVars];

            if (string.IsNullOrEmpty(logText))
                return values;

            string[] lines =
                logText.Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries
                );

            foreach (string line in lines)
            {
                for (int i = 0; i < numVars; i++)
                {
                    string search =
                        "x" + (i + 1) + " =";

                    if (
                        line.IndexOf(
                            search,
                            StringComparison.OrdinalIgnoreCase
                        ) >= 0
                    )
                    {
                        string[] parts =
                            line.Split('=');

                        if (parts.Length >= 2)
                        {
                            string valueText =
                                parts[1].Trim();

                            double value;

                            if (
                                double.TryParse(
                                    valueText,
                                    NumberStyles.Float,
                                    CultureInfo.InvariantCulture,
                                    out value
                                )
                            )
                            {
                                values[i] = value;
                            }
                        }
                    }
                }
            }

            return values;
        }

        // ============================================================
        // PARSE OBJECTIVE
        // ============================================================

        private double ParseObjective(
            string logText)
        {
            if (string.IsNullOrEmpty(logText))
                return double.MinValue;

            string[] lines =
                logText.Split(
                    new[] { '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries
                );

            foreach (string line in lines)
            {
                if (
                    line.IndexOf(
                        "Optimal Z =",
                        StringComparison.OrdinalIgnoreCase
                    ) >= 0
                )
                {
                    string[] parts =
                        line.Split('=');

                    if (parts.Length >= 2)
                    {
                        double value;

                        string valueText =
                            parts[1].Trim();

                        if (
                            double.TryParse(
                                valueText,
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out value
                            )
                        )
                        {
                            return value;
                        }
                    }
                }
            }

            return double.MinValue;
        }

        // ============================================================
        // LOGGING
        // ============================================================

        private void LogMessage(
            string message,
            int indent = 0)
        {
            string prefix =
                new string(
                    ' ',
                    indent * 2
                );

            string line =
                prefix + message;

            Console.WriteLine(line);
            log.AppendLine(line);
        }

        // ============================================================
        // DISPLAY SOLUTION
        // ============================================================

        private void DisplayBestSolution()
        {
            LogMessage(
                "========================================"
            );

            LogMessage(
                " BEST INTEGER SOLUTION"
            );

            LogMessage(
                "========================================"
            );

            for (int i = 0; i < bestSolution.Length; i++)
            {
                LogMessage(
                    "x" +
                    (i + 1) +
                    " = " +
                    bestSolution[i].ToString("F3")
                );
            }

            LogMessage("");

            LogMessage(
                "Optimal Z = " +
                bestObjective.ToString("F3")
            );

            LogMessage(
                "========================================"
            );
        }

        // ============================================================
        // INNER CLASSES
        // ============================================================

        private class BnbNode
        {
            public int Id { get; set; }

            public double[] Solution { get; set; }

            public double ObjectiveValue { get; set; }

            public List<BranchConstraint> Branches { get; set; }

            public NodeStatus Status { get; set; }

            public int Depth { get; set; }

            public int? ParentId { get; set; }

            public bool IsFeasible { get; set; }
        }

        private enum NodeStatus
        {
            Active,
            Fathomed,
            Integer,
            Infeasible
        }

        private class BranchConstraint
        {
            public int VariableIndex { get; set; }

            public string Operator { get; set; }

            public double Value { get; set; }

            public override string ToString()
            {
                return
                    "x" +
                    (VariableIndex + 1) +
                    " " +
                    Operator +
                    " " +
                    Value.ToString("F0");
            }
        }

        private class LPSolutionResult
        {
            public double[] Solution { get; set; }

            public double ObjectiveValue { get; set; }

            public bool IsFeasible { get; set; }
        }
    }
}