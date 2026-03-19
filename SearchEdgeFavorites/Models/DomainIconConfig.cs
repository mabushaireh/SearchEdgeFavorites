// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SearchEdgeFavorites.Models;

public class DomainIconConfig
{
    [JsonPropertyName("domains")]
    public List<DomainIconMapping> Domains { get; set; } = new();

    [JsonPropertyName("defaultIcon")]
    public string DefaultIcon { get; set; } = "\uE774";

    [JsonPropertyName("defaultDescription")]
    public string DefaultDescription { get; set; } = "Web";
}

public class DomainIconMapping
{
    [JsonPropertyName("pattern")]
    public string Pattern { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
