// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace SearchEdgeFavorites.Services;

public interface IAiSummaryService
{
    Task<string> GenerateSummaryAsync(string title, string content, string url);
    bool IsConfigured();
    string ProviderName { get; }
}
