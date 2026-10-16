using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Scs.BuildingBlocks.Configuration;

namespace Scs.BuildingBlocks.Mail;

/// <summary>
/// Sends one email and returns only when the mail server has accepted it. Callers that must
/// tell the person whether a message left (sign-in) wait for it; the notifications engine
/// (spec 008) will own templates, language and retries.
/// </summary>
public interface IEmailSender
{
    /// <exception cref="EmailNotSentException">The message could not be handed over in time.</exception>
    Task SendAsync(EmailMessage message, CancellationToken cancellation);
}

/// <param name="To">The recipient. Personal data: never logged.</param>
public sealed record EmailMessage(string To, string Subject, string TextBody, string HtmlBody)
{
    /// <summary>
    /// The name shown as the sender: the platform's, in the email's language (spec 094,
    /// FR-009a). Empty for a message not made by the template.
    /// </summary>
    public string SenderName { get; init; } = "";

    /// <summary>The type name only, so a message logged by mistake shows nothing.</summary>
    public override string ToString() => nameof(EmailMessage);
}

/// <summary>The message did not leave. Carries no address and no server detail.</summary>
public sealed class EmailNotSentException : Exception
{
    public EmailNotSentException()
        : base("The email could not be handed to the mail server.")
    {
    }

    public EmailNotSentException(string message)
        : base(message)
    {
    }

    public EmailNotSentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed partial class SmtpEmailSender(IOptions<ScsOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(message);
        var mail = options.Value.Mail;

        using var mime = new MimeMessage();
        var from = MailboxAddress.Parse(mail.From);
        if (message.SenderName.Length > 0)
        {
            // The address is the mail server's; the name beside it is the platform's.
            from.Name = message.SenderName;
        }

        mime.From.Add(from);
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody }.ToMessageBody();

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        limit.CancelAfter(TimeSpan.FromSeconds(mail.TimeoutSeconds));

        try
        {
            using var client = new SmtpClient { Timeout = mail.TimeoutSeconds * 1000 };
            await client.ConnectAsync(
                mail.Host,
                mail.Port,
                mail.UseTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                limit.Token).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(mail.User))
            {
                await client.AuthenticateAsync(mail.User, mail.Password, limit.Token).ConfigureAwait(false);
            }

            await client.SendAsync(mime, limit.Token).ConfigureAwait(false);
            await client.DisconnectAsync(quit: true, limit.Token).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Whatever went wrong, the caller needs one answer: it was not sent.
        catch (Exception failure) when (!cancellation.IsCancellationRequested)
#pragma warning restore CA1031
        {
            // The type only: a mail library's message can carry the recipient or the server name.
            LogNotSent(logger, failure.GetType().Name);
            throw new EmailNotSentException();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "An email could not be sent ({failure})")]
    private static partial void LogNotSent(ILogger logger, string failure);
}

public static class MailExtensions
{
    public static IServiceCollection AddScsMail(this IServiceCollection services)
    {
        services.TryAddSingleton<IEmailSender, SmtpEmailSender>();
        services.TryAddSingleton<EmailRenderer>();
        return services;
    }
}
