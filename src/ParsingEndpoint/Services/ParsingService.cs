using AngleSharp.Html.Parser;
using FluentValidation;
using ParsingEndpoint.Database;
using ParsingEndpoint.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ParsingEndpoint.Services;

public interface IParsingService
{
    Task<ParsingResponse> ProcessAsync(ParsingRequest request, CancellationToken cancellationToken);
}

public sealed class ParsingService(
    IEnumerable<IValidator<ParsingRequest>> _validators,
    IDapperSession _session,
    IElementRepository _repository) : IParsingService
{
    public static readonly Regex EmailRegex = new Regex(@"((([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])+(\.([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])+)*)|((\x22)((((\x20|\x09)*(\x0d\x0a))?(\x20|\x09)+)?(([\x01-\x08\x0b\x0c\x0e-\x1f\x7f]|\x21|[\x23-\x5b]|[\x5d-\x7e]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(\\([\x01-\x09\x0b\x0c\x0d-\x7f]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF]))))*(((\x20|\x09)*(\x0d\x0a))?(\x20|\x09)+)?(\x22)))@((([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])*([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))\.)+(([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])*([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))\.?", // 
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async Task<ParsingResponse> ProcessAsync(ParsingRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var validationError = await ValidateRequestAsync(request, cancellationToken);
            if (validationError is { } error)
            {
                return ParsingResponse.Failure(error.Code, error.Message);
            }

            var url = StrictUtf8.GetString(Convert.FromBase64String(request.UrlB64!));
            var page = StrictUtf8.GetString(Convert.FromBase64String(request.PageB64!));

            var key = Convert.FromBase64String(request.KeyBytesB64!);
            var ciphertext = Convert.FromBase64String(request.EncryptedTextBytesB64!);

            var emailTask = Task.Run(() => FindEmails(page), cancellationToken);
            var decryptTask = Task.Run(() => Decrypt(ciphertext, key), cancellationToken);
            var backgroundTask = Task.WhenAll(emailTask, decryptTask);

            ObserveFaults(backgroundTask);

            IReadOnlyList<ElementRecord> elements;
            try
            {
                elements = await GetElementsBySelectorAsync(page, request, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return ParsingResponse.Failure("INVALID_SELECTOR", ex.Message);
            }

            try
            {
                await _session.BeginTransactionAsync(cancellationToken);
                await _repository.InsertAsync(elements, cancellationToken);
                await backgroundTask;
                await _session.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                await _session.RollbackTransactionAsync(CancellationToken.None);
                throw;
            }

            var emails = await emailTask;
            return new ParsingResponse
            {
                ElementsCount = elements.Count,
                ElementsAttrList = elements.Select(element => element.AttributeValue).ToList(),
                EmailsCount = emails.Count,
                EmailsList = emails,
                Url = url,
                DecryptedPlainText = await decryptTask
            };
        }
        catch (Exception ex) when (ex is CryptographicException or DecoderFallbackException)
        {
            return ParsingResponse.Failure("INVALID_CIPHERTEXT", ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ParsingResponse.Failure("INTERNAL_ERROR", ex.Message, 500);
        }
    }

    private async Task<(string Code, string Message)?> ValidateRequestAsync(ParsingRequest request, CancellationToken cancellationToken)
    {
        var results = await Task.WhenAll(_validators.Select(validator => validator.ValidateAsync(request, cancellationToken)));
        var errors = results.SelectMany(result => result.Errors).ToArray();

        return errors.Length switch
        {
            0 => null,
            1 => (errors[0].ErrorCode, errors[0].ErrorMessage),
            _ => ("MULTIPLE_VALIDATION_ERRORS",
                string.Join(Environment.NewLine, errors.Select(error => error.ErrorMessage)))
        };
    }

    private static List<string> FindEmails(string page) 
        => EmailRegex.Matches(page).Select(match => match.Value).ToList();

    private static string Decrypt(byte[] ciphertext, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        using var decryptor = aes.CreateDecryptor();
        var plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        return StrictUtf8.GetString(plaintext);
    }

    private static void ObserveFaults(Task task)
    {
        _ = task.ContinueWith(static faultedTask => _ = faultedTask.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static async Task<IReadOnlyList<ElementRecord>> GetElementsBySelectorAsync(string html, ParsingRequest request, CancellationToken cancellationToken)
    {
        var document = await new HtmlParser().ParseDocumentAsync(html, cancellationToken);

        return document.QuerySelectorAll(request.Selector!)
                       .Select(element => new ElementRecord(element.GetAttribute(request.Attribute!) ?? string.Empty, element.OuterHtml))
                       .ToArray();
    }
}


