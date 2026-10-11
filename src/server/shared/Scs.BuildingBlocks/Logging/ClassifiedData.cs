using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using Scs.SharedKernel;
using Serilog.Core;
using Serilog.Events;

namespace Scs.BuildingBlocks.Logging;

/// <summary>
/// How one of our own types may appear in a log entry (FR-021):
/// members marked <see cref="PersonalDataAttribute"/> or <see cref="SecretAttribute"/> are
/// written as <c>***</c>, and a type from a module's <c>Domain</c> namespace is written as its
/// name and id only, never its contents.
/// </summary>
internal static class ClassifiedData
{
    private static readonly ConcurrentDictionary<Type, TypeShape> Shapes = new();

    public static bool IsOurs(Type type) =>
        type.Namespace is { } ns && ns.StartsWith("Scs.", StringComparison.Ordinal) && !type.IsEnum;

    public static TypeShape ShapeOf(Type type) => Shapes.GetOrAdd(type, Describe);

    /// <summary>The safe text for a domain object: <c>Patient#42</c>, or just <c>Patient</c>.</summary>
    public static string Identify(object value)
    {
        var shape = ShapeOf(value.GetType());
        var id = shape.Id?.GetValue(value);
        return id is null ? shape.Name : $"{shape.Name}#{id}";
    }

    private static TypeShape Describe(Type type)
    {
        var properties = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();

        // On a record, an attribute written on a primary-constructor parameter stays on the
        // parameter, so both places are checked.
        var classifiedParameters = type
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SelectMany(c => c.GetParameters())
            .Where(p => IsClassified(p.GetCustomAttributes()))
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var members = properties
            .Select(p => new Member(p, IsClassified(p.GetCustomAttributes()) || classifiedParameters.Contains(p.Name)))
            .ToArray();

        var isDomain = type.Namespace!.Split('.').Contains("Domain", StringComparer.Ordinal);
        var id = properties.FirstOrDefault(p => p.Name == "Id" && IsSimple(p.PropertyType));

        return new TypeShape(type.Name, isDomain, id, members);
    }

    private static bool IsClassified(IEnumerable<Attribute> attributes) =>
        attributes.Any(a => a is PersonalDataAttribute or SecretAttribute);

    private static bool IsSimple(Type type) =>
        type.IsPrimitive || type == typeof(Guid) || type == typeof(string) || type == typeof(long);

    internal sealed record Member(PropertyInfo Property, bool Classified);

    internal sealed record TypeShape(string Name, bool IsDomain, PropertyInfo? Id, IReadOnlyList<Member> Members)
    {
        public bool HasClassifiedMembers => Members.Any(m => m.Classified);
    }
}

/// <summary>Applies when an object is logged for its structure (<c>{@Object}</c>).</summary>
public sealed class ClassifiedDataDestructuringPolicy : IDestructuringPolicy
{
    public bool TryDestructure(
        object value, ILogEventPropertyValueFactory propertyValueFactory, out LogEventPropertyValue result)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(propertyValueFactory);

        var type = value.GetType();
        if (!ClassifiedData.IsOurs(type))
        {
            result = null!;
            return false;
        }

        var shape = ClassifiedData.ShapeOf(type);
        if (shape.IsDomain)
        {
            result = new ScalarValue(ClassifiedData.Identify(value));
            return true;
        }

        if (!shape.HasClassifiedMembers)
        {
            result = null!;
            return false;
        }

        var properties = shape.Members.Select(member => new LogEventProperty(
            member.Property.Name,
            member.Classified
                ? new ScalarValue(ScrubbingJsonFormatter.Mask)
                : propertyValueFactory.CreatePropertyValue(member.Property.GetValue(value), destructureObjects: true)));

        result = new StructureValue(properties, shape.Name);
        return true;
    }
}

/// <summary>
/// Closes two gaps the destructuring policy cannot reach.
///
/// An object logged as plain text (<c>{Object}</c>) is written with its <c>ToString()</c>,
/// which for a record prints every member, and by then only the text is left. Text of that
/// shape (<c>Name { A = 1, B = 2 }</c>) is replaced by the type name alone.
///
/// The web framework adds the actual request path, with its values, to every entry of a
/// request. It is removed; the route template is logged instead.
/// </summary>
public sealed partial class ClassifiedScalarEnricher : ILogEventEnricher
{
    private static readonly string[] RequestProperties = ["RequestPath", "RequestId", "ConnectionId"];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        foreach (var name in RequestProperties)
        {
            logEvent.RemovePropertyIfPresent(name);
        }

        List<LogEventProperty>? replacements = null;

        foreach (var (name, value) in logEvent.Properties)
        {
            if (value is not ScalarValue { Value: { } scalar })
            {
                continue;
            }

            if (scalar is string text)
            {
                var printed = PrintedRecord().Match(text);
                if (printed.Success)
                {
                    (replacements ??= []).Add(new LogEventProperty(name, new ScalarValue(printed.Groups[1].Value)));
                }
            }
            else if (ClassifiedData.IsOurs(scalar.GetType()))
            {
                (replacements ??= []).Add(new LogEventProperty(name, new ScalarValue(ClassifiedData.Identify(scalar))));
            }
        }

        if (replacements is not null)
        {
            foreach (var replacement in replacements)
            {
                logEvent.AddOrUpdateProperty(replacement);
            }
        }
    }

    /// <summary>The text a record's generated <c>ToString()</c> produces.</summary>
    [GeneratedRegex(@"^([A-Za-z_][A-Za-z0-9_]*) \{ [A-Za-z_][A-Za-z0-9_]* = .*\}$", RegexOptions.Singleline)]
    private static partial Regex PrintedRecord();
}
