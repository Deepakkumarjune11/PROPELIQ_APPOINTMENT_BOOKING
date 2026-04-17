namespace PatientAccess.Data.Seeding;

public sealed class NoOpDataSeeder : IDataSeeder
{
    public Task SeedAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
