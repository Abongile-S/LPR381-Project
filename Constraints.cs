using System;
using System.Collections.Generic;

namespace LPR381
{
    public class Constraint
    {
        public List<double> Coefficients { get; set; }
        public string Relation { get; set; }
        public double RHS { get; set; }

        public Constraint(List<double> coefficients, string relation, double rhs)
        {
            Coefficients = coefficients;
            Relation = relation;
            RHS = rhs;
        }
    }
}