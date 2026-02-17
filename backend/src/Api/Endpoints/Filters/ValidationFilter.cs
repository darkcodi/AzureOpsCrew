using System.ComponentModel.DataAnnotations;

namespace AzureOpsCrew.Api.Endpoints.Filters;

public class ValidationFilter<T> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<T>().FirstOrDefault();

        if (request is null)
        {
            return Results.BadRequest(new { Error = "Request body is required." });
        }

        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();

        if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
        {
            var errors = validationResults
                .SelectMany(r => r.MemberNames.Select(m => new { Property = m, Message = r.ErrorMessage }))
                .ToDictionary(e => e.Property, e => e.Message ?? string.Empty);

            return Results.BadRequest(new { Errors = errors });
        }

        return await next(context);
    }
}
