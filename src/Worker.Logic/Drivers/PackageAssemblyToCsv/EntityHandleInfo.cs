// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection.Metadata;

namespace NuGet.Insights.Worker.PackageAssemblyToCsv
{
    public record EntityHandleInfo(
        HandleKind HandleKind,
        string Namespace,
        string Name,
        AssemblyName Assembly,
        EntityHandleInfo Scope)
    {
        private List<EntityHandleInfo> GetScopes()
        {
            var scopes = new List<EntityHandleInfo>();
            var current = this;
            while (current is not null)
            {
                scopes.Add(current);
                current = current.Scope;
            }

            scopes.Reverse();
            return scopes;
        }

        public string GetFullTypeName()
        {
            var scopes = GetScopes();
            var sb = new StringBuilder();
            var afterNamespace = false;
            var i = 0;
            for (; i < scopes.Count; i++)
            {
                var scope = scopes[i];
                if (!string.IsNullOrEmpty(scope.Namespace)
                    && scope.Namespace.All(c => char.IsLetterOrDigit(c) || c == '.'))
                {
                    sb.Append(scope.Namespace);
                    afterNamespace = true;
                    break;
                }
            }

            for (; i < scopes.Count; i++)
            {
                var scope = scopes[i];
                if (!string.IsNullOrEmpty(scope.Name)
                    && scope.Name.All(char.IsLetterOrDigit))
                {
                    if (sb.Length > 0)
                    {
                        if (afterNamespace)
                        {
                            sb.Append('.');
                            afterNamespace = false;
                        }
                        else
                        {
                            sb.Append('+');
                        }
                    }

                    sb.Append(scope.Name);
                }
            }

            return sb.ToString();
        }
    }
}
