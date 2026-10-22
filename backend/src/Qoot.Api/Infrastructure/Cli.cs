using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Qoot.Api.Data;
using Qoot.Api.Services;

namespace Qoot.Api.Infrastructure;

/// <summary>
/// Maintenance commands, run with the same configuration as the app:
/// <code>
///   dotnet Qoot.Api.dll migrate
///   dotnet Qoot.Api.dll seed
///   dotnet Qoot.Api.dll create-admin --email admin@qoot.com [--name "Admin"]
///   dotnet Qoot.Api.dll reset-password --email admin@qoot.com
/// </code>
/// create-admin and reset-password ask for the password twice without echoing it (or read
/// QOOT_ADMIN_PASSWORD when there is no terminal), so it never appears in shell history or process lists.
/// </summary>
public static class Cli
{
    private static readonly string[] Commands = ["migrate", "seed", "create-admin", "reset-password"];

    public static bool IsCommand(string arg) => Commands.Contains(arg, StringComparer.OrdinalIgnoreCase);

    public static async Task<int> RunAsync(WebApplication app, string[] args)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<QootDbContext>();
        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "migrate":
                    if (!db.Database.IsRelational())
                    {
                        Console.Error.WriteLine("The configured provider has no migrations (InMemory).");
                        return 1;
                    }

                    var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
                    Console.WriteLine(pending.Count == 0 ? "The database is up to date." : $"Applying {pending.Count} migration(s): {string.Join(", ", pending)}");
                    await db.Database.MigrateAsync();
                    await services.GetRequiredService<DataSeeder>().SeedAsync();
                    Console.WriteLine("Done. Content seeded where missing.");
                    return 0;

                case "seed":
                    await services.GetRequiredService<DataSeeder>().SeedAsync();
                    Console.WriteLine("Seed complete (only missing content was added).");
                    return 0;

                case "create-admin":
                    return await CreateAdminAsync(services, args);

                case "reset-password":
                    return await ResetPasswordAsync(services, args);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed: {ex.Message}");
            return 1;
        }

        return 1;
    }

    private static async Task<int> CreateAdminAsync(IServiceProvider services, string[] args)
    {
        var email = EmailOption(args, "create-admin --email you@example.com [--name \"Your Name\"]");
        if (email is null)
        {
            return 1;
        }

        var seeder = services.GetRequiredService<DataSeeder>();
        var password = ReadNewPassword(email);
        if (password is null)
        {
            return 1;
        }

        var result = await seeder.CreateAdminAsync(email, Option(args, "--name"), password);
        if (!result.Succeeded)
        {
            WriteErrors(result);
            if (result.Errors.Any(e => e.Code == "DuplicateEmail"))
            {
                Console.Error.WriteLine($"To set a new password for it: reset-password --email {email}");
            }

            return 1;
        }

        Console.WriteLine($"Administrator {email} created. Sign in at /admin/.");
        return 0;
    }

    private static async Task<int> ResetPasswordAsync(IServiceProvider services, string[] args)
    {
        var email = EmailOption(args, "reset-password --email you@example.com");
        if (email is null)
        {
            return 1;
        }

        var seeder = services.GetRequiredService<DataSeeder>();
        if (!(await seeder.AccountEmailsAsync()).Contains(email, StringComparer.OrdinalIgnoreCase))
        {
            await WriteNoAccountAsync(seeder, email);
            return 1;
        }

        var password = ReadNewPassword(email);
        if (password is null)
        {
            return 1;
        }

        var result = await seeder.ResetPasswordAsync(email, password);
        if (!result.Succeeded)
        {
            WriteErrors(result);
            return 1;
        }

        Console.WriteLine($"New password set for {email}; the account is unlocked and its other sessions ended. Sign in at /admin/.");
        return 0;
    }

    private static async Task WriteNoAccountAsync(DataSeeder seeder, string email)
    {
        var existing = await seeder.AccountEmailsAsync();
        Console.Error.WriteLine($"There is no account for {email}.");
        Console.Error.WriteLine(existing.Count == 0
            ? "No accounts exist yet: create the first one with create-admin --email you@example.com"
            : $"Existing accounts: {string.Join(", ", existing)}");
    }

    private static string? EmailOption(string[] args, string usage)
    {
        var email = Option(args, "--email")?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email) || !SafeText.IsEmail(email))
        {
            Console.Error.WriteLine($"Usage: {usage}");
            return null;
        }

        return email;
    }

    private static void WriteErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            Console.Error.WriteLine($"- {error.Description}");
        }
    }

    /// <summary>Reads the new password twice from the terminal, or QOOT_ADMIN_PASSWORD without one.</summary>
    private static string? ReadNewPassword(string email)
    {
        string? password;
        if (!Console.IsInputRedirected)
        {
            password = ReadSecret($"Password for {email} (at least {PasswordRules.MinLength} characters, upper and lower case and a digit): ");
            var again = ReadSecret("Repeat the password: ");
            if (password != again)
            {
                Console.Error.WriteLine("The passwords do not match.");
                return null;
            }
        }
        else
        {
            password = Environment.GetEnvironmentVariable("QOOT_ADMIN_PASSWORD");
        }

        if (string.IsNullOrEmpty(password))
        {
            Console.Error.WriteLine("No password given (type it when asked, or set QOOT_ADMIN_PASSWORD when running without a terminal).");
            return null;
        }

        // Typed blind, a password entered with another keyboard layout (Arabic, for instance) would
        // not match what the person types later in the browser.
        if (password.Any(c => c > '~'))
        {
            Console.Error.WriteLine("The password contains letters outside A-Z (is the keyboard set to another language? Alt+Shift or Win+Space switches it). Type it again with the English layout.");
            return null;
        }

        return password;
    }

    private static string? Option(string[] args, string name)
    {
        var i = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static string ReadSecret(string prompt)
    {
        Console.Write(prompt);
        var buffer = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return buffer.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (buffer.Length > 0)
                {
                    buffer.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                buffer.Append(key.KeyChar);
            }
        }
    }
}
