using ConfigAdmin.Application;
using ConfigAdmin.Application.Services;
using ConfigAdmin.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConfigAdmin.Tests;

public class ProfileServiceTests
{
    [Fact]
    public async Task AddOrUpdateClientAsync_RenameById_UpdatesExistingClientWithoutDuplicate()
    {
        var dbPath = CreateTempDbPath();
        var services = new ServiceCollection();
        services.AddConfigAdminApplication(dbPath);
        var provider = services.BuildServiceProvider();

        await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();

        var profileService = provider.GetRequiredService<ProfileService>();
        var original = await profileService.AddOrUpdateClientAsync(
            "Old Name",
            @"D:\Exports",
            comment: "first");

        var updated = await profileService.AddOrUpdateClientAsync(
            "New Name",
            @"D:\Exports\renamed",
            comment: "renamed",
            clientId: original.Id);

        var clients = await profileService.GetClientsAsync();

        Assert.Single(clients);
        Assert.Equal(original.Id, updated.Id);
        Assert.Equal("New Name", updated.Name);
        Assert.Equal(@"D:\Exports\renamed", updated.ExportRootPath);
        Assert.Equal("renamed", updated.Comment);
    }

    private static string CreateTempDbPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"configadmin-test-{Guid.NewGuid():N}.db");
        if (File.Exists(path))
            File.Delete(path);
        return path;
    }
}
