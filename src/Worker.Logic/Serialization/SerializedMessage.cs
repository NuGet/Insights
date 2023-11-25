// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker
{
    public class SerializedMessage : ISerializedEntity
    {
        private readonly Lazy<JsonElement> _json;
        private readonly Lazy<string> _string;

        public SerializedMessage(Func<JsonElement> getJson)
        {
            _json = new Lazy<JsonElement>(getJson);
            _string = new Lazy<string>(() =>
            {
                return JsonSerializer.Serialize(_json.Value);
            });
        }

        public string AsString()
        {
            return _string.Value;
        }

        public JsonElement AsJsonElement()
        {
            return _json.Value;
        }
    }
}
