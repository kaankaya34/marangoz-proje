using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.Text.RegularExpressions;

namespace web.Models;

public static class PhoneHelper
{
    private static readonly Regex AllowedChars = new(@"^[0-9+()\s-]+$", RegexOptions.Compiled);

    // Kabul edilen örnekler: 05321234567, 0532 123 45 67, (532) 123 45 67, +90 532 123 45 67, 5321234567
    // Başarılıysa 05XXXXXXXXX biçiminde standart numara döner.
    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(input) || !AllowedChars.IsMatch(input)) return false;
        if (input.Count(c => c == '+') > 1 || (input.Contains('+') && !input.TrimStart().StartsWith('+'))) return false;

        var digits = new string(input.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("90") && digits.Length == 12) digits = digits[2..];
        else if (digits.StartsWith('0') && digits.Length == 11) digits = digits[1..];

        // Ulusal numara 10 hane olmalı ve alan/operatör kodu 2-5 ile başlamalı (cep: 5XX).
        if (digits.Length != 10 || digits[0] is < '2' or > '5') return false;

        normalized = "0" + digits;
        return true;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public class TurkishPhoneAttribute : ValidationAttribute, IClientModelValidator
{
    public TurkishPhoneAttribute()
        => ErrorMessage = "Geçerli bir telefon numarası girin (örn: 0532 123 45 67). Yalnızca rakam kullanın.";

    // Boş değer geçerlidir; zorunluluk [Required] ile belirlenir.
    public override bool IsValid(object? value)
        => value is null || (value is string s && (string.IsNullOrWhiteSpace(s) || PhoneHelper.TryNormalize(s, out _)));

    public void AddValidation(ClientModelValidationContext context)
    {
        Merge(context.Attributes, "data-val", "true");
        Merge(context.Attributes, "data-val-regex", ErrorMessage!);
        Merge(context.Attributes, "data-val-regex-pattern", @"^\s*$|^[0-9+()\s\-]{10,20}$");
    }

    private static void Merge(IDictionary<string, string> attrs, string key, string value)
        => attrs.TryAdd(key, value);
}

// En az bir harf ve bir rakam; uzunluk ayrıca StringLength ile denetlenir.
[AttributeUsage(AttributeTargets.Property)]
public class PasswordStrengthAttribute : ValidationAttribute
{
    public PasswordStrengthAttribute() => ErrorMessage = "Şifre en az bir harf ve bir rakam içermelidir.";

    public override bool IsValid(object? value)
        => value is not string s || s.Length == 0 || (s.Any(char.IsLetter) && s.Any(char.IsDigit));
}
