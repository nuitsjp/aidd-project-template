using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Shouldly;
using WpfNotesSample.Analyzers;
using Xunit;

namespace WpfNotesSample.UnitTests.Architecture;

public sealed class ArchitectureTests
{
    private const string AssemblyName = "WpfNotesSample";

    [Theory]
    [InlineData("View", "ViewModel")]
    [InlineData("ViewModel", "Domain")]
    [InlineData("Infrastructure", "Domain")]
    [InlineData("View", "View")]
    [InlineData("ViewModel", "ViewModel")]
    [InlineData("Domain", "Domain")]
    [InlineData("Infrastructure", "Infrastructure")]
    [InlineData("", "View")]
    [InlineData("Compatibility", "Domain")]
    [InlineData("Domain", "Compatibility")]
    [InlineData("ViewModels", "Infrastructure")]
    [InlineData("Views", "Infrastructure")]
    public async Task FourLayerAllowedAndOutsideDependenciesHaveNoDiagnostics(string source, string target)
    {
        var diagnostics = await AnalyzeAsync(DependencySource(source, target));

        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("View", "Domain")]
    [InlineData("View", "Infrastructure")]
    [InlineData("ViewModel", "View")]
    [InlineData("ViewModel", "Infrastructure")]
    [InlineData("Domain", "View")]
    [InlineData("Domain", "ViewModel")]
    [InlineData("Domain", "Infrastructure")]
    [InlineData("Infrastructure", "View")]
    [InlineData("Infrastructure", "ViewModel")]
    public async Task OtherDependenciesAreErrors(string source, string target)
    {
        var diagnostics = await AnalyzeAsync(DependencySource(source, target));

        diagnostics.ShouldNotBeEmpty();
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Id == "ARCH001" && diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task UnusedUsingDoesNotCreateADependency()
    {
        var diagnostics = await AnalyzeAsync("""
            using WpfNotesSample.Infrastructure;
            namespace WpfNotesSample.Infrastructure { public class Target {} }
            namespace WpfNotesSample.Domain { public class Source {} }
            """);

        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("using TargetAlias = WpfNotesSample.Infrastructure.Target;", "public TargetAlias Value;")]
    [InlineData("", "public global::WpfNotesSample.Infrastructure.Target Value;")]
    [InlineData("using static WpfNotesSample.Infrastructure.Target;", "public int Value = Number;")]
    [InlineData("", "public int Value = WpfNotesSample.Infrastructure.Target.Number;")]
    [InlineData("", "public object Value = WpfNotesSample.Infrastructure.Target.Create();")]
    [InlineData("", "public System.Collections.Generic.List<WpfNotesSample.Infrastructure.Target> Value;")]
    [InlineData("", "public WpfNotesSample.Infrastructure.Target[] Value;")]
    [InlineData("", "public void Use<T>() where T : WpfNotesSample.Infrastructure.Target {}")]
    public async Task AliasesMembersAndNestedTypesCannotHideADependency(string imports, string member)
    {
        var diagnostics = await AnalyzeAsync($$"""
            {{imports}}
            namespace WpfNotesSample.Infrastructure
            {
                public class Target
                {
                    public const int Number = 1;
                    public static Target Create() => new Target();
                }
            }
            namespace WpfNotesSample.Domain { public class Source { {{member}} } }
            """);

        diagnostics.ShouldNotBeEmpty();
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Id == "ARCH001");
    }

