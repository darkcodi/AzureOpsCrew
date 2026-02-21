using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Api.Email;
using AzureOpsCrew.Api.Endpoints.Dtos.Auth;
using AzureOpsCrew.Api.Endpoints.Filters;
using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Domain.Users;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace AzureOpsCrew.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        var group = routeBuilder.MapGroup("/api/auth")
            .WithTags("Auth");

        group.MapPost("/register", async (
            RegisterRequestDto body,
            AzureOpsCrewContext context,
            IPasswordHasher<PendingRegistration> pendingRegistrationHasher,
            IRegistrationEmailSender registrationEmailSender,
            IOptions<EmailVerificationSettings> emailVerificationOptions,
            CancellationToken cancellationToken) =>
        {
            var settings = emailVerificationOptions.Value;
            var now = DateTime.UtcNow;
            var normalizedEmail = NormalizeEmail(body.Email);
            var email = body.Email.Trim();

            var exists = await context.Users
                .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

            if (exists)
                return Results.Conflict(new { error = "Email is already registered." });

            var pendingRegistration = await context.PendingRegistrations
                .SingleOrDefaultAsync(p => p.NormalizedEmail == normalizedEmail, cancellationToken);

            if (pendingRegistration is not null)
            {
                var remainingCooldown = GetRemainingCooldownSeconds(
                    now,
                    pendingRegistration.VerificationCodeSentAt,
                    settings.ResendCooldownSeconds);

                if (remainingCooldown > 0)
                {
                    return Results.Json(
                        new
                        {
                            error = $"Please wait {remainingCooldown} seconds before requesting another code.",
                            retryAfterSeconds = remainingCooldown
                        },
                        statusCode: StatusCodes.Status429TooManyRequests);
                }
            }

            if (pendingRegistration is null)
            {
                pendingRegistration = new PendingRegistration(email, normalizedEmail);
                context.PendingRegistrations.Add(pendingRegistration);
            }

            var displayName = string.IsNullOrWhiteSpace(body.DisplayName)
                ? email
                : body.DisplayName.Trim();

            var verificationCode = GenerateVerificationCode(settings.CodeLength);
            var passwordHash = pendingRegistrationHasher.HashPassword(pendingRegistration, body.Password);
            var verificationCodeHash = pendingRegistrationHasher.HashPassword(pendingRegistration, verificationCode);
            var expiresAtUtc = now.AddMinutes(settings.CodeTtlMinutes);

            pendingRegistration.Refresh(
                email,
                displayName,
                passwordHash,
                verificationCodeHash,
                expiresAtUtc,
                now);

            await context.SaveChangesAsync(cancellationToken);

            try
            {
                await registrationEmailSender.SendRegistrationCodeAsync(
                    email,
                    verificationCode,
                    expiresAtUtc,
                    cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return Results.Json(
                    new { error = "Unable to send verification email. Please try again." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(
                new RegisterChallengeDto(
                    "Verification code sent. Check your email to continue.",
                    expiresAtUtc,
                    settings.ResendCooldownSeconds));
        })
        .AddEndpointFilter<ValidationFilter<RegisterRequestDto>>()
        .Produces<RegisterChallengeDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status429TooManyRequests)
        .Produces(StatusCodes.Status503ServiceUnavailable)
        .Produces(StatusCodes.Status409Conflict)
        .AllowAnonymous();

        group.MapPost("/register/resend", async (
            ResendRegistrationCodeRequestDto body,
            AzureOpsCrewContext context,
            IPasswordHasher<PendingRegistration> pendingRegistrationHasher,
            IRegistrationEmailSender registrationEmailSender,
            IOptions<EmailVerificationSettings> emailVerificationOptions,
            CancellationToken cancellationToken) =>
        {
            var settings = emailVerificationOptions.Value;
            var now = DateTime.UtcNow;
            var normalizedEmail = NormalizeEmail(body.Email);

            var pendingRegistration = await context.PendingRegistrations
                .SingleOrDefaultAsync(p => p.NormalizedEmail == normalizedEmail, cancellationToken);

            if (pendingRegistration is null)
                return Results.BadRequest(new { error = "Registration request not found. Start sign up again." });

            var remainingCooldown = GetRemainingCooldownSeconds(
                now,
                pendingRegistration.VerificationCodeSentAt,
                settings.ResendCooldownSeconds);

            if (remainingCooldown > 0)
            {
                return Results.Json(
                    new
                    {
                        error = $"Please wait {remainingCooldown} seconds before requesting another code.",
                        retryAfterSeconds = remainingCooldown
                    },
                    statusCode: StatusCodes.Status429TooManyRequests);
            }

            var verificationCode = GenerateVerificationCode(settings.CodeLength);
            var verificationCodeHash = pendingRegistrationHasher.HashPassword(pendingRegistration, verificationCode);
            var expiresAtUtc = now.AddMinutes(settings.CodeTtlMinutes);

            pendingRegistration.Refresh(
                pendingRegistration.Email,
                pendingRegistration.DisplayName,
                pendingRegistration.PasswordHash,
                verificationCodeHash,
                expiresAtUtc,
                now);

            await context.SaveChangesAsync(cancellationToken);

            try
            {
                await registrationEmailSender.SendRegistrationCodeAsync(
                    pendingRegistration.Email,
                    verificationCode,
                    expiresAtUtc,
                    cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return Results.Json(
                    new { error = "Unable to send verification email. Please try again." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(
                new RegisterChallengeDto(
                    "A new verification code has been sent.",
                    expiresAtUtc,
                    settings.ResendCooldownSeconds));
        })
        .AddEndpointFilter<ValidationFilter<ResendRegistrationCodeRequestDto>>()
        .Produces<RegisterChallengeDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status429TooManyRequests)
        .Produces(StatusCodes.Status503ServiceUnavailable)
        .AllowAnonymous();

        group.MapPost("/register/verify", async (
            VerifyRegistrationCodeRequestDto body,
            AzureOpsCrewContext context,
            IPasswordHasher<PendingRegistration> pendingRegistrationHasher,
            JwtTokenService jwtTokenService,
            IOptions<EmailVerificationSettings> emailVerificationOptions,
            CancellationToken cancellationToken) =>
        {
            var settings = emailVerificationOptions.Value;
            var now = DateTime.UtcNow;
            var normalizedEmail = NormalizeEmail(body.Email);

            var pendingRegistration = await context.PendingRegistrations
                .SingleOrDefaultAsync(p => p.NormalizedEmail == normalizedEmail, cancellationToken);

            if (pendingRegistration is null)
                return Results.BadRequest(new { error = "Registration request not found. Start sign up again." });

            if (pendingRegistration.VerificationCodeExpiresAt < now)
            {
                context.PendingRegistrations.Remove(pendingRegistration);
                await context.SaveChangesAsync(cancellationToken);
                return Results.BadRequest(new { error = "Verification code expired. Request a new one." });
            }

            if (pendingRegistration.VerificationAttempts >= settings.MaxVerificationAttempts)
            {
                context.PendingRegistrations.Remove(pendingRegistration);
                await context.SaveChangesAsync(cancellationToken);
                return Results.BadRequest(new { error = "Too many invalid attempts. Start sign up again." });
            }

            var code = body.Code.Trim();
            var verificationResult = pendingRegistrationHasher.VerifyHashedPassword(
                pendingRegistration,
                pendingRegistration.VerificationCodeHash,
                code);

            if (verificationResult == PasswordVerificationResult.Failed)
            {
                pendingRegistration.IncrementFailedAttempt();
                var attemptsLeft = settings.MaxVerificationAttempts - pendingRegistration.VerificationAttempts;

                if (attemptsLeft <= 0)
                {
                    context.PendingRegistrations.Remove(pendingRegistration);
                    await context.SaveChangesAsync(cancellationToken);
                    return Results.BadRequest(new { error = "Too many invalid attempts. Start sign up again." });
                }

                await context.SaveChangesAsync(cancellationToken);
                return Results.BadRequest(new { error = $"Invalid verification code. {attemptsLeft} attempt(s) left." });
            }

            var exists = await context.Users
                .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

            if (exists)
            {
                context.PendingRegistrations.Remove(pendingRegistration);
                await context.SaveChangesAsync(cancellationToken);
                return Results.Conflict(new { error = "Email is already registered." });
            }

            var user = new User(
                email: pendingRegistration.Email,
                normalizedEmail: pendingRegistration.NormalizedEmail,
                passwordHash: pendingRegistration.PasswordHash,
                displayName: pendingRegistration.DisplayName);
            user.MarkLogin();

            context.Users.Add(user);
            context.PendingRegistrations.Remove(pendingRegistration);
            await context.SaveChangesAsync(cancellationToken);

            var token = jwtTokenService.CreateToken(user);
            return Results.Ok(ToAuthResponse(user, token));
        })
        .AddEndpointFilter<ValidationFilter<VerifyRegistrationCodeRequestDto>>()
        .Produces<AuthResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict)
        .AllowAnonymous();

        group.MapPost("/login", async (
            LoginRequestDto body,
            AzureOpsCrewContext context,
            IPasswordHasher<User> passwordHasher,
            JwtTokenService jwtTokenService,
            CancellationToken cancellationToken) =>
        {
            var normalizedEmail = NormalizeEmail(body.Email);

            var user = await context.Users
                .SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

            if (user is null || !user.IsActive)
                return Results.Unauthorized();

            var passwordResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, body.Password);
            if (passwordResult == PasswordVerificationResult.Failed)
                return Results.Unauthorized();

            if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                var rehash = passwordHasher.HashPassword(user, body.Password);
                user.UpdatePasswordHash(rehash);
            }

            user.MarkLogin();
            await context.SaveChangesAsync(cancellationToken);

            var token = jwtTokenService.CreateToken(user);
            return Results.Ok(ToAuthResponse(user, token));
        })
        .AddEndpointFilter<ValidationFilter<LoginRequestDto>>()
        .Produces<AuthResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .AllowAnonymous();

        group.MapGet("/me", async (
            HttpContext httpContext,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();

            var user = await context.Users
                .SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);

            if (user is null)
                return Results.Unauthorized();

            var now = DateTime.UtcNow;
            if (!user.LastLoginAt.HasValue || now - user.LastLoginAt.Value >= TimeSpan.FromMinutes(1))
            {
                user.MarkLogin();
                await context.SaveChangesAsync(cancellationToken);
            }

            return Results.Ok(new AuthUserDto(user.Id, user.Email, user.DisplayName));
        })
        .Produces<AuthUserDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static int GetRemainingCooldownSeconds(
        DateTime nowUtc,
        DateTime lastSentAtUtc,
        int resendCooldownSeconds)
    {
        var secondsSinceLastSend = (int)(nowUtc - lastSentAtUtc).TotalSeconds;
        return Math.Max(0, resendCooldownSeconds - secondsSinceLastSend);
    }

    private static string GenerateVerificationCode(int codeLength)
    {
        var maxExclusive = (int)Math.Pow(10, codeLength);
        var value = RandomNumberGenerator.GetInt32(0, maxExclusive);
        return value.ToString($"D{codeLength}");
    }

    private static AuthResponseDto ToAuthResponse(User user, AuthTokenResult token)
    {
        return new AuthResponseDto(
            token.AccessToken,
            token.ExpiresAtUtc,
            new AuthUserDto(user.Id, user.Email, user.DisplayName));
    }
}
