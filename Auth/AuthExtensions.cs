using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Claims;
using Coflnet.Core;

namespace Coflnet.Auth;

public static class AuthExtensions
{
    public static IServiceCollection AddCoflAuthService(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<AuthService>();
        // from config
        var issuer = builder.Configuration["jwt:issuer"];
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["jwt:secret"] ?? throw new InvalidOperationException("jwt:secret is missing in the configuration.")));
        // override default claim mapping to not remab "sub" to "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
        JsonWebTokenHandler.DefaultInboundClaimTypeMap.Clear();
        builder.Services
            .AddAuthorization()
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = issuer,
                    IssuerSigningKey = key
                };

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = c =>
                    {
                        Console.WriteLine(c.Error);
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                        {
                            context.Response.Headers.Add("Token-Expired", "true");
                        }
                        else
                        {
                            Console.WriteLine(context.Exception);
                        }
                        return Task.CompletedTask;
                    }
                };
            });
        return builder.Services;
    }

    public static Guid UserId(this ClaimsPrincipal user)
    {
        return Guid.Parse(user.Claims.FirstOrDefault(c => c.Type == "sub")?.Value ?? throw new ApiException("missing_user_id", "User id not found in claims"));
    }

    public static DateTime RoundDown(this DateTime date, TimeSpan span)
    {
        return new DateTime(date.Ticks / span.Ticks * span.Ticks);
    }
}