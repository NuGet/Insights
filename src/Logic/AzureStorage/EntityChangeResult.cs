// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure;

#nullable enable

namespace NuGet.Insights
{
    public enum EntityChangeResultType
    {
        Success = 1,
        PreconditionFailed,
    }

    public class EntityChangeResult
    {
        private static EntityChangeResult PreconditionFailedInstance = new EntityChangeResult(EntityChangeResultType.PreconditionFailed, response: null);

        private readonly Response? _response;

        private EntityChangeResult(EntityChangeResultType type, Response? response)
        {
            Type = type;
            _response = response;
        }

        public EntityChangeResultType Type { get; }

        public Response Response
        {
            get => _response ?? throw new InvalidOperationException();
        }

        public static EntityChangeResult Success(Response response)
        {
            return new EntityChangeResult(EntityChangeResultType.Success, response);
        }

        public static EntityChangeResult PreconditionFailed()
        {
            return PreconditionFailedInstance;
        }
    }
}
