using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MimeKit;
using Qoot.Api.Data;
using Qoot.Api.Domain;
using Qoot.Api.Infrastructure;

namespace Qoot.Api.Services;

/// <summary>The Contact us form: name, phone and message are required, the email is optional.</summary>
public sealed record ContactRequest(
    string? Name,
    string? Phone,
    string? Email,
    string? Message,
    string? Language,
    string? Website);

public sealed record SubmissionDto(
    int Id,
    string Name,
    string Phone,
    string Email,
    string Message,
    string Language,
    string Status,
    bool IsRead,
    string AdminNotes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record UpdateSubmissionRequest(string? Status, string? AdminNotes, bool? IsRead);

public sealed record SubmissionQuery(string? Status, string? Search, int? Page, int? PageSize);

/// <summary>Messages sent from the Contact us form, and what the dashboard does with them.</summary>
public sealed class ContactService(
    QootDbContext db,
    AuditService audit,
    ICurrentUser user,
    TimeProvider clock,
    NotificationQueue notifications)
{
    public const int NameMax = 120;
    public const int PhoneMax = 40;
    public const int EmailMax = 254;
    public const int MessageMax = 2000;

    public async Task SubmitAsync(ContactRequest r, CancellationToken ct)
    {
        // Bots fill every field, people never see this one: accept quietly and keep nothing.
        if (!string.IsNullOrWhiteSpace(r.Website))
        {
            return;
        }

        var errors = new FieldErrors();
        var name = SafeText.Clean(r.Name);
        var phone = SafeText.Clean(r.Phone);
        var email = SafeText.Clean(r.Email);
        var message = SafeText.Clean(r.Message, multiline: true);

        Check(errors, "name", name, NameMax, required: true);
        Check(errors, "phone", phone, PhoneMax, required: true);
        Check(errors, "email", email, EmailMax);
        Check(errors, "message", message, MessageMax, required: true);
        if (phone.Length is > 0 and <= PhoneMax && !SafeText.LooksLikePhone(phone))
        {
            errors.Add("phone", "invalid_phone_number");
        }

        if (email.Length is > 0 and <= EmailMax && !SafeText.IsEmail(email))
        {
            errors.Add("email", "invalid_email");
        }

        errors.ThrowIfAny();

        var now = clock.GetUtcNow().UtcDateTime;
        var submission = new ContactSubmission
        {
            Name = name,
            Phone = phone,
            Email = email,
            Message = message,
            Language = r.Language == "en" ? "en" : "ar",
            Status = SubmissionStatus.New,
            CreatedAt = now,
            UpdatedAt = now,
            IpHash = user.IpAddress is { } ip ? Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("qoot:" + ip)))[..32] : null,
            UserAgent = user.UserAgent,
        };
        db.ContactSubmissions.Add(submission);
        await db.SaveChangesAsync(ct);
        notifications.Enqueue(submission.Id);
    }

    public async Task<PagedResult<SubmissionDto>> ListAsync(SubmissionQuery q, CancellationToken ct)
    {
        var (page, size) = Paging.Normalize(q.Page, q.PageSize, defaultSize: 25);
        var query = db.ContactSubmissions.AsNoTracking();
        query = q.Status switch
        {
            "unread" => query.Where(s => !s.IsRead),
            "new" => query.Where(s => s.Status == SubmissionStatus.New),
            "in_progress" => query.Where(s => s.Status == SubmissionStatus.InProgress),
            "done" => query.Where(s => s.Status == SubmissionStatus.Done),
            "archived" => query.Where(s => s.Status == SubmissionStatus.Archived),
            "open" => query.Where(s => s.Status != SubmissionStatus.Archived),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = $"%{q.Search.Trim()}%";
            query = query.Where(s => EF.Functions.Like(s.Name, term) || EF.Functions.Like(s.Phone, term) || EF.Functions.Like(s.Email, term) || EF.Functions.Like(s.Message, term));
        }

        var result = await query.OrderByDescending(s => s.CreatedAt).ThenByDescending(s => s.Id).ToPageAsync(page, size, ct);
        return new PagedResult<SubmissionDto>(result.Items.Select(ToDto).ToList(), result.Page, result.PageSize, result.Total);
    }

    public async Task<SubmissionDto> GetAsync(int id, CancellationToken ct)
    {
        var s = await db.ContactSubmissions.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiError.NotFound();
        if (!s.IsRead)
        {
            s.IsRead = true;
            await db.SaveChangesAsync(ct);
        }

        return ToDto(s);
    }

    public async Task<SubmissionDto> UpdateAsync(int id, UpdateSubmissionRequest r, CancellationToken ct)
    {
        var s = await db.ContactSubmissions.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiError.NotFound();
        if (r.Status is not null)
        {
            s.Status = r.Status switch
            {
                "new" => SubmissionStatus.New,
                "in_progress" => SubmissionStatus.InProgress,
                "done" => SubmissionStatus.Done,
                "archived" => SubmissionStatus.Archived,
                _ => throw ApiError.Validation("status", "invalid_status"),
            };
        }

        if (r.AdminNotes is not null)
        {
            var notes = SafeText.Clean(r.AdminNotes, multiline: true);
            if (notes.Length > 2000)
            {
                throw ApiError.Validation("adminNotes", "too_long", 2000);
            }

            s.AdminNotes = notes;
        }

        if (r.IsRead is { } read)
        {
            s.IsRead = read;
        }

        s.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        audit.Record(AuditActions.MessageUpdated, "message", id.ToString(CultureInfo.InvariantCulture), $"Contact message #{id}: status {StatusName(s.Status)}");
        await db.SaveChangesAsync(ct);
        return ToDto(s);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var s = await db.ContactSubmissions.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiError.NotFound();
        db.ContactSubmissions.Remove(s);
        audit.Record(AuditActions.MessageDeleted, "message", id.ToString(CultureInfo.InvariantCulture), $"Deleted contact message #{id} from {s.Name}");
        await db.SaveChangesAsync(ct);
    }

    private static void Check(FieldErrors errors, string field, string value, int max, bool required = false)
    {
        if (required && value.Length == 0)
        {
            errors.Add(field, "required");
        }
        else if (value.Length > max)
        {
            errors.Add(field, "too_long", max);
        }
    }

    internal static string StatusName(SubmissionStatus s) => s switch
    {
        SubmissionStatus.InProgress => "in_progress",
        SubmissionStatus.Done => "done",
        SubmissionStatus.Archived => "archived",
        _ => "new",
    };

    private static SubmissionDto ToDto(ContactSubmission s) => new(
        s.Id, s.Name, s.Phone, s.Email, s.Message, s.Language, StatusName(s.Status), s.IsRead, s.AdminNotes, s.CreatedAt, s.UpdatedAt);
}

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>Leave empty to turn email notifications off.</summary>
    public string Host { get; set; } = "";

    public int Port { get; set; } = 587;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";

    /// <summary>StartTls (587), SslOnConnect (465) or None (local relay only).</summary>
    public string Security { get; set; } = "StartTls";

    /// <summary>Dashboard address used in the email's link, e.g. https://www.qoot.com/admin/</summary>
    public string DashboardUrl { get; set; } = "";
}

