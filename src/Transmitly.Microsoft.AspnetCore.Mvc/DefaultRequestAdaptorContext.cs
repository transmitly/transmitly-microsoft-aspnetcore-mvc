// ﻿﻿Copyright (c) Code Impressions, LLC. All Rights Reserved.
//  
//  Licensed under the Apache License, Version 2.0 (the "License")
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//  
//      http://www.apache.org/licenses/LICENSE-2.0
//  
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.

using Microsoft.AspNetCore.Http;
using System.IO;
using Transmitly.Delivery;

namespace Transmitly.Microsoft.Aspnet.Mvc;

class DefaultRequestAdaptorContext(HttpRequest request) : IRequestAdaptorContext
{
    private readonly HttpRequest _httpRequest = Guard.AgainstNull(request);

    public string? GetQueryValue(string key)
    {
        if (_httpRequest.Query.TryGetValue(key, out var result))
            return result.ToString();
        return null;
    }
    public string? GetFormValue(string key)
    {
        if (_httpRequest.Form.TryGetValue(key, out var formResult))
            return formResult.ToString();
        return null;
    }

    public string? GetHeaderValue(string key)
    {
        if (_httpRequest.Headers.TryGetValue(key, out var headersResult))
            return headersResult.ToString();
        return null;
    }

    public string? GetValue(string key)
    {
        if (_httpRequest.Query.TryGetValue(key, out var result))
            return result.ToString();
        if (_httpRequest.Form.TryGetValue(key, out var formResult))
            return formResult.ToString();
        if (_httpRequest.Headers.TryGetValue(key, out var headersResult))
            return headersResult.ToString();

        return null;
    }

    public string? Content { get; } = new StreamReader(request.BodyReader.AsStream()).ReadToEnd();

    public string? PipelineName => GetValue(DeliveryUtil.PipelineNameKey);

    public string? ResourceId => GetValue(DeliveryUtil.ResourceIdKey);
}
