using System.Text;
using FluentValidation;
using ParsingEndpoint.Models;

namespace ParsingEndpoint.Validation;

public sealed class ParsingRequestValidator : AbstractValidator<ParsingRequest>
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public ParsingRequestValidator()
    {
        RuleFor(x => x.Selector).Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode("MISSING_PARAMETER")
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithErrorCode("EMPTY_SELECTOR");
        RuleFor(x => x.Attribute).Cascade(CascadeMode.Stop)
            .NotNull().WithErrorCode("MISSING_PARAMETER")
            .Must(value => !string.IsNullOrWhiteSpace(value)).WithErrorCode("EMPTY_ATTRIBUTE");

        RuleFor(x => x.UrlB64).Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("MISSING_PARAMETER")
            .Must(IsUtf8Base64).WithErrorCode("INVALID_URL_BASE64");
        RuleFor(x => x.PageB64).Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("MISSING_PARAMETER")
            .Must(IsUtf8Base64).WithErrorCode("INVALID_PAGE_BASE64");
        RuleFor(x => x.KeyBytesB64).Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("MISSING_PARAMETER")
            .Must(value => IsBase64(value) && Convert.FromBase64String(value!).Length == 32)
            .WithErrorCode("INVALID_KEY");
        RuleFor(x => x.EncryptedTextBytesB64).Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrorCode("MISSING_PARAMETER")
            .Must(value => IsBase64(value) && Convert.FromBase64String(value!).Length is > 0 and var length && length % 16 == 0)
            .WithErrorCode("INVALID_CIPHERTEXT");
    }

    private static bool IsUtf8Base64(string? value)
    {
        if (!IsBase64(value)) return false;
        try { _ = StrictUtf8.GetString(Convert.FromBase64String(value!)); return true; }
        catch (DecoderFallbackException) { return false; }
    }

    private static bool IsBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { _ = Convert.FromBase64String(value); return true; }
        catch (FormatException) { return false; }
    }
}

