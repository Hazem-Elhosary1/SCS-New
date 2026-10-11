namespace Scs.SharedKernel;

/// <summary>
/// Marks a member that holds personal data (name, mobile number, email, national ID, address,
/// clinical content). Such a member is never written to operational logs (SH-094).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class PersonalDataAttribute : Attribute;

/// <summary>
/// Marks a member that holds a secret (password, token, key, connection string). Such a member
/// is never written to a log, a screen or a response (SH-091).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SecretAttribute : Attribute;
