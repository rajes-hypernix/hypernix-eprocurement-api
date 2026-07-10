using eProcure.Application.Abstractions;

namespace eProcure.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
