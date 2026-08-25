using System;

namespace LPR381.Events
{
    // ===== EVENT ARGS =====

    public class AlgorithmEventArgs : EventArgs
    {
        public string AlgorithmName { get; set; }
        public string Message { get; set; }
        public int Progress { get; set; }
        public int Total { get; set; }
        public DateTime Timestamp { get; set; }

        public AlgorithmEventArgs(string algorithmName, string message)
        {
            AlgorithmName = algorithmName;
            Message = message;
            Timestamp = DateTime.Now;
            Progress = 0;
            Total = 100;
        }

        public AlgorithmEventArgs(string algorithmName, string message, int progress, int total)
        {
            AlgorithmName = algorithmName;
            Message = message;
            Progress = progress;
            Total = total;
            Timestamp = DateTime.Now;
        }
    }

    public class SolutionFoundEventArgs : EventArgs
    {
        public string AlgorithmName { get; set; }
        public double ObjectiveValue { get; set; }
        public double[] Solution { get; set; }
        public int Iterations { get; set; }

        public SolutionFoundEventArgs(string algorithmName, double objectiveValue, double[] solution, int iterations = 0)
        {
            AlgorithmName = algorithmName;
            ObjectiveValue = objectiveValue;
            Solution = solution;
            Iterations = iterations;
        }
    }

    public class ErrorEventArgs : EventArgs
    {
        public string AlgorithmName { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
        public bool IsFatal { get; set; }

        public ErrorEventArgs(string algorithmName, string errorMessage, Exception exception = null, bool isFatal = false)
        {
            AlgorithmName = algorithmName;
            ErrorMessage = errorMessage;
            Exception = exception;
            IsFatal = isFatal;
        }
    }

    // ===== EVENT HANDLERS =====

    public delegate void AlgorithmEventHandler(object sender, AlgorithmEventArgs e);
    public delegate void SolutionFoundEventHandler(object sender, SolutionFoundEventArgs e);
    public delegate void ErrorEventHandler(object sender, ErrorEventArgs e);

    // ===== EVENT MANAGER =====

    public static class EventManager
    {
        public static event AlgorithmEventHandler OnAlgorithmStarted;
        public static event AlgorithmEventHandler OnAlgorithmProgress;
        public static event AlgorithmEventHandler OnAlgorithmCompleted;
        public static event SolutionFoundEventHandler OnSolutionFound;
        public static event ErrorEventHandler OnError;

        public static void RaiseAlgorithmStarted(string algorithmName)
        {
            OnAlgorithmStarted?.Invoke(null, new AlgorithmEventArgs(algorithmName, "Started"));
        }

        public static void RaiseAlgorithmProgress(string algorithmName, string message, int progress, int total)
        {
            OnAlgorithmProgress?.Invoke(null, new AlgorithmEventArgs(algorithmName, message, progress, total));
        }

        public static void RaiseAlgorithmCompleted(string algorithmName, string message)
        {
            OnAlgorithmCompleted?.Invoke(null, new AlgorithmEventArgs(algorithmName, message));
        }

        public static void RaiseSolutionFound(string algorithmName, double objectiveValue, double[] solution, int iterations = 0)
        {
            OnSolutionFound?.Invoke(null, new SolutionFoundEventArgs(algorithmName, objectiveValue, solution, iterations));
        }

        public static void RaiseError(string algorithmName, string errorMessage, Exception exception = null, bool isFatal = false)
        {
            OnError?.Invoke(null, new ErrorEventArgs(algorithmName, errorMessage, exception, isFatal));
        }
    }
}