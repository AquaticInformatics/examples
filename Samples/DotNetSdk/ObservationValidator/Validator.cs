using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Aquarius.Samples.Client.ServiceModel;
using log4net;

namespace ObservationValidator
{
    public class Validator
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly List<Rule> _rules;
        private readonly IDictionary<string, List<Rule>> _ruleMap = new Dictionary<string, List<Rule>>();

        public Validator(List<Rule> rules)
        {
            _rules = rules;
            CreateRuleMap();
        }

        private void CreateRuleMap()
        {
            foreach (var rule in _rules)
            {
                var key = rule.LeftParam;

                if(_ruleMap.ContainsKey(key))
                {
                    _ruleMap[key].Add(rule);
                    continue;
                }

                _ruleMap[key] = new List<Rule>{ rule }; 
            }
        }

        public List<Observation> GetInvalidObservations(List<Observation> observations)
        {
            var invalidObservations = new List<Observation>();

            if (observations == null)
            {
                return invalidObservations;
            }

            //We get observations by specimen name.
            //Specimens can have a same name, so need to regroup:
            var observationGroups = observations.Where(obs => obs.Specimen != null)
            .GroupBy(obs => obs.Specimen.Id).ToList();

            Log.Debug($"Validating {observations.Count} observations in {observationGroups.Count} specimens.");

            foreach (var observationGroup in observationGroups)
            {
                foreach (var observation in observationGroup)
                {
                    var parameter = observation.ObservedProperty.CustomId;
                    if (!_ruleMap.ContainsKey(parameter))
                        continue;

                    var applicableRules = _ruleMap[parameter];
                    Log.Debug($"Got {applicableRules.Count} applicable rules for parameter {parameter}.");

                    var invalidRightSideObservations =
                        GetInvalidRightSideParameterObservations(observation, observationGroup.ToList(), applicableRules);

                    if (!invalidRightSideObservations.Any())
                    {
                        Log.Debug($"No invalid observations found for parameter {parameter} " +
                                  $"in specimen with id {observationGroup.Key}.");
                        continue;
                    }

                    invalidObservations.Add(observation);
                    invalidObservations.AddRange(invalidRightSideObservations);
                }
            }

            return invalidObservations.Distinct().ToList();
        }

        private List<Observation> GetInvalidRightSideParameterObservations(Observation leftSideObservation, 
            List<Observation> observations, List<Rule> rules)
        {
            var invalidObservations = new List<Observation>();

            foreach (var rule in rules)
            {
                var rightSideParamObservations = observations.Where(obs => rule.RightParam == obs.ObservedProperty.CustomId).ToList(); 

                var invalidObservationsThisRule =
                    rightSideParamObservations.Where(
                        rightObs => !IsObservationValid(leftSideObservation, rule, rightObs));

                invalidObservations.AddRange(invalidObservationsThisRule);
            }

            return invalidObservations;
        }

        private bool IsObservationValid(Observation left, Rule rule, Observation right)
        {
            if (!HasNumericValue(left) ||
                !HasNumericValue(right))
            {
                return true;
            }

            //Both have values. If different units, should be valid:
            if (HasDifferentUnit(left, right))
                return true;

            //Both have values. Check the rule:
            var leftValue = GetNumericValue(left);
            var rightValue = GetNumericValue(right);

            switch (rule.ComparisonSymbol)
            {
                case ComparisonSymbol.GreaterThan:
                    return DoubleHelper.IsGreaterThan(leftValue, rightValue);
                case ComparisonSymbol.LessThan:
                    return DoubleHelper.IsLessThan(leftValue, rightValue);
                case ComparisonSymbol.GreaterOrEqual:
                    return DoubleHelper.IsGreaterOrEqual(leftValue, rightValue);
                case ComparisonSymbol.LessOrEqual:
                    return DoubleHelper.IsLessOrEqual(leftValue, rightValue);
                case ComparisonSymbol.Equal:
                    return DoubleHelper.AreEqual(leftValue, rightValue);
            }

            return false;
        }

        private double GetNumericValue(Observation observation)
        {
            if (IsNonDetected(observation))
                return 0d;

            return observation.NumericResult.Quantity.Value;
        }

        private static bool IsNonDetected(Observation observation)
        {
            return observation.NumericResult.DetectionCondition == DetectionConditionType.NOT_DETECTED;
        }

        private bool HasDifferentUnit(Observation left, Observation right)
        {
            //Non detected treated as same unit:
            if(IsNonDetected(left) || IsNonDetected(right))
                return false;
            
            return left.NumericResult.Quantity.Unit.Id != right.NumericResult.Quantity.Unit.Id;
        }

        private bool HasNumericValue(Observation observation)
        {
            //non-detected is treated as 0:
            return IsNonDetected(observation) ||
                observation.NumericResult?.Quantity != null;
        }
    }
}
