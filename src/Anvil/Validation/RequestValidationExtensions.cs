using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Localization;

namespace Anvil;

public static class RequestValidationExtensions
{
    public static IReadOnlyDictionary<string, string[]> ToFieldErrors(
        this IEnumerable<ValidationError> errors)
    {
        return errors
            .GroupBy(error => error.Field, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Message).Distinct().ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<ValidationError> ValidateLocalized<T>(
        this RequestContext context,
        T model,
        IStringLocalizer localizer)
    {
        return context.Validate(model)
            .Select(error => error with { Message = localizer[error.Message] })
            .ToArray();
    }

    public static IReadOnlyList<ValidationError> Validate<T>(
        this RequestContext requestContext,
        T model)
    {
        ArgumentNullException.ThrowIfNull(requestContext);
        ArgumentNullException.ThrowIfNull(model);

        var validationContext = new ValidationContext(
            model,
            requestContext.HttpContext.RequestServices,
            items: null);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, validationContext, results, validateAllProperties: true);

        return results
            .SelectMany(result =>
            {
                var members = result.MemberNames.DefaultIfEmpty(string.Empty);
                return members.Select(member => new ValidationError(
                    member,
                    result.ErrorMessage ?? "The value is invalid."));
            })
            .ToArray();
    }

    internal static Dictionary<string, string[]> ToProblemErrors(
        IEnumerable<ValidationError> errors)
    {
        return new Dictionary<string, string[]>(errors.ToFieldErrors(), StringComparer.OrdinalIgnoreCase);
    }
}
