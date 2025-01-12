// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

using System.Text.Json;
using Azure.Core;

namespace Bicep.Core.RegistryClient.Models
{
    internal partial class AcrAccessToken
    {
        internal static AcrAccessToken DeserializeAcrAccessToken(JsonElement element)
        {
            Optional<string> accessToken = default;
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("access_token"))
                {
                    accessToken = property.Value.GetString();
                    continue;
                }
            }
            return new AcrAccessToken(accessToken.Value);
        }
    }
}