    [Theory]
    [InlineData("public class Source : WpfNotesSample.Infrastructure.Target {}")]
    [InlineData("[WpfNotesSample.Infrastructure.Marker] public class Source {}")]
    [InlineData("public class Source { public int Value = (int)WpfNotesSample.Infrastructure.Kind.First; }")]
    [InlineData("[System.CodeDom.Compiler.GeneratedCode(\"fixture\", \"1\")] public class Source { public WpfNotesSample.Infrastructure.Target Value; }")]
    public async Task BasesAttributesEnumConstantsAndGeneratedCodeAreChecked(string declaration)
    {
        var diagnostics = await AnalyzeAsync($$"""
            namespace WpfNotesSample.Infrastructure
            {
                public class Target {}
                public class MarkerAttribute : System.Attribute {}
                public enum Kind { First }
            }
            namespace WpfNotesSample.Domain { {{declaration}} }
            """);

        diagnostics.ShouldNotBeEmpty();
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Id == "ARCH001");
    }

    [Theory]
    [InlineData("new WpfNotesSample.ViewModel.Target().Set(null)")]
    [InlineData("new WpfNotesSample.ViewModel.Target(null)")]
    [InlineData("new WpfNotesSample.ViewModel.Target().Set(new object())")]
    public async Task MethodAndConstructorSignaturesIncludeImplicitArgumentTypes(string expression)
    {
        var diagnostics = await AnalyzeAsync($$"""
            namespace WpfNotesSample.Domain
            {
                public class Value {}
            }
            namespace WpfNotesSample.ViewModel
            {
                public class Target
                {
                    public Target() {}
                    public Target(WpfNotesSample.Domain.Value value) {}
                    public void Set(WpfNotesSample.Domain.Value value) {}
                    public void Set(object value, WpfNotesSample.Domain.Value other = null) {}
                }
            }
            namespace WpfNotesSample.View { public class Source { public void Use() { {{expression}}; } } }
            """);

        diagnostics.ShouldNotBeEmpty();
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Id == "ARCH001");
    }

    [Fact]
    public async Task ConvertedArgumentTypesAreCheckedAtTheArgument()
    {
        var diagnostics = await AnalyzeAsync("""
            namespace WpfNotesSample.Domain { public class Value {} }
            namespace WpfNotesSample.ViewModel
            {
                public class Target
                {
                    public static implicit operator WpfNotesSample.Domain.Value(Target value) => new WpfNotesSample.Domain.Value();
                }
            }
            namespace WpfNotesSample
            {
                public static class Factory { public static void Set(WpfNotesSample.Domain.Value value) {} }
            }
            namespace WpfNotesSample.View
            {
                public class Source
                {
                    public void Use()
                    {
                        var value = new WpfNotesSample.ViewModel.Target();
                        WpfNotesSample.Factory.Set(value);
                    }
                }
            }
            """);

        diagnostics.ShouldContain(diagnostic => diagnostic.Id == "ARCH001" &&
            diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan) == "value");
    }

    [Fact]
    public async Task ProductAssemblyNameDeterminesTheNamespaceRoot()
    {
        var diagnostics = await AnalyzeAsync(DependencySource("Domain", "Infrastructure", "Acme.Notes"), "Acme.Notes");

        diagnostics.ShouldNotBeEmpty();
        diagnostics.ShouldAllBe(diagnostic => diagnostic.Id == "ARCH001");
    }

    [Fact]
    public async Task ExternalAssemblyTypesAreNotSubjectToLayerRules()
    {
        var external = CreateCompilation("namespace WpfNotesSample.Infrastructure { public class Target {} }", "External.Library");
        using var image = new MemoryStream();
        external.Emit(image, cancellationToken: TestContext.Current.CancellationToken).Success.ShouldBeTrue();
        var reference = MetadataReference.CreateFromImage(image.ToArray());
        var diagnostics = await AnalyzeAsync(
            "namespace WpfNotesSample.Domain { public class Source { public WpfNotesSample.Infrastructure.Target Value; } }",
            additionalReference: reference);

        diagnostics.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Model")]
    [InlineData("Model.Domain.Notes")]
    [InlineData("Model.Infrastructure.Sqlite")]
    public async Task LegacyModelTypeDeclarationsAreRejected(string namespaceSuffix)
    {
        var diagnostics = await AnalyzeAsync($"namespace {AssemblyName}.{namespaceSuffix} {{ public class Legacy {{}} }}");

        diagnostics.ShouldHaveSingleItem().Id.ShouldBe("ARCH002");
        diagnostics[0].Severity.ShouldBe(DiagnosticSeverity.Error);
    }

    private static string DependencySource(string source, string target, string root = AssemblyName)
    {
        var sourceNamespace = source.Length == 0 ? root : root + "." + source;
        return $$"""
            namespace {{root}}.{{target}} { public class Target {} }
            namespace {{sourceNamespace}} { public class Source { public {{root}}.{{target}}.Target Value; } }
            """;
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, string assemblyName = AssemblyName,
        MetadataReference? additionalReference = null)
    {
        var compilation = CreateCompilation(source, assemblyName, additionalReference);
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ShouldBeEmpty();
        return await compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new LayerDependencyAnalyzer()))
            .GetAnalyzerDiagnosticsAsync();
    }

    private static CSharpCompilation CreateCompilation(string source, string assemblyName,
        MetadataReference? additionalReference = null)
    {
        var references = new[] { typeof(object).Assembly, typeof(List<>).Assembly, typeof(GeneratedCodeAttribute).Assembly }
            .Distinct().Select(assembly => MetadataReference.CreateFromFile(assembly.Location)).Cast<MetadataReference>().ToList();
        if (additionalReference is not null)
        {
            references.Add(additionalReference);
        }

        return CSharpCompilation.Create(assemblyName,
            new[] { CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest)) }, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
