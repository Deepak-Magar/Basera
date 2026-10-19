using System.Text;
using Basera.Shared.Domain;
using Basera.Shared.Files;

namespace Basera.UnitTests.Shared.Web;

public sealed class LocalDiskFileStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "basera-files-" + Guid.NewGuid().ToString("N"));
    private readonly LocalDiskFileStorage storage;
    private readonly FileKey key = new(GuidV7.New(), "residents", GuidV7.New());

    public LocalDiskFileStorageTests() => storage = new LocalDiskFileStorage(root);

    public void Dispose() => Directory.Delete(root, recursive: true);

    [Fact]
    public async Task Saves_and_reads_back_content_and_type_under_the_tenant_folder()
    {
        await using (var content = new MemoryStream(Encoding.UTF8.GetBytes("citizenship front")))
        {
            (await storage.SaveAsync(key, content, "image/jpeg")).Length.Should().Be(17);
        }

        File.Exists(Path.Combine(root, "tenant", key.TenantId.ToString(), "residents", key.Id.ToString())).Should().BeTrue();
        var read = await storage.OpenReadAsync(key);
        read!.ContentType.Should().Be("image/jpeg");
        await using (read.Content)
        {
            (await new StreamReader(read.Content).ReadToEndAsync()).Should().Be("citizenship front");
        }
    }

    [Fact]
    public async Task Missing_files_read_as_null_and_delete_removes_them()
    {
        (await storage.OpenReadAsync(key)).Should().BeNull();
        (await storage.ExistsAsync(key)).Should().BeFalse();

        await storage.SaveAsync(key, new MemoryStream([1, 2, 3]), "application/pdf");
        (await storage.ExistsAsync(key)).Should().BeTrue();

        await storage.DeleteAsync(key);
        (await storage.ExistsAsync(key)).Should().BeFalse();
        Directory.GetFiles(root, "*", SearchOption.AllDirectories).Should().BeEmpty("the meta file goes too");
    }

    [Fact]
    public async Task Saving_again_replaces_the_file()
    {
        await storage.SaveAsync(key, new MemoryStream([1, 2, 3]), "application/pdf");
        await storage.SaveAsync(key, new MemoryStream([9]), "image/png");

        var read = (await storage.OpenReadAsync(key))!;
        await using (read.Content)
        {
            read.Length.Should().Be(1);
            read.ContentType.Should().Be("image/png");
        }
    }

    [Theory]
    [InlineData("../billing")]
    [InlineData("Residents")]
    [InlineData("a")]
    [InlineData("res/idents")]
    [InlineData("")]
    public void File_keys_only_take_plain_module_names(string module)
    {
        var act = () => new FileKey(GuidV7.New(), module, GuidV7.New());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void File_keys_need_real_ids()
    {
        var act = () => new FileKey(Guid.Empty, "residents", GuidV7.New());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Path_follows_the_architecture_layout()
    {
        key.Path.Should().Be($"tenant/{key.TenantId}/residents/{key.Id}");
    }
}
