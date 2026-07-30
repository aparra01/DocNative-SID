using DocNative.Core.Pdf;
using Xunit;

namespace DocNative.Core.Tests;

[CollectionDefinition(nameof(DocnetCollection), DisableParallelization = true)]
public sealed class DocnetCollection
{
}

[Collection(nameof(DocnetCollection))]
public abstract class DocnetTestBase
{
}
