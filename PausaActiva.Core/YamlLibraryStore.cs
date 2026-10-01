using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PausaActiva.Core;

public sealed class YamlLibraryStore(string path)
{
    public string FilePath { get; } = path;
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreFields().Build();
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance).Build();

    public async Task<ExerciseLibrary> LoadAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (!File.Exists(FilePath))
            {
                var sample = SampleLibrary.Create();
                await WriteAsync(sample);
                return sample;
            }
            var library = Deserializer.Deserialize<ExerciseLibrary>(await File.ReadAllTextAsync(FilePath))
                ?? throw new InvalidDataException("El YAML está vacío.");
            LibraryRules.Validate(library);
            return library;
        }
        finally { gate.Release(); }
    }
    public async Task SaveAsync(ExerciseLibrary library)
    {
        LibraryRules.Validate(library);
        await gate.WaitAsync();
        try { await WriteAsync(library); }
        finally { gate.Release(); }
    }
    private async Task WriteAsync(ExerciseLibrary library)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
        var temporary = FilePath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, Serializer.Serialize(library));
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, FilePath + ".bak");
            else File.Move(temporary, FilePath);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
