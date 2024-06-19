using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace Disasmo.Utils
{
	internal class ValidationRuleMinMaxInt : ValidationRuleStringAsInt
	{		
		public int Min { get; set; }
		public int Max { get; set; }

		public override ValidationResult Validate(object value, CultureInfo cultureInfo)
		{
			ValidationResult result = ValidateInternal(value, cultureInfo, out int parsedValue);

			if (result.IsValid) {
				if (parsedValue < Min)
					result = new ValidationResult(false, $"Please enter a value greater than {Min}!");
				else if (parsedValue > Max)
					result = new ValidationResult(false, $"Please enter a value less than {Max}!");
			}

			return result;
		}
	}
}
