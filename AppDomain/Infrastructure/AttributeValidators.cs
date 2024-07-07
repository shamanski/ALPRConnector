using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

public class AttributeValidators : ValidationAttribute
{
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            return new ValidationResult("IP address is required");
        }

        var ipPattern = @"^([0-9]{1,3}\.){3}[0-9]{1,3}$";
        if (Regex.IsMatch(value.ToString(), ipPattern))
        {
            return ValidationResult.Success;
        }
        return new ValidationResult("Invalid IP address format");
    }
}

public class NumericRangeAttribute : ValidationAttribute
{
    private readonly double _minimum;
    private readonly double _maximum;

    public NumericRangeAttribute(double minimum, double maximum)
    {
        _minimum = minimum;
        _maximum = maximum;
    }

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return ValidationResult.Success; 
        }

        string strValue = value.ToString();

        if (double.TryParse(strValue, out double number))
        {
            if (number < _minimum || number > _maximum)
            {
                return new ValidationResult($"The value must be between {_minimum} and {_maximum}");
            }
            return ValidationResult.Success;
        }
        else
        {
            return new ValidationResult("The value is not a valid number");
        }
    }
}
