using Microsoft.CodeAnalysis;

namespace Knapcode.ExplorePackages
{
    public interface IPropertyVisitor
    {
        void OnProperty(INamedTypeSymbol nullable, IPropertySymbol symbol, string prettyPropType);
        void Finish();
        string GetResult();
    }
}
