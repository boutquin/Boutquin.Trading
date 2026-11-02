using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;

// Inspect dependency DLL metadata without loading its runtime dependencies.
if (args[0] == "--assembly-only")
{
    var identityAssembly = AssemblyName.GetAssemblyName(args[1]);
    var identityFile = FileVersionInfo.GetVersionInfo(args[1]);
    // MinVer policy: the assembly version is <major>.0.0.0; the file version is the package core plus .0.
    var identityCore = args[2].Split('-')[0];
    if (identityAssembly.Version?.ToString() != identityCore.Split('.')[0] + ".0.0.0" || identityFile.FileVersion != identityCore + ".0"
        || identityFile.ProductVersion != args[2] + "+" + args[3])
    {
        throw new InvalidOperationException($"Wrong dependency identity: {identityAssembly.Version}/{identityFile.FileVersion}/{identityFile.ProductVersion}");
    }

    Console.WriteLine(JsonSerializer.Serialize(new
    {
        name = identityAssembly.Name, assemblyVersion = identityAssembly.Version.ToString(),
        fileVersion = identityFile.FileVersion, informationalVersion = identityFile.ProductVersion,
    }));
    return;
}

// Inspect actual packaged bytes without loading the package's runtime dependencies.
var assembly = AssemblyName.GetAssemblyName(args[0]);
var file = FileVersionInfo.GetVersionInfo(args[0]);
var version = args[2];
var commit = args[3];
var core = version.Split('-')[0];
if (assembly.Version?.ToString() != core.Split('.')[0] + ".0.0.0" || file.FileVersion != core + ".0"
    || file.ProductVersion != version + "+" + commit)
{
    throw new InvalidOperationException($"Wrong identity: {assembly.Version}/{file.FileVersion}/{file.ProductVersion}");
}
using var peStream = File.OpenRead(args[0]);
using var pe = new PEReader(peStream);
var metadata = pe.GetMetadataReader();
var exportedTypes = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition)
    .Count(type => (type.Attributes & TypeAttributes.VisibilityMask) is TypeAttributes.Public or TypeAttributes.NestedPublic);
var entry = pe.ReadDebugDirectory().Single(item => item.Type == DebugDirectoryEntryType.CodeView);
var codeView = pe.ReadCodeViewDebugDirectoryData(entry);
using var pdbStream = File.OpenRead(args[1]);
using var provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
var reader = provider.GetMetadataReader();
var id = reader.DebugMetadataHeader!.Id;
if (new Guid(id.AsSpan(0, 16)) != codeView.Guid
    || System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(id.AsSpan(16, 4)) != entry.Stamp)
{
    throw new InvalidOperationException("Portable PDB identity differs from the DLL CodeView entry.");
}
var sourceLinkKind = new Guid("CC110556-A091-4D38-9FEC-25AB9A351A6A");
var sourceLink = reader.CustomDebugInformation.Select(reader.GetCustomDebugInformation)
    .Where(item => reader.GetGuid(item.Kind) == sourceLinkKind)
    .Select(item => Encoding.UTF8.GetString(reader.GetBlobBytes(item.Value))).Single();
using var mappings = JsonDocument.Parse(sourceLink);
var documents = mappings.RootElement.GetProperty("documents").EnumerateObject().ToArray();
if (documents.Length == 0 || documents.Any(item =>
    !item.Value.GetString()!.StartsWith("https://raw.githubusercontent.com/boutquin/Boutquin.Trading/" + commit + "/", StringComparison.Ordinal)))
{
    throw new InvalidOperationException("SourceLink does not resolve every document to the public candidate commit.");
}
var embeddedSourceKind = new Guid("0E8A571B-6926-466E-B4AD-8AB04611F5FE");
foreach (var documentHandle in reader.Documents)
{
    var path = reader.GetString(reader.GetDocument(documentHandle).Name);
    var embedded = reader.GetCustomDebugInformation(documentHandle)
        .Select(reader.GetCustomDebugInformation)
        .Any(item => reader.GetGuid(item.Kind) == embeddedSourceKind);
    if (!embedded && !documents.Any(mapping => MatchesDocument(mapping.Name, path)))
    {
        throw new InvalidOperationException($"PDB document has neither embedded source nor SourceLink coverage: {path}");
    }
}

static bool MatchesDocument(string pattern, string path)
{
    var wildcard = pattern.IndexOf('*');
    if (wildcard < 0)
    {
        return string.Equals(pattern, path, StringComparison.Ordinal);
    }

    return pattern.LastIndexOf('*') == wildcard
        && path.Length >= pattern.Length - 1
        && path.StartsWith(pattern[..wildcard], StringComparison.Ordinal)
        && path.EndsWith(pattern[(wildcard + 1)..], StringComparison.Ordinal);
}

Console.WriteLine(JsonSerializer.Serialize(new
{
    name = assembly.Name, assemblyVersion = assembly.Version.ToString(),
    fileVersion = file.FileVersion, informationalVersion = file.ProductVersion,
    pdbId = Convert.ToHexString(id.AsSpan()), sourceLink, exportedTypes,
}));
