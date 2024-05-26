using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

public class IpAddressAttribute : ValidationAttribute
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
