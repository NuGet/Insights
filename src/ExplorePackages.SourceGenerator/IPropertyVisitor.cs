using Microsoft.CodeAnalysis;

namespace Knapcode.ExplorePackages
{
    public interface IPropertyVisitor
    {
        void OnProperty(GeneratorExecutionContext context, INamedTypeSymbol nullable, IPropertySymbol symbol, string prettyPropType);
        void Finish(GeneratorExecutionContext context);
        string GetResult();
    }
}
