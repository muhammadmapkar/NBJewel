// Role: Two small exception types that repositories throw so the controllers
// can turn them into clear HTTP errors. Validation becomes 400, a bad status
// move becomes 409.

namespace NbJewel.Api.Models;

public sealed class DomainValidationException(string message) : Exception(message);

public sealed class InvalidStatusMoveException(string message) : Exception(message);
