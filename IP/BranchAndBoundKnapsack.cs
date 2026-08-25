using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LPR381.Events;
using LPR381.Utils;

namespace LPR381.IP
{
    public class BranchAndBoundKnapsack
    {
        // ===== Configuration =====
        private double tolerance = 1e-6;

        // ===== Private Fields =====
        private int numItems;
        private double[] profits;
        private double[] weights;
        private double capacity;
        private int[] originalIndices;

        private double bestValue;
        private int[] bestSelection;
        private int[] currentSelection;
        private bool solutionFound;
        private int nodeCounter;

        // ===== Logging =====
        private readonly StringBuilder log = new StringBuilder();
        public string Log => log.ToString();

        // ===== Constructor =====
        public BranchAndBoundKnapsack()
        {
            bestValue = 0;
            bestSelection = Array.Empty<int>();
            currentSelection = Array.Empty<int>();
            solutionFound = false;
            nodeCounter = 0;
        }

        // ===== Main Solver =====
        public void Solve(InputFileReader reader)
        {
            try
            {
                EventManager.RaiseAlgorithmStarted("Branch and Bound Knapsack");

                this.numItems = reader.ObjCoefficients.Count;
                this.profits = reader.ObjCoefficients.ToArray();
                this.weights = reader.Constraints[0].Coefficients.ToArray();
                this.capacity = reader.Constraints[0].RHS;
                this.originalIndices = Enumerable.Range(0, numItems).ToArray();

                bestSelection = new int[numItems];
                currentSelection = new int[numItems];

                LogMessage("========================================");
                LogMessage(" BRANCH AND BOUND KNAPSACK ALGORITHM");
                LogMessage("========================================");
                LogMessage($"Items: {numItems}");
                LogMessage($"Capacity: {capacity:F0}");
                LogMessage($"Profits: [{string.Join(", ", profits)}]");
                LogMessage($"Weights: [{string.Join(", ", weights)}]");
                LogMessage("");

                SortItems();

                LogMessage("--- SORTED BY PROFIT/WEIGHT RATIO ---");
                for (int i = 0; i < numItems; i++)
                {
                    double ratio = profits[i] / weights[i];
                    LogMessage($"Item {originalIndices[i] + 1}: P={profits[i]:F0}, W={weights[i]:F0}, Ratio={ratio:F3}");
                }
                LogMessage("");

                LogMessage("--- STARTING BRANCH AND BOUND (DFS) ---");
                LogMessage("");
                BranchAndBound(0, 0, 0);

                LogMessage("");
                LogMessage("--- BEST CANDIDATE ---");
                if (solutionFound)
                {
                    DisplayBestSolution();
                    EventManager.RaiseAlgorithmCompleted("Branch and Bound Knapsack", $"Solution found: Profit = {bestValue:F0}");
                }
                else
                {
                    LogMessage("[ERROR] No feasible solution found.");
                    EventManager.RaiseError("Branch and Bound Knapsack", "No feasible solution found");
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.HandleException(ex, "Branch and Bound Knapsack");
                EventManager.RaiseError("Branch and Bound Knapsack", ex.Message);
                LogMessage($"[ERROR] {ex.Message}");
            }
        }

        private void BranchAndBound(int level, double currentProfit, double currentWeight)
        {
            nodeCounter++;

            LogNodeState(level, currentProfit, currentWeight);

            if (level == numItems)
            {
                if (currentProfit > bestValue + tolerance)
                {
                    bestValue = currentProfit;
                    Array.Copy(currentSelection, bestSelection, numItems);
                    solutionFound = true;
                    LogMessage($"[SUCCESS] NEW BEST SOLUTION: Profit = {bestValue:F0}", 1);
                    EventManager.RaiseSolutionFound("Branch and Bound Knapsack", bestValue,
                        bestSelection.Select(x => (double)x).ToArray());
                }
                return;
            }

            double upperBound = ComputeUpperBound(level, currentProfit, currentWeight);

            if (upperBound <= bestValue + tolerance)
            {
                LogMessage($"Node {nodeCounter} FATHOMED (UB={upperBound:F3} <= Best={bestValue:F3})", 1);
                return;
            }

            if (currentWeight + weights[level] <= capacity + tolerance)
            {
                currentSelection[level] = 1;
                BranchAndBound(level + 1, currentProfit + profits[level], currentWeight + weights[level]);
                currentSelection[level] = 0;
            }
            else
            {
                LogMessage($"Item {originalIndices[level] + 1} cannot be included (weight would exceed capacity)", 1);
            }

            currentSelection[level] = 0;
            BranchAndBound(level + 1, currentProfit, currentWeight);
        }

        private double ComputeUpperBound(int level, double currentProfit, double currentWeight)
        {
            double remainingCapacity = capacity - currentWeight;
            double bound = currentProfit;

            for (int i = level; i < numItems && remainingCapacity > tolerance; i++)
            {
                if (weights[i] <= remainingCapacity + tolerance)
                {
                    bound += profits[i];
                    remainingCapacity -= weights[i];
                }
                else
                {
                    bound += (profits[i] / weights[i]) * remainingCapacity;
                    remainingCapacity = 0;
                }
            }

            return bound;
        }

        private void SortItems()
        {
            var indices = Enumerable.Range(0, numItems).ToList();
            indices.Sort((a, b) =>
            {
                double ratioA = profits[a] / weights[a];
                double ratioB = profits[b] / weights[b];
                return ratioB.CompareTo(ratioA);
            });

            var sortedProfits = new double[numItems];
            var sortedWeights = new double[numItems];
            var sortedIndices = new int[numItems];

            for (int i = 0; i < numItems; i++)
            {
                sortedProfits[i] = profits[indices[i]];
                sortedWeights[i] = weights[indices[i]];
                sortedIndices[i] = originalIndices[indices[i]];
            }

            profits = sortedProfits;
            weights = sortedWeights;
            originalIndices = sortedIndices;
        }

        private void LogMessage(string message, int indent = 0)
        {
            string prefix = new string(' ', indent * 2);
            string line = $"{prefix}{message}";
            Console.WriteLine(line);
            log.AppendLine(line);
        }

        private void LogNodeState(int level, double currentProfit, double currentWeight)
        {
            LogMessage($"--- Node {nodeCounter} (Level {level}) ---");
            LogMessage($"Current Profit: {currentProfit:F3}");
            LogMessage($"Current Weight: {currentWeight:F3}");
            LogMessage($"Remaining Capacity: {capacity - currentWeight:F3}");

            if (level < numItems)
            {
                LogMessage($"Next Item: {originalIndices[level] + 1} (P={profits[level]:F0}, W={weights[level]:F0})");
            }

            var selected = new List<string>();
            for (int i = 0; i < level; i++)
            {
                if (currentSelection[i] == 1)
                {
                    selected.Add($"x{originalIndices[i] + 1}");
                }
            }
            LogMessage($"Selected Items: {(selected.Any() ? string.Join(", ", selected) : "None")}");
            LogMessage("");
        }

        private void DisplayBestSolution()
        {
            LogMessage("========================================");
            LogMessage(" BEST SOLUTION");
            LogMessage("========================================");

            var originalSelection = new int[numItems];
            for (int i = 0; i < numItems; i++)
            {
                originalSelection[originalIndices[i]] = bestSelection[i];
            }

            double totalWeight = 0;
            double totalProfit = 0;

            LogMessage("Selected Items:");
            for (int i = 0; i < numItems; i++)
            {
                if (originalSelection[i] == 1)
                {
                    int idx = Array.IndexOf(originalIndices, i);
                    LogMessage($"  x{i + 1} = 1  (Profit: {profits[idx]:F0}, Weight: {weights[idx]:F0})");
                    totalWeight += weights[idx];
                    totalProfit += profits[idx];
                }
                else
                {
                    LogMessage($"  x{i + 1} = 0");
                }
            }

            LogMessage("");
            LogMessage($"Total Weight: {totalWeight:F3}");
            LogMessage($"Total Profit: {totalProfit:F3}");
            LogMessage($"Nodes Explored: {nodeCounter}");
            LogMessage("========================================");
        }
    }
}