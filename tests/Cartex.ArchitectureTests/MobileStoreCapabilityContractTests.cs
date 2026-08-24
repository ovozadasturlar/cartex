using System.Buffers.Binary;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Cartex.ArchitectureTests;

public sealed class MobileStoreCapabilityContractTests
{
    [Fact]
    public void RUXSAT_04a_Store_ScanViewModel_CanUseCart_delegates_to_AccessState()
    {
        var root = SolutionRoot.Find();
        var projectRoot = Path.Combine(root, "src", "mobile", "Cartex.Mobile.Store");
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var assemblyPath = Path.Combine(
            projectRoot,
            "bin",
            configuration,
            "net10.0-android",
            "Cartex.Mobile.Store.dll");
        var sourcePath = Path.Combine(projectRoot, "ViewModels", "ScanViewModel.cs");

        Assert.True(File.Exists(assemblyPath),
            $"Store assembly topilmadi: {assemblyPath}. Avval Cartex.Mobile.Store loyihasini build qiling.");
        Assert.True(File.GetLastWriteTimeUtc(assemblyPath) >= File.GetLastWriteTimeUtc(sourcePath),
            $"Store assembly ScanViewModel manbasidan eski: {assemblyPath}. Cartex.Mobile.Store loyihasini qayta build qiling.");

        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();
        var viewModel = metadata.TypeDefinitions
            .Select(metadata.GetTypeDefinition)
            .Single(type => metadata.GetString(type.Name) == "ScanViewModel" &&
                            metadata.GetString(type.Namespace) == "Cartex.Mobile.Store.ViewModels");
        var property = viewModel.GetProperties()
            .Select(metadata.GetPropertyDefinition)
            .Single(definition => metadata.GetString(definition.Name) == "CanUseCart");
        var getterHandle = property.GetAccessors().Getter;

        Assert.False(getterHandle.IsNil);
        var getter = metadata.GetMethodDefinition(getterHandle);
        Assert.True((getter.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public,
            "ScanViewModel.CanUseCart public getter bo'lishi shart.");

        var accessGetterTokens = metadata.MemberReferences
            .Where(handle => IsAccessStateCanUseCartGetter(metadata, handle))
            .Select(handle => MetadataTokens.GetToken(handle))
            .ToHashSet();
        var il = peReader.GetMethodBody(getter.RelativeVirtualAddress).GetILBytes();

        Assert.NotEmpty(accessGetterTokens);
        Assert.NotNull(il);
        Assert.True(CallsAny(il, accessGetterTokens),
            "ScanViewModel.CanUseCart AccessState.CanUseCart ga to'g'ridan-to'g'ri delegatsiya qilishi shart.");
    }

    private static bool IsAccessStateCanUseCartGetter(MetadataReader metadata, MemberReferenceHandle handle)
    {
        var member = metadata.GetMemberReference(handle);
        if (metadata.GetString(member.Name) != "get_CanUseCart" ||
            member.Parent.Kind != HandleKind.TypeReference)
            return false;

        var declaringType = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
        return metadata.GetString(declaringType.Name) == "AccessState" &&
               metadata.GetString(declaringType.Namespace) == "Cartex.Mobile.Core";
    }

    private static bool CallsAny(byte[] il, HashSet<int> tokens)
    {
        for (var index = 0; index <= il.Length - 5; index++)
        {
            if (il[index] is not (0x28 or 0x6f)) continue;
            if (tokens.Contains(BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(index + 1, 4))))
                return true;
        }

        return false;
    }
}
