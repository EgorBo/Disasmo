using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Disasmo.Utils
{
	internal class ValidationRuleStringAsInt : ValidationRule
	{
		public override ValidationResult Validate(object value, CultureInfo cultureInfo)
		{
			return ValidateInternal(value, cultureInfo, out _);
		}

		protected ValidationResult ValidateInternal(object value, CultureInfo cultureInfo, out int parsedValue)
		{
			parsedValue = 0;

			if (value is int)
				return ValidationResult.ValidResult;

			if (value is string valueAsString)
				return int.TryParse(valueAsString, NumberStyles.Integer, cultureInfo, out parsedValue) ? ValidationResult.ValidResult : new ValidationResult(false, "Please enter a valid number!");

			return new ValidationResult(false, "Please enter a valid number!");
		}
	}
}
