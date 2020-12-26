using Microsoft.CodeAnalysis;

namespace Knapcode.ExplorePackages
{
    public interface IPropertyVisitor
    {
        void OnProperty(IPropertySymbol symbol, string prettyPropType);
        void Finish();
        string GetResult();
    }
}
