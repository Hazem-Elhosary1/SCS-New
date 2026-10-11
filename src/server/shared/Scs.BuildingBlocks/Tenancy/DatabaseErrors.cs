using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Scs.BuildingBlocks.Tenancy;

/// <summary>
/// Recognises database refusals a module needs to tell apart, so that no module has to
/// reference the database client itself (only the shared project may).
/// </summary>
public static class DatabaseErrors
{
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    /// <summary>The save was refused because a value that must be unique already exists.</summary>
    public static bool IsUniqueViolation(this DbUpdateException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation };
    }
}
