// Role: The exception type that repositories throw for bad input, so the
// controllers can turn it into a clear HTTP 400 message.

namespace NbJewel.Api.Models;

public sealed class DomainValidationException(string message) : Exception(message);
