using System.Text;
using api_gestion.Data;
using api_gestion.Hubs;
using api_gestion.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace api_gestion
{
    public class Program
    {
        public static void Main(string[] args)
        {
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            var builder = WebApplication.CreateBuilder(args);

            // --- Services ---
            builder.Services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "API Gestion & Suivi de Stock", Version = "v1" });
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Entrez le jeton JWT précédé de 'Bearer '"
                });
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddScoped<JwtService>();
            builder.Services.AddScoped<BarcodeService>();
            builder.Services.AddSignalR();

            var origenesAutorisees = builder.Configuration.GetSection("Cors:OrigensAutorisees").Get<string[]>()
                ?? new[] { "http://localhost:3000", "http://localhost:3001", "https://gestion-nine-mu.vercel.app", "https://scan-ten-rho.vercel.app" };

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("PolitiqueFront", policy =>
                {
                    policy.WithOrigins(origenesAutorisees)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            var jwtSection = builder.Configuration.GetSection("Jwt");
            var jwtKey = jwtSection["Key"] ?? "cle-secrete-par-defaut-a-changer-en-production-1234567890";

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSection["Issuer"],
                    ValidAudience = jwtSection["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };

                // Permet au hub SignalR de recevoir le token via query string
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/stock"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            builder.Services.AddAuthorization();

            var app = builder.Build();

            // --- Migration + seed automatique au démarrage ---
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                DataSeeder.SeedAsync(db).GetAwaiter().GetResult();
            }

            // --- Pipeline HTTP ---
            // Le TLS est terminé par le proxy d'hébergement (Render, Vercel, ...),
            // on fait confiance aux en-têtes X-Forwarded-* pour connaître le vrai schéma/IP du client.
            var optionsEntetesTransferes = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            };
            optionsEntetesTransferes.KnownIPNetworks.Clear();
            optionsEntetesTransferes.KnownProxies.Clear();
            app.UseForwardedHeaders(optionsEntetesTransferes);

            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "API Gestion & Suivi de Stock v1");
            });

            app.UseCors("PolitiqueFront");

            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.MapHub<StockHub>("/hubs/stock");

            app.MapGet("/", () => Results.Ok(new { statut = "ok", message = "API Gestion & Suivi de Stock en ligne" }));

            app.Run();
        }
    }
}