/// <summary>Hands new Contact us messages to the background mailer so visitors never wait on SMTP.</summary>
public sealed class NotificationQueue
{
    private readonly Channel<int> _channel = Channel.CreateBounded<int>(new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Enqueue(int submissionId) => _channel.Writer.TryWrite(submissionId);

    public IAsyncEnumerable<int> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>Emails the address set in Settings → Notifications when a Contact us message arrives.</summary>
public sealed class ContactNotificationService(
    NotificationQueue queue,
    IServiceScopeFactory scopes,
    IOptions<SmtpOptions> smtp,
    ILogger<ContactNotificationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var id in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await SendAsync(id, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not email the notification for contact message {Id}", id);
            }
        }
    }

    private async Task SendAsync(int id, CancellationToken ct)
    {
        var options = smtp.Value;
        if (string.IsNullOrWhiteSpace(options.Host) || string.IsNullOrWhiteSpace(options.From))
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var settings = await scope.ServiceProvider.GetRequiredService<SettingsService>().GetRawAsync(ct);
        var to = settings["notifyEmail"]?.GetValue<string>() ?? "";
        if (!SafeText.IsEmail(to))
        {
            return;
        }

        var db = scope.ServiceProvider.GetRequiredService<QootDbContext>();
        var s = await db.ContactSubmissions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (s is null)
        {
            return;
        }

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(options.From));
        message.To.Add(MailboxAddress.Parse(to));
        if (SafeText.IsEmail(s.Email))
        {
            message.ReplyTo.Add(new MailboxAddress(s.Name, s.Email));
        }

        message.Subject = $"Contact us — {s.Name}";
        var body = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"Name: {s.Name}")
            .AppendLine(CultureInfo.InvariantCulture, $"Phone: {s.Phone}")
            .AppendLine(CultureInfo.InvariantCulture, $"Email: {(s.Email.Length > 0 ? s.Email : "-")}")
            .AppendLine()
            .AppendLine(s.Message)
            .AppendLine()
            .AppendLine(options.DashboardUrl.Length > 0 ? $"Open in the dashboard: {options.DashboardUrl.TrimEnd('/')}/messages/{s.Id}" : "");
        message.Body = new TextPart("plain") { Text = body.ToString() };

        using var client = new SmtpClient();
        var security = options.Security switch
        {
            "SslOnConnect" => SecureSocketOptions.SslOnConnect,
            "None" => SecureSocketOptions.None,
            _ => SecureSocketOptions.StartTls,
        };
        await client.ConnectAsync(options.Host, options.Port, security, ct);
        if (options.User.Length > 0)
        {
            await client.AuthenticateAsync(options.User, options.Password, ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
