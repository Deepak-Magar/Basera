using Basera.Shared.Domain;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Basera.Shared.Persistence;

/// <summary>Stores <see cref="Money"/> as a <c>bigint</c> of paisa (ADR-009). Applied to every Money property by convention.</summary>
public sealed class MoneyConverter() : ValueConverter<Money, long>(money => money.Paisa, paisa => Money.FromPaisa(paisa));
