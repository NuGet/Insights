using Microsoft.CodeAnalysis;

namespace Knapcode.ExplorePackages
{
    public interface IPropertyVisitor
    {
        void OnProperty(PropertyVisitorContext context, IPropertySymbol symbol, string prettyPropType);
        void Finish(PropertyVisitorContext context);
        string GetResult();
    }
}
